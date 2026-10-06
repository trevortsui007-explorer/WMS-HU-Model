using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using System.Data.SqlClient;
using Dapper;
using WMS_HU_Model.Infrastructure.Database;
using WMS_HU_Model.Infrastructure.Inbound;
using WMS_HU_Model.Models.Inbound;

namespace WMS_HU_Model.Repositories
{
    public class FinishedGoodsInboundRepository : IFinishedGoodsInboundRepository
    {
        public FinishedGoodsInboundPersistenceResult FindExisting(FinishedGoodsInboundCommand command)
        {
            using (SqlConnection conn = DbConnectionFactory.Create())
            {
                conn.Open();
                ReceiptRow existing = conn.QueryFirstOrDefault<ReceiptRow>(@"
SELECT Id, SnapshotHash, HashVersion, ReceiveDt AS ReceiveTime
FROM dbo.WMS_FG_FinishedGoodsInboundReceipt
WHERE SourceSystem = @SourceSystem AND SourceType = @SourceType AND SourceEntityId = @SourceEntityId;",
                    new { command.Snapshot.SourceSystem, command.Snapshot.SourceType, command.Snapshot.SourceEntityId });
                if (existing == null) return null;
                if (!string.Equals(existing.SnapshotHash, command.SnapshotHash, StringComparison.Ordinal) || !string.Equals(existing.HashVersion, command.HashVersion, StringComparison.Ordinal))
                    return new FinishedGoodsInboundPersistenceResult { IsConflict = true, Message = "同一Source已存在不同SnapshotHash或HashVersion。" };
                return new FinishedGoodsInboundPersistenceResult { Data = ToResult(existing, command, true), Message = "DUPLICATE" };
            }
        }

        public FinishedGoodsInboundPersistenceResult Persist(FinishedGoodsInboundCommand command)
        {
            using (SqlConnection conn = DbConnectionFactory.Create())
            {
                conn.Open();
                using (SqlTransaction tran = conn.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        ReceiptRow existing = conn.QueryFirstOrDefault<ReceiptRow>(@"
SELECT Id, SnapshotHash, HashVersion, ReceiveDt AS ReceiveTime
FROM dbo.WMS_FG_FinishedGoodsInboundReceipt WITH (UPDLOCK, HOLDLOCK)
WHERE SourceSystem = @SourceSystem AND SourceType = @SourceType AND SourceEntityId = @SourceEntityId;",
                            new { command.Snapshot.SourceSystem, command.Snapshot.SourceType, command.Snapshot.SourceEntityId }, tran);
                        if (existing != null)
                        {
                            tran.Commit();
                            if (!string.Equals(existing.SnapshotHash, command.SnapshotHash, StringComparison.Ordinal) || !string.Equals(existing.HashVersion, command.HashVersion, StringComparison.Ordinal))
                                return new FinishedGoodsInboundPersistenceResult { IsConflict = true, Message = "同一Source已存在不同SnapshotHash或HashVersion。" };
                            return new FinishedGoodsInboundPersistenceResult
                            {
                                Data = ToResult(existing, command, true),
                                Message = "DUPLICATE"
                            };
                        }

                        long locationId = GetInboundPendingLocationId(conn, tran, command.Snapshot.PlantCode);

                        long receiptId = conn.ExecuteScalar<long>(@"
INSERT INTO dbo.WMS_FG_FinishedGoodsInboundReceipt
(
    SourceSystem, SourceType, SourceEntityId, SnapshotHash, HashVersion,
    RequestId, ReceiveDt, ReceiveBy, Remark
)
OUTPUT INSERTED.Id
VALUES
(
    @SourceSystem, @SourceType, @SourceEntityId, @SnapshotHash, @HashVersion,
    @RequestId, @ReceiveTime, @Operator, @Remark
);", new
                        {
                            command.Snapshot.SourceSystem,
                            command.Snapshot.SourceType,
                            command.Snapshot.SourceEntityId,
                            command.SnapshotHash,
                            command.HashVersion,
                            command.RequestId,
                            command.Operator,
                            command.Remark,
                            command.ReceiveTime
                        }, tran);

                        LockAndValidateBarcodes(conn, tran, command);

                        long? palletId = null;
                        string businessNo = "FGI-" + receiptId;
                        int operationSequence = 0;
                        if (command.Snapshot.Pallet != null)
                        {
                            string palletCode = string.IsNullOrWhiteSpace(command.Snapshot.Pallet.PalletCode) ? "FGI-P-" + receiptId : command.Snapshot.Pallet.PalletCode.Trim();
                            palletId = InsertHu(conn, tran, palletCode, "PALLET", command.Snapshot.PlantCode, command.Snapshot.SourceSystem, command.Snapshot.Pallet.SourceEntityId, businessNo, command);
                            InsertOperation(conn, tran, CreateOperationNo(receiptId, ++operationSequence), "RECEIVE", palletId.Value, null, businessNo, command, "成品入库：托盘收货完成");
                        }

                        foreach (FinishedGoodsInboundBoxSnapshot box in command.Snapshot.Boxes)
                        {
                            bool weekException = box.InnerPacks.Exists(x => !x.WeekResolutionSucceeded);
                            long boxId = InsertHu(conn, tran, box.BoxCode.Trim(), "OUTER_BOX", command.Snapshot.PlantCode, command.Snapshot.SourceSystem, box.SourceEntityId, businessNo, command, weekException);
                            InsertOperation(conn, tran, CreateOperationNo(receiptId, ++operationSequence), "CREATE_HU", boxId, palletId, businessNo, command, "创建外箱：完成");
                            if (!string.IsNullOrWhiteSpace(box.AlternateBoxCode) && !string.Equals(box.AlternateBoxCode.Trim(), box.BoxCode.Trim(), StringComparison.Ordinal))
                                InsertIdentifier(conn, tran, boxId, "ALTERNATE_BOX_CODE", box.AlternateBoxCode.Trim(), command);
                            if (weekException) InsertWeekCodeHold(conn, tran, boxId, CreateOperationNo(receiptId, ++operationSequence), businessNo, box, command);
                            if (palletId.HasValue)
                            {
                                InsertRelation(conn, tran, palletId.Value, boxId, CreateRelationOperationNo(receiptId, ++operationSequence), command.Operator);
                                InsertOperation(conn, tran, CreateOperationNo(receiptId, ++operationSequence), "ATTACH_HU", boxId, palletId, businessNo, command, "托盘绑定：外箱绑定完成");
                            }
                            else
                            {
                                InsertDirectLocation(conn, tran, boxId, locationId, "OUTER_BOX", box.SourceEntityId, businessNo, command);
                                InsertOperation(conn, tran, CreateOperationNo(receiptId, ++operationSequence), "RECEIVE", boxId, null, businessNo, command, "成品入库：无托外箱收货完成");
                            }
                            foreach (FinishedGoodsInboundPackSnapshot pack in box.InnerPacks)
                            {
                                long packId = InsertHu(conn, tran, pack.PackCode.Trim(), "INNER_PACK", command.Snapshot.PlantCode, command.Snapshot.SourceSystem, pack.SourceEntityId, businessNo, command);
                                if (!string.IsNullOrWhiteSpace(pack.PackId))
                                    InsertPackIdIdentifier(conn, tran, packId, pack, command);
                                InsertRelation(conn, tran, boxId, packId, CreateRelationOperationNo(receiptId, ++operationSequence), command.Operator);
                                InsertItem(conn, tran, packId, box, pack, command);
                                InsertOperation(conn, tran, CreateOperationNo(receiptId, ++operationSequence), "CREATE_HU", packId, boxId, businessNo, command, "创建内包：完成并绑定外箱");
                            }
                        }
                        if (palletId.HasValue)
                        {
                            InsertDirectLocation(conn, tran, palletId.Value, locationId, "PALLET", command.Snapshot.Pallet.SourceEntityId, businessNo, command);
                            InboundConfirmRepository.MarkRequired(conn, tran, receiptId, palletId.Value, command.RequestId, command.Operator, command.ReceiveTime);
                        }
                        tran.Commit();
                        return new FinishedGoodsInboundPersistenceResult
                        {
                            Data = new FinishedGoodsInboundResultDto
                            {
                                ReceiptId = receiptId,
                                SourceSystem = command.Snapshot.SourceSystem,
                                SourceType = command.Snapshot.SourceType,
                                SourceEntityId = command.Snapshot.SourceEntityId,
                                SnapshotHash = command.SnapshotHash,
                                HashVersion = command.HashVersion,
                                ReceiveTime = command.ReceiveTime
                            },
                            Message = "SUCCESS"
                        };
                    }
                    catch
                    {
                        if (tran.Connection != null) tran.Rollback();
                        throw;
                    }
                }
            }
        }

        private static long InsertHu(SqlConnection conn, SqlTransaction tran, string code, string type, string plant, string sourceSystem, string sourceId, string businessNo, FinishedGoodsInboundCommand command, bool isHold = false)
        {
            return conn.ExecuteScalar<long>(@"
INSERT INTO dbo.WMS_FG_HU
(HUCode, HUType, PlantCode, LifecycleStatus, IsHold, SourceSystem, SourceEntityId, SourceBusinessNo, CreateBy, CreateDt)
OUTPUT INSERTED.Id
VALUES (@Code, @Type, @Plant, 'ACTIVE', @IsHold, @SourceSystem, @SourceId, @BusinessNo, @Operator, @ReceiveTime);",
                new { Code = code, Type = type, Plant = plant.Trim(), SourceSystem = sourceSystem.Trim(), SourceId = sourceId.Trim(), BusinessNo = businessNo, IsHold = isHold, command.Operator, command.ReceiveTime }, tran);
        }

        private static void InsertIdentifier(SqlConnection conn, SqlTransaction tran, long huId, string type, string value, FinishedGoodsInboundCommand command)
        {
            conn.Execute(@"
INSERT INTO dbo.WMS_FG_HU_Identifier
            (HUId, IdentifierType, IdentifierValue, IsActive, EffectiveFrom, CreateBy, CreateDt)
VALUES (@HuId, @Type, @Value, 1, @ReceiveTime, @Operator, @ReceiveTime);",
                new { HuId = huId, Type = type, Value = value, command.Operator, command.ReceiveTime }, tran);
        }

        private static void InsertPackIdIdentifier(SqlConnection conn, SqlTransaction tran, long huId, FinishedGoodsInboundPackSnapshot pack, FinishedGoodsInboundCommand command)
        {
            conn.Execute(@"
INSERT INTO dbo.WMS_FG_HU_Identifier
            (HUId, IdentifierType, IdentifierValue, IsActive, EffectiveFrom, CreateBy, CreateDt)
VALUES (@HuId, 'MES_PACK_ID', @PackId, 1, @ReceiveTime, @Operator, @ReceiveTime);",
                new
                {
                    HuId = huId,
                    PackId = pack.PackId.Trim(),
                    command.Operator,
                    command.ReceiveTime
                }, tran);
        }

        private static void InsertRelation(SqlConnection conn, SqlTransaction tran, long parentId, long childId, string operationNo, string operatorCode)
        {
            conn.Execute(@"
INSERT INTO dbo.WMS_FG_HU_Relation
(ParentHUId, ChildHUId, EffectiveFrom, IsActive, OperationNo, CreateBy)
VALUES (@ParentId, @ChildId, SYSDATETIME(), 1, @OperationNo, @Operator);",
                new { ParentId = parentId, ChildId = childId, OperationNo = operationNo, Operator = operatorCode }, tran);
        }

        private static void InsertItem(SqlConnection conn, SqlTransaction tran, long huId, FinishedGoodsInboundBoxSnapshot box, FinishedGoodsInboundPackSnapshot pack, FinishedGoodsInboundCommand command)
        {
            conn.Execute(@"
INSERT INTO dbo.WMS_FG_HU_Item
(
    HUId, ProdNo, ProdRev, DateCode, PackCreatedTime, LotNo, SAPBatchNo, Qty, Uom, StockStatus, IsActive,
    WeekFormatCode, WeekYear, WeekNo, WeekSortKey, WeekRuleSource, WeekRuleValue, WeekResolvedTime,
    CreateBy, CreateDt
)
VALUES
(
    @HuId, @ProdNo, @ProdRev, @DateCode, @PackCreatedTime, @LotNo, @SAPBatchNo, @Qty, @Uom, 'AVAILABLE', 1,
    @WeekFormatCode, @WeekYear, @WeekNo, @WeekSortKey, @WeekRuleSource, @WeekRuleValue, @WeekResolvedTime,
    @Operator, @ReceiveTime
);", new
            {
                HuId = huId,
                ProdNo = box.MaterialCode.Trim(),
                ProdRev = string.IsNullOrWhiteSpace(box.MaterialRevision) ? null : box.MaterialRevision.Trim(),
                DateCode = pack.DateCode,
                PackCreatedTime = pack.PackCreatedTime,
                LotNo = pack.LotNo,
                SAPBatchNo = pack.SapBatchNo,
                pack.Qty,
                Uom = box.Uom.Trim(),
                WeekFormatCode = pack.WeekCode == null ? null : pack.WeekCode.WeekFormatCode,
                WeekYear = pack.WeekCode == null ? (short?)null : pack.WeekCode.WeekYear,
                WeekNo = pack.WeekCode == null ? (byte?)null : pack.WeekCode.WeekNo,
                WeekSortKey = pack.WeekCode == null ? (int?)null : pack.WeekCode.WeekSortKey,
                WeekRuleSource = pack.WeekCode == null ? box.WeekRuleSource : pack.WeekCode.WeekRuleSource,
                WeekRuleValue = pack.WeekCode == null ? box.WeekRuleValue : pack.WeekCode.WeekRuleValue,
                WeekResolvedTime = pack.WeekCode == null ? (DateTime?)null : pack.WeekCode.WeekResolvedTime,
                command.Operator,
                command.ReceiveTime
            }, tran);
        }

        private static long GetInboundPendingLocationId(SqlConnection conn, SqlTransaction tran, string plantCode)
        {
            string plant = plantCode.Trim();
            List<InboundWarehouseRow> warehouses = conn.Query<InboundWarehouseRow>(@"
SELECT DISTINCT w.Id, w.WarehouseCode
FROM dbo.WMS_FG_WarehousePlant wp WITH (UPDLOCK, HOLDLOCK)
INNER JOIN dbo.WMS_FG_Warehouse w WITH (UPDLOCK, HOLDLOCK) ON w.Id = wp.WarehouseId
WHERE wp.PlantCode = @PlantCode AND wp.IsActive = 1
  AND w.IsActive = 1 AND w.WarehouseType = 'FINISHED_GOODS';",
                new { PlantCode = plant }, tran).AsList();
            if (warehouses.Count == 0)
                throw new InboundMappingException("厂别[" + plant + "]未配置启用的成品仓入库映射，请检查厂别映射及仓库状态。");
            if (warehouses.Count > 1)
                throw new InboundMappingException("厂别[" + plant + "]存在多个有效入库仓库["
                    + string.Join(",", warehouses.Select(x => x.WarehouseCode).OrderBy(x => x)) + "]，请保留唯一入库目标。");

            InboundWarehouseRow warehouse = warehouses[0];
            List<InboundLocationRow> locations = conn.Query<InboundLocationRow>(@"
SELECT l.Id, l.IsActive, l.IsVirtual, z.WarehouseId AS ZoneWarehouseId, z.IsActive AS ZoneIsActive
FROM dbo.WMS_FG_Location l WITH (UPDLOCK, HOLDLOCK)
LEFT JOIN dbo.WMS_FG_Zone z WITH (UPDLOCK, HOLDLOCK) ON z.Id = l.ZoneId
WHERE l.WarehouseId = @WarehouseId AND l.LocationCode = 'INBOUND-PENDING';",
                new { WarehouseId = warehouse.Id }, tran).AsList();
            string context = "厂别[" + plant + "]仓库[" + warehouse.WarehouseCode + "]";
            if (locations.Count != 1)
                throw new InboundMappingException(context + (locations.Count == 0
                    ? "缺少INBOUND-PENDING待入库货位。" : "存在多个INBOUND-PENDING待入库货位。"));
            InboundLocationRow location = locations[0];
            if (!location.IsActive || !location.IsVirtual)
                throw new InboundMappingException(context + "的INBOUND-PENDING必须为启用的虚拟货位。");
            if (location.ZoneWarehouseId != warehouse.Id || location.ZoneIsActive != true)
                throw new InboundMappingException(context + "的INBOUND-PENDING所属区域必须存在、启用且属于同一仓库。");
            return location.Id;
        }

        private class InboundWarehouseRow
        {
            public long Id { get; set; }
            public string WarehouseCode { get; set; }
        }

        private class InboundLocationRow
        {
            public long Id { get; set; }
            public bool IsActive { get; set; }
            public bool IsVirtual { get; set; }
            public long? ZoneWarehouseId { get; set; }
            public bool? ZoneIsActive { get; set; }
        }

        private static void InsertDirectLocation(SqlConnection conn, SqlTransaction tran, long huId, long locationId, string rootHuType, string rootSourceId, string businessNo, FinishedGoodsInboundCommand command)
        {
            string moveRequestId = FinishedGoodsInboundSnapshotHasher.CreateMoveRequestId(command.Snapshot, rootHuType, rootSourceId);
            string moveNo = FinishedGoodsInboundSnapshotHasher.CreateMoveNo(command.Snapshot, rootHuType, rootSourceId);
            long moveLogId = HuMoveLogWriter.Insert(conn, tran, moveNo, huId, null, locationId, "RECEIVE", null, businessNo, null, moveRequestId, null, command.Operator, command.ReceiveTime, command.Remark);
            conn.Execute(@"
INSERT INTO dbo.WMS_FG_HU_CurrentLocation
(HUId, LocationId, LocationSince, LastMoveLogId, CreateBy)
VALUES (@HuId, @LocationId, @ReceiveTime, @MoveLogId, @Operator);",
                new { HuId = huId, LocationId = locationId, command.ReceiveTime, MoveLogId = moveLogId, command.Operator }, tran);
        }

        private static void InsertWeekCodeHold(SqlConnection conn, SqlTransaction tran, long huId, string operationNo, string businessNo, FinishedGoodsInboundBoxSnapshot box, FinishedGoodsInboundCommand command)
        {
            string holdNo = "FGI-WEEK-" + huId;
            FinishedGoodsInboundPackSnapshot failedPack = box.InnerPacks.First(x => !x.WeekResolutionSucceeded);
            string reason = string.IsNullOrWhiteSpace(failedPack.WeekFailureCode) ? "WEEKCODE_UNRESOLVED" : failedPack.WeekFailureCode;
            if (!string.IsNullOrWhiteSpace(failedPack.WeekFailureMessage)) reason = failedPack.WeekFailureMessage;
            reason = BusinessLogText.Reason("冻结", BusinessLogText.MesError("周码解析失败：" + reason));
            conn.Execute(@"INSERT INTO dbo.WMS_FG_HU_HoldLog (HoldNo, HUId, HoldType, HoldReasonCode, HoldReason, IsActive, HoldDt, HoldBy)
VALUES (@HoldNo, @HuId, 'INBOUND_EXCEPTION', 'WEEKCODE_UNRESOLVED', @Reason, 1, @Now, @Operator);",
                new { HoldNo = holdNo, HuId = huId, Reason = reason, Now = command.ReceiveTime, command.Operator }, tran);
            InsertOperation(conn, tran, operationNo, "HU_HOLD", huId, null, businessNo, command, reason);
        }

        private static void LockAndValidateBarcodes(SqlConnection conn, SqlTransaction tran, FinishedGoodsInboundCommand command)
        {
            List<string> codes = new List<string>();
            if (command.Snapshot.Pallet != null && !string.IsNullOrWhiteSpace(command.Snapshot.Pallet.PalletCode)) codes.Add(command.Snapshot.Pallet.PalletCode.Trim().ToUpperInvariant());
            foreach (FinishedGoodsInboundBoxSnapshot box in command.Snapshot.Boxes)
            {
                codes.Add(box.BoxCode.Trim().ToUpperInvariant());
                if (!string.IsNullOrWhiteSpace(box.AlternateBoxCode)) codes.Add(box.AlternateBoxCode.Trim().ToUpperInvariant());
                foreach (FinishedGoodsInboundPackSnapshot pack in box.InnerPacks) codes.Add(pack.PackCode.Trim().ToUpperInvariant());
            }
            foreach (string code in codes.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal))
            {
                int lockResult = conn.ExecuteScalar<int>("DECLARE @r INT; EXEC @r = sp_getapplock @Resource=@Resource, @LockMode='Exclusive', @LockOwner='Transaction'; SELECT @r;", new { Resource = "WMS_BARCODE:" + code }, tran);
                if (lockResult < 0) throw new InvalidOperationException("无法获取扫码身份锁。");
                int conflicts = conn.ExecuteScalar<int>(@"SELECT COUNT(1) FROM (
SELECT Id FROM dbo.WMS_FG_HU WHERE UPPER(HUCode) = @Code
UNION ALL SELECT HUId FROM dbo.WMS_FG_HU_Identifier WHERE IsActive = 1 AND UPPER(IdentifierValue) = @Code) x;", new { Code = code }, tran);
                if (conflicts > 0) throw new InvalidOperationException("扫码身份已存在：" + code);
            }
        }

        private static void InsertOperation(SqlConnection conn, SqlTransaction tran, string operationNo, string operationType, long huId, long? relatedHuId, string businessNo, FinishedGoodsInboundCommand command, string remark)
        {
            HuOperationLogWriter.Insert(conn, tran, operationNo, operationType, huId, relatedHuId, businessNo, command.RequestId, command.Operator, command.ReceiveTime, null, operationType == "HU_HOLD" ? remark : command.Remark, operationType == "HU_HOLD" ? null : remark);
        }

        private static string CreateOperationNo(long receiptId, int sequence)
        {
            return "FGI-OP-" + receiptId + "-" + sequence.ToString("D4");
        }

        private static string CreateRelationOperationNo(long receiptId, int sequence)
        {
            return "FGI-REL-" + receiptId + "-" + sequence.ToString("D4");
        }

        private static FinishedGoodsInboundResultDto ToResult(ReceiptRow row, FinishedGoodsInboundCommand command, bool duplicate)
        {
            return new FinishedGoodsInboundResultDto
            {
                ReceiptId = row.Id,
                SourceSystem = command.Snapshot.SourceSystem,
                SourceType = command.Snapshot.SourceType,
                SourceEntityId = command.Snapshot.SourceEntityId,
                SnapshotHash = row.SnapshotHash,
                HashVersion = row.HashVersion,
                ReceiveTime = row.ReceiveTime,
                IsDuplicate = duplicate
            };
        }

        private class ReceiptRow { public long Id { get; set; } public string SnapshotHash { get; set; } public string HashVersion { get; set; } public DateTime ReceiveTime { get; set; } }
    }
}
