using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using WMS_HU_Model.Infrastructure.Database;
using WMS_HU_Model.Models.Relocation;

namespace WMS_HU_Model.Repositories
{
    public class RelocationRepository : IRelocationRepository
    {
        public BatchRelocationResultDto GetByRequestId(string requestId)
        {
            if (string.IsNullOrWhiteSpace(requestId)) return null;
            const string sql = @"
SELECT op.OperationNo, op.OperationDt AS MoveTime, box.HUCode AS BoxCode,
       source.LocationCode AS SourceLocationCode, target.LocationCode AS TargetLocationCode
FROM dbo.WMS_FG_HU_OperationLog op
INNER JOIN dbo.WMS_FG_HU box ON box.Id = op.HUId
INNER JOIN dbo.WMS_FG_HU_MoveLog moveLog ON moveLog.MoveNo = op.OperationNo AND moveLog.HUId = op.HUId
LEFT JOIN dbo.WMS_FG_Location source ON source.Id = moveLog.FromLocationId
INNER JOIN dbo.WMS_FG_Location target ON target.Id = moveLog.ToLocationId
WHERE op.OperationType = 'MOVE' AND moveLog.MoveType = 'RELOCATION' AND op.RequestId = @RequestId
ORDER BY box.HUCode;";
            List<BatchRelocationRow> rows = new List<BatchRelocationRow>();
            using (SqlConnection connection = DbConnectionFactory.Create())
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add("@RequestId", SqlDbType.VarChar, 100).Value = requestId;
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read()) rows.Add(new BatchRelocationRow
                    {
                        OperationNo = Convert.ToString(reader["OperationNo"]), MoveTime = Convert.ToDateTime(reader["MoveTime"]),
                        BoxCode = Convert.ToString(reader["BoxCode"]), SourceLocationCode = Convert.ToString(reader["SourceLocationCode"]),
                        TargetLocationCode = Convert.ToString(reader["TargetLocationCode"])
                    });
                }
            }
            if (rows.Count == 0) return null;
            return new BatchRelocationResultDto
            {
                OperationNo = BatchOperationNo(rows[0].OperationNo), SourceLocationCode = rows[0].SourceLocationCode,
                TargetLocationCode = rows[0].TargetLocationCode, BoxCodes = rows.Select(x => x.BoxCode).ToList(),
                MovedCount = rows.Count, IsDuplicate = true, MoveTime = rows.Min(x => x.MoveTime)
            };
        }

        public BatchRelocationPersistenceResult ExecuteBatch(BatchRelocationCommand command)
        {
            using (SqlConnection connection = DbConnectionFactory.Create())
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        const string requestSql = @"SELECT COUNT(1) FROM dbo.WMS_FG_HU_OperationLog WITH (UPDLOCK,HOLDLOCK) WHERE RequestId=@RequestId;";
                        using (SqlCommand requestCommand = new SqlCommand(requestSql, connection, transaction))
                        {
                            requestCommand.Parameters.Add("@RequestId", SqlDbType.VarChar, 100).Value = command.RequestId;
                            if (Convert.ToInt32(requestCommand.ExecuteScalar()) > 0) { transaction.Rollback(); return BatchRelocationPersistenceResult.Fail("RequestId已经被其他WMS业务使用。"); }
                        }

                        RelocationLocationRow source = ReadLocation(connection, transaction, command.WarehouseCode, command.SourceLocationCode);
                        RelocationLocationRow target = ReadLocation(connection, transaction, command.WarehouseCode, command.TargetLocationCode);
                        if (source == null) { transaction.Rollback(); return BatchRelocationPersistenceResult.Fail("源货位不存在或不属于指定仓库。"); }
                        if (target == null) { transaction.Rollback(); return BatchRelocationPersistenceResult.Fail("目标货位不存在或不属于指定仓库。"); }
                        if (!source.IsActive || source.IsVirtual || source.ZoneCode != "STORAGE") { transaction.Rollback(); return BatchRelocationPersistenceResult.Fail("源货位必须是可用的实体STORAGE货位。"); }
                        if (!target.IsActive || target.IsVirtual || target.ZoneCode != "STORAGE") { transaction.Rollback(); return BatchRelocationPersistenceResult.Fail("目标货位必须是可用的实体STORAGE货位。"); }
                        if (source.Id == target.Id) { transaction.Rollback(); return BatchRelocationPersistenceResult.Fail("源货位与目标货位不能相同。"); }

                        List<RelocationBoxRow> boxes = ReadBoxes(connection, transaction, command.BoxHuCodes);
                        if (boxes.Count != command.BoxHuCodes.Count) { transaction.Rollback(); return BatchRelocationPersistenceResult.Fail("一个或多个外箱不存在。"); }
                        if (boxes.Any(x => x.HuType != "OUTER_BOX")) { transaction.Rollback(); return BatchRelocationPersistenceResult.Fail("批量移库只支持OUTER_BOX。"); }
                        if (boxes.Any(x => x.LifecycleStatus != "ACTIVE")) { transaction.Rollback(); return BatchRelocationPersistenceResult.Fail("一个或多个外箱不是ACTIVE状态。"); }
                        if (boxes.Any(x => x.IsHold)) { transaction.Rollback(); return BatchRelocationPersistenceResult.Fail("一个或多个外箱已被冻结。"); }
                        if (boxes.Any(x => x.HasActiveParent)) { transaction.Rollback(); return BatchRelocationPersistenceResult.Fail("一个或多个外箱仍存在有效父HU，不能执行普通移库。"); }
                        if (boxes.Any(x => x.LocationId != source.Id)) { transaction.Rollback(); return BatchRelocationPersistenceResult.Fail("一个或多个外箱的有效源货位已变化。"); }
                        List<string> plants = boxes.Select(x => x.PlantCode).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                        foreach (string plant in plants)
                        {
                            const string plantSql = @"SELECT COUNT(1) FROM dbo.WMS_FG_WarehousePlant wp INNER JOIN dbo.WMS_FG_Warehouse w ON w.Id=wp.WarehouseId WHERE w.WarehouseCode=@WarehouseCode AND wp.PlantCode=@PlantCode AND wp.IsActive=1 AND w.IsActive=1;";
                            using (SqlCommand plantCommand = new SqlCommand(plantSql, connection, transaction))
                            {
                                plantCommand.Parameters.Add("@WarehouseCode", SqlDbType.VarChar, 30).Value = command.WarehouseCode;
                                plantCommand.Parameters.Add("@PlantCode", SqlDbType.VarChar, 20).Value = plant;
                                if (Convert.ToInt32(plantCommand.ExecuteScalar()) == 0) { transaction.Rollback(); return BatchRelocationPersistenceResult.Fail("目标仓库不允许存储外箱厂别[" + plant + "]。"); }
                            }
                        }
                        if (target.PlantPolicy == "DEDICATED" && plants.Any(x => !string.Equals(x, target.DedicatedPlantCode, StringComparison.OrdinalIgnoreCase))) { transaction.Rollback(); return BatchRelocationPersistenceResult.Fail("目标货位为其他厂别专用货位。"); }
                        if (target.PlantPolicy == "SAME_PLANT")
                        {
                            if (plants.Count != 1) { transaction.Rollback(); return BatchRelocationPersistenceResult.Fail("SAME_PLANT目标货位不能接收不同厂别的批量外箱。"); }
                            const string samePlantSql = @"SELECT COUNT(1) FROM dbo.WMS_FG_HU_CurrentLocation cl INNER JOIN dbo.WMS_FG_HU h ON h.Id=cl.HUId WHERE cl.LocationId=@LocationId AND h.PlantCode<>@PlantCode AND h.LifecycleStatus='ACTIVE';";
                            using (SqlCommand samePlantCommand = new SqlCommand(samePlantSql, connection, transaction))
                            {
                                samePlantCommand.Parameters.Add("@LocationId", SqlDbType.BigInt).Value = target.Id;
                                samePlantCommand.Parameters.Add("@PlantCode", SqlDbType.VarChar, 20).Value = plants[0];
                                if (Convert.ToInt32(samePlantCommand.ExecuteScalar()) > 0) { transaction.Rollback(); return BatchRelocationPersistenceResult.Fail("目标货位当前存在其他厂别库存。"); }
                            }
                        }

                        DateTime now = DateTime.Now;
                        List<string> moved = new List<string>();
                        for (int index = 0; index < boxes.Count; index++)
                        {
                            RelocationBoxRow box = boxes[index];
                            string moveNo = command.OperationNo + "-" + (index + 1).ToString("000");
                            string itemRequestId = command.RequestId + ":" + (index + 1).ToString("000");
                            long moveLogId = HuMoveLogWriter.Insert(connection, transaction, moveNo, box.Id, source.Id, target.Id, "RELOCATION",
                                target.LocationCode, null, null, itemRequestId, command.Reason, command.Operator, now, command.Remark);
                            const string updateSql = @"UPDATE dbo.WMS_FG_HU_CurrentLocation SET LocationId=@LocationId,LocationSince=@Now,LastMoveLogId=@MoveLogId,LastEditBy=@Operator,LastEditDt=@Now WHERE HUId=@HUId AND LocationId=@SourceLocationId;";
                            using (SqlCommand updateCommand = new SqlCommand(updateSql, connection, transaction))
                            {
                                updateCommand.Parameters.Add("@LocationId", SqlDbType.BigInt).Value = target.Id;
                                updateCommand.Parameters.Add("@Now", SqlDbType.DateTime2).Value = now;
                                updateCommand.Parameters.Add("@MoveLogId", SqlDbType.BigInt).Value = moveLogId;
                                updateCommand.Parameters.Add("@MoveNo", SqlDbType.VarChar, 50).Value = moveNo;
                                updateCommand.Parameters.Add("@Operator", SqlDbType.VarChar, 50).Value = DbParameterValue.StringOrDbNull(command.Operator);
                                updateCommand.Parameters.Add("@HUId", SqlDbType.BigInt).Value = box.Id;
                                updateCommand.Parameters.Add("@SourceLocationId", SqlDbType.BigInt).Value = source.Id;
                                if (updateCommand.ExecuteNonQuery() != 1) { transaction.Rollback(); return BatchRelocationPersistenceResult.Fail("外箱源货位已变化，批量移库已取消。"); }
                            }
                            HuOperationLogWriter.Insert(connection, transaction, moveNo, "MOVE", box.Id, null, null, command.RequestId, command.Operator, now, null, command.Remark);
                            moved.Add(box.HuCode);
                        }
                        transaction.Commit();
                        return BatchRelocationPersistenceResult.Success(new BatchRelocationResultDto
                        {
                            OperationNo = command.OperationNo, SourceLocationCode = source.LocationCode, TargetLocationCode = target.LocationCode,
                            BoxCodes = moved, MovedCount = moved.Count, IsDuplicate = false, MoveTime = now
                        });
                    }
                    catch
                    {
                        if (transaction.Connection != null) transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        private static RelocationLocationRow ReadLocation(SqlConnection connection, SqlTransaction transaction, string warehouseCode, string locationCode)
        {
            const string sql = @"SELECT l.Id,l.LocationCode,l.IsActive,l.IsVirtual,l.PlantPolicy,l.DedicatedPlantCode,z.ZoneCode FROM dbo.WMS_FG_Location l WITH (UPDLOCK,HOLDLOCK) INNER JOIN dbo.WMS_FG_Warehouse w ON w.Id=l.WarehouseId INNER JOIN dbo.WMS_FG_Zone z ON z.Id=l.ZoneId WHERE w.WarehouseCode=@WarehouseCode AND l.LocationCode=@LocationCode;";
            using (SqlCommand command = new SqlCommand(sql, connection, transaction))
            {
                command.Parameters.Add("@WarehouseCode", SqlDbType.VarChar, 30).Value = warehouseCode;
                command.Parameters.Add("@LocationCode", SqlDbType.VarChar, 50).Value = locationCode;
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read()) return null;
                    return new RelocationLocationRow
                    {
                        Id = Convert.ToInt64(reader["Id"]), LocationCode = Convert.ToString(reader["LocationCode"]), IsActive = Convert.ToBoolean(reader["IsActive"]),
                        IsVirtual = Convert.ToBoolean(reader["IsVirtual"]), PlantPolicy = Convert.ToString(reader["PlantPolicy"]),
                        DedicatedPlantCode = reader["DedicatedPlantCode"] == DBNull.Value ? null : Convert.ToString(reader["DedicatedPlantCode"]), ZoneCode = Convert.ToString(reader["ZoneCode"])
                    };
                }
            }
        }

        private static List<RelocationBoxRow> ReadBoxes(SqlConnection connection, SqlTransaction transaction, IList<string> boxCodes)
        {
            StringBuilder names = new StringBuilder();
            for (int index = 0; index < boxCodes.Count; index++) { if (index > 0) names.Append(","); names.Append("@Box").Append(index); }
            string sql = @"SELECT h.Id,h.HUCode,h.HUType,h.PlantCode,h.LifecycleStatus,h.IsHold,cl.LocationId,CAST(CASE WHEN parent.Id IS NULL THEN 0 ELSE 1 END AS bit) AS HasActiveParent FROM dbo.WMS_FG_HU h WITH (UPDLOCK,HOLDLOCK) LEFT JOIN dbo.WMS_FG_HU_CurrentLocation cl WITH (UPDLOCK,HOLDLOCK) ON cl.HUId=h.Id OUTER APPLY(SELECT TOP 1 r.Id FROM dbo.WMS_FG_HU_Relation r WITH (UPDLOCK,HOLDLOCK) INNER JOIN dbo.WMS_FG_HU p ON p.Id=r.ParentHUId AND p.LifecycleStatus='ACTIVE' WHERE r.ChildHUId=h.Id AND r.IsActive=1) parent WHERE h.HUCode IN (" + names + ") ORDER BY h.HUCode;";
            List<RelocationBoxRow> result = new List<RelocationBoxRow>();
            using (SqlCommand command = new SqlCommand(sql, connection, transaction))
            {
                for (int index = 0; index < boxCodes.Count; index++) command.Parameters.Add("@Box" + index, SqlDbType.VarChar, 100).Value = boxCodes[index];
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read()) result.Add(new RelocationBoxRow
                    {
                        Id = Convert.ToInt64(reader["Id"]), HuCode = Convert.ToString(reader["HUCode"]), HuType = Convert.ToString(reader["HUType"]),
                        PlantCode = Convert.ToString(reader["PlantCode"]), LifecycleStatus = Convert.ToString(reader["LifecycleStatus"]), IsHold = Convert.ToBoolean(reader["IsHold"]),
                        LocationId = reader["LocationId"] == DBNull.Value ? (long?)null : Convert.ToInt64(reader["LocationId"]), HasActiveParent = Convert.ToBoolean(reader["HasActiveParent"])
                    });
                }
            }
            return result;
        }

        private static string BatchOperationNo(string itemOperationNo)
        {
            int separator = itemOperationNo == null ? -1 : itemOperationNo.LastIndexOf('-');
            return separator > 0 ? itemOperationNo.Substring(0, separator) : itemOperationNo;
        }

        private class BatchRelocationRow { public string OperationNo { get; set; } public DateTime MoveTime { get; set; } public string BoxCode { get; set; } public string SourceLocationCode { get; set; } public string TargetLocationCode { get; set; } }
        private class RelocationLocationRow { public long Id { get; set; } public string LocationCode { get; set; } public bool IsActive { get; set; } public bool IsVirtual { get; set; } public string PlantPolicy { get; set; } public string DedicatedPlantCode { get; set; } public string ZoneCode { get; set; } }
        private class RelocationBoxRow { public long Id { get; set; } public string HuCode { get; set; } public string HuType { get; set; } public string PlantCode { get; set; } public string LifecycleStatus { get; set; } public bool IsHold { get; set; } public long? LocationId { get; set; } public bool HasActiveParent { get; set; } }
    }
}
