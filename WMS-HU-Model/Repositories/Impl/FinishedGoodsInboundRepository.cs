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
                            InsertOperation(conn, tran, CreateOperationNo(receiptId, ++operationSequence), "RECEIVE", palletId.Value, null, businessNo, command, "Finished goods inbound pallet received.");
                        }

                        foreach (FinishedGoodsInboundBoxSnapshot box in command.Snapshot.Boxes)
                        {
                            bool weekException = box.InnerPacks.Exists(x => !x.WeekResolutionSucceeded);
                            long boxId = InsertHu(conn, tran, box.BoxCode.Trim(), "OUTER_BOX", command.Snapshot.PlantCode, command.Snapshot.SourceSystem, box.SourceEntityId, businessNo, command, weekException);
                            InsertOperation(conn, tran, CreateOperationNo(receiptId, ++operationSequence), "CREATE_HU", boxId, palletId, businessNo, command, "Finished goods inbound outer box created.");
                            if (!string.IsNullOrWhiteSpace(box.AlternateBoxCode) && !string.Equals(box.AlternateBoxCode.Trim(), box.BoxCode.Trim(), StringComparison.Ordinal))
                                InsertIdentifier(conn, tran, boxId, "ALTERNATE_BOX_CODE", box.AlternateBoxCode.Trim(), command);
                            if (weekException) InsertWeekCodeHold(conn, tran, boxId, CreateOperationNo(receiptId, ++operationSequence), businessNo, box, command);
                            if (palletId.HasValue)
                            {
                                InsertRelation(conn, tran, palletId.Value, boxId, CreateRelationOperationNo(receiptId, ++operationSequence), command.Operator);
                                InsertOperation(conn, tran, CreateOperationNo(receiptId, ++operationSequence), "ATTACH_HU", boxId, palletId, businessNo, command, "Outer box attached to inbound pallet.");
                            }
                            else
                            {
                                long locationId = GetInboundPendingLocationId(conn, tran, command.Snapshot.PlantCode);
                                InsertDirectLocation(conn, tran, boxId, locationId, "OUTER_BOX", box.SourceEntityId, businessNo, command);
                                InsertOperation(conn, tran, CreateOperationNo(receiptId, ++operationSequence), "RECEIVE", boxId, null, businessNo, command, "Finished goods inbound root box received.");
                            }
                            foreach (FinishedGoodsInboundPackSnapshot pack in box.InnerPacks)
                            {
                                long packId = InsertHu(conn, tran, pack.PackCode.Trim(), "INNER_PACK", command.Snapshot.PlantCode, command.Snapshot.SourceSystem, pack.SourceEntityId, businessNo, command);
                                if (!string.IsNullOrWhiteSpace(pack.PackId))
                                    InsertPackIdIdentifier(conn, tran, packId, pack, command);
                                InsertRelation(conn, tran, boxId, packId, CreateRelationOperationNo(receiptId, ++operationSequence), command.Operator);
                                InsertItem(conn, tran, packId, box, pack, command);
                                InsertOperation(conn, tran, CreateOperationNo(receiptId, ++operationSequence), "CREATE_HU", packId, boxId, businessNo, command, "Finished goods inbound inner pack created and attached.");
                            }
                        }
                        if (palletId.HasValue)
                        {
                            long locationId = GetInboundPendingLocationId(conn, tran, command.Snapshot.PlantCode);
                            InsertDirectLocation(conn, tran, palletId.Value, locationId, "PALLET", command.Snapshot.Pallet.SourceEntityId, businessNo, command);
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
    HUId, ProdNo, ProdRev, DateCode, LotNo, SAPBatchNo, Qty, Uom, StockStatus, IsActive,
    WeekFormatCode, WeekYear, WeekNo, WeekSortKey, WeekRuleSource, WeekRuleValue, WeekResolvedTime,
    CreateBy, CreateDt
)
VALUES
(
    @HuId, @ProdNo, @ProdRev, @DateCode, @LotNo, @SAPBatchNo, @Qty, @Uom, 'AVAILABLE', 1,
    @WeekFormatCode, @WeekYear, @WeekNo, @WeekSortKey, @WeekRuleSource, @WeekRuleValue, @WeekResolvedTime,
    @Operator, @ReceiveTime
);", new
            {
                HuId = huId,
                ProdNo = box.MaterialCode.Trim(),
                ProdRev = string.IsNullOrWhiteSpace(box.MaterialRevision) ? null : box.MaterialRevision.Trim(),
                DateCode = pack.DateCode,
                LotNo = pack.LotNo,
                SAPBatchNo = pack.SapBatchNo,
                pack.Qty,
                Uom = box.Uom.Trim(),
                WeekFormatCode = pack.WeekCode == null ? null : pack.WeekCode.WeekFormatCode,
                WeekYear = pack.WeekCode == null ? (short?)null : pack.WeekCode.WeekYear,
                WeekNo = pack.WeekCode == null ? (byte?)null : pack.WeekCode.WeekNo,
                WeekSortKey = pack.WeekCode == null ? (int?)null : pack.WeekCode.WeekSortKey,
                WeekRuleSource = pack.WeekCode == null ? null : pack.WeekCode.WeekRuleSource,
                WeekRuleValue = pack.WeekCode == null ? null : pack.WeekCode.WeekRuleValue,
                WeekResolvedTime = pack.WeekCode == null ? (DateTime?)null : pack.WeekCode.WeekResolvedTime,
                command.Operator,
                command.ReceiveTime
            }, tran);
        }

        private static long GetInboundPendingLocationId(SqlConnection conn, SqlTransaction tran, string plantCode)
        {
            List<long> locations = conn.Query<long>(@"
SELECT l.Id
FROM dbo.WMS_FG_Location l WITH (UPDLOCK, HOLDLOCK)
INNER JOIN dbo.WMS_FG_WarehousePlant wp WITH (UPDLOCK, HOLDLOCK) ON wp.WarehouseId = l.WarehouseId
WHERE wp.PlantCode = @PlantCode AND wp.IsActive = 1
  AND l.LocationCode = 'INBOUND-PENDING' AND l.IsActive = 1 AND l.IsVirtual = 1;", new { PlantCode = plantCode.Trim() }, tran).AsList();
            if (locations.Count != 1) throw new InvalidOperationException(locations.Count == 0 ? "未找到有效INBOUND-PENDING虚拟库位。" : "存在多个有效INBOUND-PENDING虚拟库位。");
            return locations[0];
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
            if (!string.IsNullOrWhiteSpace(failedPack.WeekFailureMessage)) reason += ": " + failedPack.WeekFailureMessage;
            if (reason.Length > 500) reason = reason.Substring(0, 500);
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
            HuOperationLogWriter.Insert(conn, tran, operationNo, operationType, huId, relatedHuId, businessNo, command.RequestId, command.Operator, command.ReceiveTime, null, remark);
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
