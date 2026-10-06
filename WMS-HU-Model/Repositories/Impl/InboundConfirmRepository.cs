using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using Dapper;
using Newtonsoft.Json;
using WMS_HU_Model.Infrastructure.Database;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.Inbound;

namespace WMS_HU_Model.Repositories
{
    public class InboundConfirmRepository : IInboundConfirmRepository
    {
        public const string PendingMessage = "请先完成整托入库确认，再进行上架。";

        public static long? LockPallet(SqlConnection connection, SqlTransaction transaction, string palletCode)
        {
            long? id = connection.Query<long?>("SELECT Id FROM dbo.WMS_FG_HU WITH (UPDLOCK,HOLDLOCK) WHERE HUCode=@Code;", new { Code = palletCode }, transaction).SingleOrDefault();
            if (id.HasValue) LockContents(connection, transaction, id.Value);
            return id;
        }

        public static void LockPallet(SqlConnection connection, SqlTransaction transaction, long palletId)
        {
            connection.ExecuteScalar<long?>("SELECT Id FROM dbo.WMS_FG_HU WITH (UPDLOCK,HOLDLOCK) WHERE Id=@Id;", new { Id = palletId }, transaction);
            LockContents(connection, transaction, palletId);
        }

        private static void LockContents(SqlConnection connection, SqlTransaction transaction, long palletId)
        {
            connection.Query<long>("SELECT Id FROM dbo.WMS_FG_HU_Relation WITH (UPDLOCK,HOLDLOCK) WHERE ParentHUId=@Id AND IsActive=1 ORDER BY Id;", new { Id = palletId }, transaction).AsList();
            connection.Query<long>(@"SELECT h.Id FROM dbo.WMS_FG_HU h WITH (UPDLOCK,HOLDLOCK)
INNER JOIN dbo.WMS_FG_HU_Relation r ON r.ChildHUId=h.Id WHERE r.ParentHUId=@Id AND r.IsActive=1 ORDER BY h.Id;", new { Id = palletId }, transaction).AsList();
            connection.Query<long>(@"SELECT cl.HUId FROM dbo.WMS_FG_HU_CurrentLocation cl WITH (UPDLOCK,HOLDLOCK)
WHERE cl.HUId=@Id OR EXISTS(SELECT 1 FROM dbo.WMS_FG_HU_Relation r WHERE r.ParentHUId=@Id AND r.ChildHUId=cl.HUId AND r.IsActive=1) ORDER BY cl.HUId;", new { Id = palletId }, transaction).AsList();
        }

        public static bool IsRequired(SqlConnection connection, SqlTransaction transaction, long palletId)
        {
            return connection.ExecuteScalar<int>("SELECT COUNT(*) FROM dbo.WMS_FG_HU_OperationLog WHERE HUId=@Id AND OperationType='INBOUND_CONFIRM_REQUIRED';", new { Id = palletId }, transaction) > 0;
        }

        public static void MarkRequired(SqlConnection connection, SqlTransaction transaction, long receiptId, long palletId,
            string requestId, string operatorCode, DateTime receiveTime)
        {
            List<RelationSnapshot> relations = connection.Query<RelationSnapshot>(@"
SELECT r.Id AS RelationId,r.ChildHUId AS BoxHuId FROM dbo.WMS_FG_HU_Relation r
INNER JOIN dbo.WMS_FG_HU h ON h.Id=r.ChildHUId AND h.HUType='OUTER_BOX'
WHERE r.ParentHUId=@Id AND r.IsActive=1 ORDER BY r.Id;", new { Id = palletId }, transaction).ToList();
            string details = JsonConvert.SerializeObject(new RequiredSnapshot { ReceiptId = receiptId, Relations = relations });
            HuOperationLogWriter.Insert(connection, transaction, "FGI-CONFIRM-" + receiptId, "INBOUND_CONFIRM_REQUIRED", palletId, null,
                "FGI-" + receiptId, requestId, operatorCode, receiveTime, details, null);
        }

        // All callers lock the pallet first. This then locks relations/boxes before current locations.
        public static string GetPutawayBlockReason(SqlConnection connection, SqlTransaction transaction, long palletId)
        {
            PalletState state = ReadState(connection, transaction, palletId);
            if (state == null || state.Requirement == null) return null; // Historical pallet.
            if (state.Error != null) return state.Error;
            if (!state.Completed || state.Boxes.Count == 0 || state.Boxes.Any(x => x.Confirm == null || !IsConfirmedLocation(x.Location, state.Location)))
                return PendingMessage;
            return null;
        }

        public OperationResult<InboundConfirmPalletDto> GetPallet(string palletHuCode)
        {
            using (SqlConnection connection = DbConnectionFactory.Create())
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    // MES labels carry PlanStock's source identifier; keep HUCode as the canonical code.
                    List<long> candidates = connection.Query<long>(@"
SELECT TOP (2) Id FROM dbo.WMS_FG_HU WITH (UPDLOCK,HOLDLOCK)
WHERE HUType='PALLET'
  AND (HUCode=@Code OR (SourceSystem='MES' AND SourceEntityId=@Code));",
                        new { Code = palletHuCode }, transaction).ToList();
                    if (candidates.Count == 0)
                        return OperationResult<InboundConfirmPalletDto>.Fail("未找到对应托盘，请核对托盘码或MES来源记录标识");
                    if (candidates.Count > 1)
                        return OperationResult<InboundConfirmPalletDto>.Fail("扫描标识对应多个托盘，请使用明确的托盘码");
                    LockPallet(connection, transaction, candidates[0]);
                    PalletState state = ReadState(connection, transaction, candidates[0]);
                    if (state == null || state.Requirement == null || state.HuType != "PALLET" || state.LifecycleStatus != "ACTIVE")
                        return OperationResult<InboundConfirmPalletDto>.Fail("没有需要入库确认的当前托盘；历史托盘无需补确认。");
                    InboundConfirmPalletDto result = ToDto(state);
                    transaction.Commit();
                    return OperationResult<InboundConfirmPalletDto>.Success(result);
                }
            }
        }

        public OperationResult<InboundConfirmPalletDto> ConfirmBox(InboundConfirmBoxRequest request)
        {
            using (SqlConnection connection = DbConnectionFactory.Create())
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        long? id = LockPallet(connection, transaction, request.PalletHuCode);
                        PalletState state = id.HasValue ? ReadState(connection, transaction, id.Value) : null;
                        if (state == null || state.Requirement == null)
                            return OperationResult<InboundConfirmPalletDto>.Fail("托盘不存在或不需要入库确认。");
                        if (state.Error != null) return OperationResult<InboundConfirmPalletDto>.Fail(state.Error);
                        BoxState box = state.Boxes.FirstOrDefault(x => Same(x.HuCode, request.BoxHuCode));
                        if (box == null) return OperationResult<InboundConfirmPalletDto>.Fail("该外箱不属于当前托盘。");

                        int conflicts = connection.ExecuteScalar<int>(@"
SELECT COUNT(*) FROM dbo.WMS_FG_HU_OperationLog WITH (UPDLOCK,HOLDLOCK)
WHERE RequestId=@RequestId AND NOT
 ((OperationType='INBOUND_CONFIRM' AND HUId=@BoxId AND RelatedHUId=@PalletId)
  OR (OperationType='INBOUND_CONFIRM_COMPLETED' AND HUId=@PalletId));
", new { request.RequestId, BoxId = box.Id, PalletId = state.Id }, transaction);
                        bool moveConflict = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM dbo.WMS_FG_HU_MoveLog WITH (UPDLOCK,HOLDLOCK) WHERE RequestId=@RequestId;", new { request.RequestId }, transaction) > 0;
                        if (conflicts > 0 || moveConflict) return OperationResult<InboundConfirmPalletDto>.Fail("RequestId已经被其他业务使用。");
                        if (box.Confirm != null)
                        {
                            InboundConfirmPalletDto duplicate = ToDto(state);
                            duplicate.IsDuplicate = true;
                            transaction.Commit();
                            return OperationResult<InboundConfirmPalletDto>.Success(duplicate, "该箱已确认，保留首次确认记录。");
                        }
                        if (state.Completed) return OperationResult<InboundConfirmPalletDto>.Fail("托盘确认完成后外箱集合发生变化，请核实。");
                        if (state.Boxes.Any(x => x.Location != null)) return OperationResult<InboundConfirmPalletDto>.Fail("未完成确认的外箱已有直接库位，请核实。");

                        DateTime now = connection.ExecuteScalar<DateTime>("SELECT SYSDATETIME();", transaction: transaction);
                        string operationNo = "IC-" + Guid.NewGuid().ToString("N");
                        string details = JsonConvert.SerializeObject(new { method = request.ConfirmMethod });
                        HuOperationLogWriter.Insert(connection, transaction, operationNo, "INBOUND_CONFIRM", box.Id, state.Id,
                            state.BusinessNo, request.RequestId, request.Operator, now, details, null, request.ConfirmMethod == "SCANNED" ? "入库确认：外箱扫码确认完成" : "入库确认：外箱手动确认完成");
                        box.Confirm = new ConfirmLog { DetailJson = details, OperationBy = request.Operator, OperationDt = now };
                        if (state.Boxes.All(x => x.Confirm != null))
                        {
                            string failure = Complete(connection, transaction, state, request, now, operationNo);
                            if (failure != null) return OperationResult<InboundConfirmPalletDto>.Fail(failure);
                        }
                        InboundConfirmPalletDto result = ToDto(state);
                        transaction.Commit();
                        return OperationResult<InboundConfirmPalletDto>.Success(result,
                            state.Completed ? "已确认入账，可进行上架。" : "外箱确认成功。");
                    }
                    catch (SqlException exception)
                    {
                        if (exception.Number != 2601 && exception.Number != 2627 && exception.Number != 1205) throw;
                        return OperationResult<InboundConfirmPalletDto>.Fail("确认发生并发冲突，请刷新后重试。");
                    }
                }
            }
        }

        private static string Complete(SqlConnection connection, SqlTransaction transaction, PalletState state,
            InboundConfirmBoxRequest request, DateTime now, string operationNo)
        {
            LocationState target = connection.QuerySingleOrDefault<LocationState>(@"
SELECT l.Id,l.WarehouseId,l.LocationCode,l.IsActive,l.IsVirtual,l.PlantPolicy,l.DedicatedPlantCode,z.ZoneCode
FROM dbo.WMS_FG_Location l WITH (UPDLOCK,HOLDLOCK)
INNER JOIN dbo.WMS_FG_Zone z ON z.Id=l.ZoneId
WHERE l.WarehouseId=@WarehouseId AND l.LocationCode='INBOUND-CONFIRMED';", new { state.Location.WarehouseId }, transaction);
            if (!IsConfirmedLocation(target, state.Location)) return "未配置可用的INBOUND-CONFIRMED收货虚拟库位。";
            if (!Same(target.PlantPolicy, state.Location.PlantPolicy) || !Same(target.DedicatedPlantCode, state.Location.DedicatedPlantCode))
                return "INBOUND-CONFIRMED与INBOUND-PENDING厂别策略不一致。";
            if (target.PlantPolicy == "DEDICATED" && !Same(target.DedicatedPlantCode, state.PlantCode)) return "确认库位不允许当前厂别。";
            if (target.PlantPolicy == "SAME_PLANT" && connection.ExecuteScalar<int>(@"
SELECT COUNT(*) FROM dbo.WMS_FG_HU_CurrentLocation cl WITH (UPDLOCK,HOLDLOCK)
INNER JOIN dbo.WMS_FG_HU h ON h.Id=cl.HUId WHERE cl.LocationId=@Id AND h.LifecycleStatus='ACTIVE' AND h.PlantCode<>@Plant;",
                new { target.Id, Plant = state.PlantCode }, transaction) > 0) return "确认库位存在其他厂别库存。";
            foreach (BoxState box in state.Boxes)
            {
                string moveNo = "ICM-" + Guid.NewGuid().ToString("N");
                long moveId = HuMoveLogWriter.Insert(connection, transaction, moveNo, box.Id, state.Location.Id, target.Id,
                    "INBOUND_CONFIRM", null, state.BusinessNo, null, moveNo, null, request.Operator, now, null);
                connection.Execute(@"
INSERT INTO dbo.WMS_FG_HU_CurrentLocation(HUId,LocationId,LocationSince,LastMoveLogId,CreateBy)
VALUES(@Id,@LocationId,@Now,@MoveId,@Operator);", new { box.Id, LocationId = target.Id, Now = now, MoveId = moveId, request.Operator }, transaction);
                box.Location = target;
            }
            HuOperationLogWriter.Insert(connection, transaction, operationNo + "-C", "INBOUND_CONFIRM_COMPLETED", state.Id, null,
                state.BusinessNo, request.RequestId, request.Operator, now, null, null);
            state.Completed = true;
            return null;
        }

        private static PalletState ReadState(SqlConnection connection, SqlTransaction transaction, long palletId)
        {
            PalletState state = connection.QuerySingleOrDefault<PalletState>(@"
SELECT Id,HUCode,HUType,PlantCode,LifecycleStatus,SourceBusinessNo AS BusinessNo
FROM dbo.WMS_FG_HU WHERE Id=@Id;", new { Id = palletId }, transaction);
            if (state == null) return null;
            ConfirmLog required = connection.QueryFirstOrDefault<ConfirmLog>(@"
SELECT TOP 1 DetailJson FROM dbo.WMS_FG_HU_OperationLog
WHERE HUId=@Id AND OperationType='INBOUND_CONFIRM_REQUIRED' ORDER BY OperationDt,Id;", new { Id = palletId }, transaction);
            if (required == null) return state;
            state.Requirement = string.IsNullOrWhiteSpace(required.DetailJson) ? null : JsonConvert.DeserializeObject<RequiredSnapshot>(required.DetailJson);
            if (state.Requirement == null || state.Requirement.Relations == null) throw new InvalidOperationException("入库确认要求日志缺少托箱关系快照。");
            state.Completed = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM dbo.WMS_FG_HU_OperationLog WHERE HUId=@Id AND OperationType='INBOUND_CONFIRM_COMPLETED';", new { Id = palletId }, transaction) > 0;
            state.Boxes = connection.Query<BoxState>(@"
SELECT r.Id AS RelationId,h.Id,h.HUCode,h.LifecycleStatus,h.PlantCode
FROM dbo.WMS_FG_HU_Relation r WITH (UPDLOCK,HOLDLOCK)
INNER JOIN dbo.WMS_FG_HU h WITH (UPDLOCK,HOLDLOCK) ON h.Id=r.ChildHUId
WHERE r.ParentHUId=@Id AND r.IsActive=1 AND h.HUType='OUTER_BOX' ORDER BY r.Id;", new { Id = palletId }, transaction).ToList();
            List<long> huIds = state.Boxes.Select(x => x.Id).Concat(new[] { state.Id }).ToList();
            Dictionary<long, LocationState> locations = connection.Query<LocationState>(@"
SELECT cl.HUId,l.Id,l.WarehouseId,l.LocationCode,l.IsActive,l.IsVirtual,l.PlantPolicy,l.DedicatedPlantCode,z.ZoneCode
FROM dbo.WMS_FG_HU_CurrentLocation cl WITH (UPDLOCK,HOLDLOCK)
INNER JOIN dbo.WMS_FG_Location l ON l.Id=cl.LocationId
INNER JOIN dbo.WMS_FG_Zone z ON z.Id=l.ZoneId WHERE cl.HUId IN @Ids ORDER BY cl.HUId;", new { Ids = huIds }, transaction).ToDictionary(x => x.HuId);
            LocationState location;
            state.Location = locations.TryGetValue(state.Id, out location) ? location : null;
            Dictionary<long, ConfirmLog> confirmations = connection.Query<ConfirmLog>(@"
SELECT firstLog.HUId,firstLog.DetailJson,firstLog.OperationBy,firstLog.OperationDt
FROM dbo.WMS_FG_HU_Relation r
CROSS APPLY(SELECT TOP 1 op.HUId,op.DetailJson,op.OperationBy,op.OperationDt
    FROM dbo.WMS_FG_HU_OperationLog op
    WHERE op.HUId=r.ChildHUId AND op.RelatedHUId=@Id AND op.OperationType='INBOUND_CONFIRM'
    ORDER BY op.OperationDt,op.Id) firstLog
WHERE r.ParentHUId=@Id AND r.IsActive=1;", new { Id = palletId }, transaction).ToDictionary(x => x.HuId);
            foreach (BoxState box in state.Boxes)
            {
                box.Location = locations.TryGetValue(box.Id, out location) ? location : null;
                ConfirmLog confirmation;
                box.Confirm = confirmations.TryGetValue(box.Id, out confirmation) ? confirmation : null;
            }
            if (state.HuType != "PALLET" || state.LifecycleStatus != "ACTIVE" || state.Location == null
                || !state.Location.IsActive || !state.Location.IsVirtual || state.Location.LocationCode != "INBOUND-PENDING" || state.Location.ZoneCode != "RECEIVE")
                state.Error = "托盘必须有效且仍位于RECEIVE区的INBOUND-PENDING虚拟库位。";
            else if (state.Boxes.Count == 0) state.Error = "托盘当前没有有效外箱关系。";
            else if (state.Boxes.Any(x => x.LifecycleStatus != "ACTIVE" || !Same(x.PlantCode, state.PlantCode))) state.Error = "托盘存在非ACTIVE或不同厂别外箱。";
            else if (state.Boxes.Any(x => !state.Requirement.Relations.Any(r => r.RelationId == x.RelationId && r.BoxHuId == x.Id))
                || (!state.Completed && state.Boxes.Count != state.Requirement.Relations.Count)) state.Error = "托盘外箱关系已变化或新增外箱，请核实。";
            else if (state.Completed && state.Boxes.Any(x => x.Confirm == null || !IsConfirmedLocation(x.Location, state.Location))) state.Error = PendingMessage;
            else if (!state.Completed && state.Boxes.Any(x => x.Location != null)) state.Error = "未完成确认的外箱已有直接库位，请核实。";
            if (state.Completed && state.Error == null)
            {
                List<long> removed = state.Requirement.Relations.Where(r => !state.Boxes.Any(x => x.RelationId == r.RelationId)).Select(r => r.RelationId).ToList();
                if (removed.Count > 0 && connection.ExecuteScalar<int>(@"SELECT COUNT(*) FROM dbo.WMS_FG_HU_Relation
WHERE Id IN @Ids AND ParentHUId=@PalletId AND IsActive=0 AND CloseReason='PUTAWAY';", new { Ids = removed, PalletId = state.Id }, transaction) != removed.Count)
                    state.Error = "托盘外箱关系发生非上架变更，请核实。";
            }
            return state;
        }

        private static bool IsConfirmedLocation(LocationState location, LocationState palletLocation)
        {
            return location != null && palletLocation != null && location.IsActive && location.IsVirtual
                && location.LocationCode == "INBOUND-CONFIRMED" && location.ZoneCode == "RECEIVE" && location.WarehouseId == palletLocation.WarehouseId;
        }

        private static InboundConfirmPalletDto ToDto(PalletState state)
        {
            InboundConfirmPalletDto result = new InboundConfirmPalletDto
            {
                ReceiptId = state.Requirement.ReceiptId, PalletHuCode = state.HuCode, PlantCode = state.PlantCode,
                Status = state.Completed ? "CONFIRMED" : "PENDING", CanConfirm = !state.Completed && state.Error == null,
                BlockReason = state.Error, TotalBoxCount = state.Boxes.Count, ConfirmedBoxCount = state.Boxes.Count(x => x.Confirm != null)
            };
            foreach (BoxState box in state.Boxes.OrderBy(x => x.HuCode))
            {
                ConfirmDetails details = box.Confirm == null ? null : JsonConvert.DeserializeObject<ConfirmDetails>(box.Confirm.DetailJson);
                result.Boxes.Add(new InboundConfirmBoxDto { BoxHuCode = box.HuCode, ConfirmMethod = details == null ? null : details.Method,
                    ConfirmedBy = box.Confirm == null ? null : box.Confirm.OperationBy, ConfirmedAt = box.Confirm == null ? (DateTime?)null : box.Confirm.OperationDt });
            }
            return result;
        }

        private static bool Same(string left, string right) { return string.Equals(left, right, StringComparison.OrdinalIgnoreCase); }
        private sealed class RequiredSnapshot { public long ReceiptId { get; set; } public List<RelationSnapshot> Relations { get; set; } }
        private sealed class RelationSnapshot { public long RelationId { get; set; } public long BoxHuId { get; set; } }
        private sealed class ConfirmDetails { public string Method { get; set; } }
        private sealed class ConfirmLog { public long HuId { get; set; } public string DetailJson { get; set; } public string OperationBy { get; set; } public DateTime OperationDt { get; set; } }
        private sealed class LocationState
        {
            public long HuId { get; set; } public long Id { get; set; } public long WarehouseId { get; set; }
            public string LocationCode { get; set; } public string ZoneCode { get; set; }
            public bool IsActive { get; set; } public bool IsVirtual { get; set; }
            public string PlantPolicy { get; set; } public string DedicatedPlantCode { get; set; }
        }
        private sealed class BoxState
        {
            public long RelationId { get; set; } public long Id { get; set; } public string HuCode { get; set; }
            public string LifecycleStatus { get; set; } public string PlantCode { get; set; }
            public LocationState Location { get; set; } public ConfirmLog Confirm { get; set; }
        }
        private sealed class PalletState
        {
            public long Id { get; set; } public string HuCode { get; set; } public string HuType { get; set; }
            public string LifecycleStatus { get; set; } public string PlantCode { get; set; } public string BusinessNo { get; set; }
            public RequiredSnapshot Requirement { get; set; } public bool Completed { get; set; } public string Error { get; set; }
            public LocationState Location { get; set; } public List<BoxState> Boxes { get; set; } = new List<BoxState>();
        }
    }
}
