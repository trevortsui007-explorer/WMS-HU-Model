using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using WMS_HU_Model.Infrastructure.Database;
using WMS_HU_Model.Models.Repack;

namespace WMS_HU_Model.Repositories
{
    internal class WholeBoxUnpackRepository
    {
        public UnpackBoxResultDto GetByRequestId(string requestId)
        {
            if (string.IsNullOrWhiteSpace(requestId)) return null;
            const string headerSql = @"
SELECT TOP 1 op.OperationNo, box.HUCode AS BoxHuCode, op.OperationDt AS OperationTime
FROM dbo.WMS_FG_HU_OperationLog op
INNER JOIN dbo.WMS_FG_HU box ON box.Id = op.HUId
WHERE op.OperationType = 'UNPACK_BOX' AND op.RequestId = @RequestId;";
            HeaderRow header = DbExecutor.QueryFirstOrDefault<HeaderRow>(headerSql, new { RequestId = requestId });
            if (header == null) return null;
            const string itemSql = @"
SELECT innerHU.HUCode AS InnerPackCode, sourceLoc.LocationCode AS FromLocationCode, targetLoc.LocationCode AS ToLocationCode
FROM dbo.WMS_FG_HU_OperationLog op
INNER JOIN dbo.WMS_FG_HU innerHU ON innerHU.Id = op.HUId
INNER JOIN dbo.WMS_FG_HU_MoveLog moveLog ON moveLog.MoveNo = op.OperationNo AND moveLog.HUId = op.HUId
LEFT JOIN dbo.WMS_FG_Location sourceLoc ON sourceLoc.Id = moveLog.FromLocationId
INNER JOIN dbo.WMS_FG_Location targetLoc ON targetLoc.Id = moveLog.ToLocationId
WHERE op.OperationType = 'UNPACK_INNER' AND op.RequestId = @RequestId
ORDER BY innerHU.HUCode;";
            List<ItemRow> items = DbExecutor.Query<ItemRow>(itemSql, new { RequestId = requestId });
            return new UnpackBoxResultDto
            {
                OperationNo = header.OperationNo,
                BoxHuCode = header.BoxHuCode,
                FromLocationCode = items.Select(x => x.FromLocationCode).FirstOrDefault(),
                ToLocationCode = items.Select(x => x.ToLocationCode).FirstOrDefault(),
                InnerPackCodes = items.Select(x => x.InnerPackCode).ToList(),
                UnpackedCount = items.Count,
                IsDuplicate = true,
                OperationTime = header.OperationTime
            };
        }

        public UnpackBoxPersistenceResult Execute(UnpackBoxCommand command)
        {
            using (SqlConnection connection = DbConnectionFactory.Create())
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        DateTime now = DateTime.Now;
                        if (RequestIdExists(connection, transaction, command.RequestId))
                        {
                            transaction.Rollback();
                            return UnpackBoxPersistenceResult.Fail("RequestId已经被其他WMS业务使用。");
                        }

                        SourceRow source = ReadSource(connection, transaction, command.BoxHuCode);
                        string sourceError = ValidateSource(source, command.WarehouseCode);
                        if (sourceError != null)
                        {
                            transaction.Rollback();
                            return UnpackBoxPersistenceResult.Fail(sourceError);
                        }

                        string targetCode = string.IsNullOrWhiteSpace(command.ToLocationCode) ? source.LocationCode : command.ToLocationCode;
                        TargetRow target = ReadTarget(connection, transaction, command.WarehouseCode, targetCode);
                        string targetError = ValidateTarget(connection, transaction, target, command.WarehouseCode, source.PlantCode);
                        if (targetError != null)
                        {
                            transaction.Rollback();
                            return UnpackBoxPersistenceResult.Fail(targetError);
                        }

                        List<ChildRow> children = ReadChildren(connection, transaction, source.Id);
                        string childError = ValidateChildren(children, source.PlantCode);
                        if (childError != null)
                        {
                            transaction.Rollback();
                            return UnpackBoxPersistenceResult.Fail(childError);
                        }

                        HuOperationLogWriter.Insert(connection, transaction, command.OperationNo, "UNPACK_BOX", source.Id, null,
                            command.BusinessNo, command.RequestId, command.Operator, now, null, command.Remark);
                        List<string> unpacked = new List<string>();
                        for (int index = 0; index < children.Count; index++)
                        {
                            ChildRow child = children[index];
                            string itemOperationNo = command.OperationNo + "-" + (index + 1).ToString("000");
                            string itemRequestId = command.RequestId + ":" + (index + 1).ToString("000");
                            if (!CloseRelation(connection, transaction, child.RelationId, itemOperationNo, command.Operator, now))
                            {
                                transaction.Rollback();
                                return UnpackBoxPersistenceResult.Fail("内包关系已变化，整箱拆包已取消。");
                            }
                            long moveLogId = HuMoveLogWriter.Insert(connection, transaction, itemOperationNo, child.Id,
                                source.LocationId.Value, target.Id, "UNPACK_INNER", command.ScannedLocationCode ?? target.LocationCode,
                                command.BusinessNo, null, itemRequestId, null, command.Operator, now, command.Remark);
                            InsertCurrentLocation(connection, transaction, child.Id, target.Id, moveLogId, itemOperationNo, command.Operator, now);
                            HuOperationLogWriter.Insert(connection, transaction, itemOperationNo, "DETACH_HU", source.Id, child.Id,
                                command.BusinessNo, command.RequestId, command.Operator, now, null, "整箱拆包：解除外箱与内包关系");
                            HuOperationLogWriter.Insert(connection, transaction, itemOperationNo, "UNPACK_INNER", child.Id, source.Id,
                                command.BusinessNo, command.RequestId, command.Operator, now, null, command.Remark);
                            unpacked.Add(child.HuCode);
                        }

                        transaction.Commit();
                        return UnpackBoxPersistenceResult.Success(new UnpackBoxResultDto
                        {
                            OperationNo = command.OperationNo,
                            BoxHuCode = source.HuCode,
                            FromLocationCode = source.LocationCode,
                            ToLocationCode = target.LocationCode,
                            InnerPackCodes = unpacked,
                            UnpackedCount = unpacked.Count,
                            IsDuplicate = false,
                            OperationTime = now
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

        private static bool RequestIdExists(SqlConnection connection, SqlTransaction transaction, string requestId)
        {
            const string sql = @"SELECT COUNT(1) FROM dbo.WMS_FG_HU_OperationLog WITH (UPDLOCK,HOLDLOCK) WHERE RequestId=@RequestId;";
            using (SqlCommand command = new SqlCommand(sql, connection, transaction))
            {
                command.Parameters.Add("@RequestId", SqlDbType.VarChar, 100).Value = requestId;
                return Convert.ToInt32(command.ExecuteScalar()) > 0;
            }
        }

        private static SourceRow ReadSource(SqlConnection connection, SqlTransaction transaction, string boxHuCode)
        {
            const string sql = @"
SELECT box.Id,box.HUCode,box.HUType,box.PlantCode,box.LifecycleStatus,box.IsHold,
       cl.LocationId,loc.LocationCode,w.WarehouseCode,z.ZoneCode
FROM dbo.WMS_FG_HU box WITH (UPDLOCK,HOLDLOCK)
LEFT JOIN dbo.WMS_FG_HU_CurrentLocation cl WITH (UPDLOCK,HOLDLOCK) ON cl.HUId=box.Id
LEFT JOIN dbo.WMS_FG_Location loc ON loc.Id=cl.LocationId
LEFT JOIN dbo.WMS_FG_Warehouse w ON w.Id=loc.WarehouseId
LEFT JOIN dbo.WMS_FG_Zone z ON z.Id=loc.ZoneId
WHERE box.HUCode=@BoxHuCode;";
            using (SqlCommand command = new SqlCommand(sql, connection, transaction))
            {
                command.Parameters.Add("@BoxHuCode", SqlDbType.VarChar, 100).Value = boxHuCode;
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read()) return null;
                    return new SourceRow
                    {
                        Id = Convert.ToInt64(reader["Id"]), HuCode = Convert.ToString(reader["HUCode"]), HuType = Convert.ToString(reader["HUType"]),
                        PlantCode = Convert.ToString(reader["PlantCode"]), LifecycleStatus = Convert.ToString(reader["LifecycleStatus"]), IsHold = Convert.ToBoolean(reader["IsHold"]),
                        LocationId = DbLong(reader, "LocationId"), LocationCode = DbString(reader, "LocationCode"), WarehouseCode = DbString(reader, "WarehouseCode"), ZoneCode = DbString(reader, "ZoneCode")
                    };
                }
            }
        }

        private static string ValidateSource(SourceRow source, string warehouseCode)
        {
            if (source == null) return "外箱不存在。";
            if (source.HuType != "OUTER_BOX") return "当前HU不是OUTER_BOX。";
            if (source.LifecycleStatus != "ACTIVE") return "只有ACTIVE状态外箱允许整箱拆包。";
            if (source.IsHold) return "外箱当前已被冻结。";
            if (!source.LocationId.HasValue || source.ZoneCode != "STORAGE") return "外箱必须位于实体STORAGE货位。";
            if (!string.Equals(source.WarehouseCode, warehouseCode, StringComparison.OrdinalIgnoreCase)) return "外箱不属于指定仓库。";
            return null;
        }

        private static TargetRow ReadTarget(SqlConnection connection, SqlTransaction transaction, string warehouseCode, string locationCode)
        {
            const string sql = @"
SELECT l.Id,l.LocationCode,l.IsActive,l.IsVirtual,l.PlantPolicy,l.DedicatedPlantCode,z.ZoneCode
FROM dbo.WMS_FG_Location l WITH (UPDLOCK,HOLDLOCK)
INNER JOIN dbo.WMS_FG_Warehouse w ON w.Id=l.WarehouseId
INNER JOIN dbo.WMS_FG_Zone z ON z.Id=l.ZoneId
WHERE w.WarehouseCode=@WarehouseCode AND l.LocationCode=@LocationCode;";
            using (SqlCommand command = new SqlCommand(sql, connection, transaction))
            {
                command.Parameters.Add("@WarehouseCode", SqlDbType.VarChar, 30).Value = warehouseCode;
                command.Parameters.Add("@LocationCode", SqlDbType.VarChar, 50).Value = locationCode;
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read()) return null;
                    return new TargetRow
                    {
                        Id = Convert.ToInt64(reader["Id"]), LocationCode = Convert.ToString(reader["LocationCode"]),
                        IsActive = Convert.ToBoolean(reader["IsActive"]), IsVirtual = Convert.ToBoolean(reader["IsVirtual"]),
                        PlantPolicy = Convert.ToString(reader["PlantPolicy"]), DedicatedPlantCode = DbString(reader, "DedicatedPlantCode"), ZoneCode = Convert.ToString(reader["ZoneCode"])
                    };
                }
            }
        }

        private static string ValidateTarget(SqlConnection connection, SqlTransaction transaction, TargetRow target, string warehouseCode, string plantCode)
        {
            if (target == null) return "目标货位不存在。";
            if (!target.IsActive || target.IsVirtual || target.ZoneCode != "STORAGE") return "拆出的内包必须进入当前仓库可用的实体STORAGE货位。";
            if (target.PlantPolicy == "DEDICATED" && !string.Equals(target.DedicatedPlantCode, plantCode, StringComparison.OrdinalIgnoreCase)) return "目标货位属于其他厂别专用货位。";
            const string plantSql = @"SELECT COUNT(1) FROM dbo.WMS_FG_WarehousePlant wp INNER JOIN dbo.WMS_FG_Warehouse w ON w.Id=wp.WarehouseId WHERE w.WarehouseCode=@WarehouseCode AND wp.PlantCode=@PlantCode AND wp.IsActive=1 AND w.IsActive=1;";
            using (SqlCommand plantCommand = new SqlCommand(plantSql, connection, transaction))
            {
                plantCommand.Parameters.Add("@WarehouseCode", SqlDbType.VarChar, 30).Value = warehouseCode;
                plantCommand.Parameters.Add("@PlantCode", SqlDbType.VarChar, 20).Value = plantCode;
                if (Convert.ToInt32(plantCommand.ExecuteScalar()) == 0) return "目标仓库不允许存储该厂别库存。";
            }
            if (target.PlantPolicy == "SAME_PLANT")
            {
                const string samePlantSql = @"SELECT COUNT(1) FROM dbo.WMS_FG_HU_CurrentLocation cl INNER JOIN dbo.WMS_FG_HU h ON h.Id=cl.HUId WHERE cl.LocationId=@LocationId AND h.PlantCode<>@PlantCode AND h.LifecycleStatus='ACTIVE';";
                using (SqlCommand command = new SqlCommand(samePlantSql, connection, transaction))
                {
                    command.Parameters.Add("@LocationId", SqlDbType.BigInt).Value = target.Id;
                    command.Parameters.Add("@PlantCode", SqlDbType.VarChar, 20).Value = plantCode;
                    if (Convert.ToInt32(command.ExecuteScalar()) > 0) return "目标货位存在其他厂别库存。";
                }
            }
            return null;
        }

        private static List<ChildRow> ReadChildren(SqlConnection connection, SqlTransaction transaction, long boxHuId)
        {
            const string sql = @"
SELECT r.Id AS RelationId,innerHU.Id,innerHU.HUCode,innerHU.HUType,innerHU.PlantCode,
       innerHU.LifecycleStatus,innerHU.IsHold,CAST(CASE WHEN cl.HUId IS NULL THEN 0 ELSE 1 END AS bit) AS HasDirectLocation
FROM dbo.WMS_FG_HU_Relation r WITH (UPDLOCK,HOLDLOCK)
INNER JOIN dbo.WMS_FG_HU innerHU WITH (UPDLOCK,HOLDLOCK) ON innerHU.Id=r.ChildHUId
LEFT JOIN dbo.WMS_FG_HU_CurrentLocation cl WITH (UPDLOCK,HOLDLOCK) ON cl.HUId=innerHU.Id
WHERE r.ParentHUId=@BoxHuId AND r.IsActive=1
ORDER BY innerHU.HUCode;";
            List<ChildRow> result = new List<ChildRow>();
            using (SqlCommand command = new SqlCommand(sql, connection, transaction))
            {
                command.Parameters.Add("@BoxHuId", SqlDbType.BigInt).Value = boxHuId;
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read()) result.Add(new ChildRow
                    {
                        RelationId = Convert.ToInt64(reader["RelationId"]), Id = Convert.ToInt64(reader["Id"]), HuCode = Convert.ToString(reader["HUCode"]),
                        HuType = Convert.ToString(reader["HUType"]), PlantCode = Convert.ToString(reader["PlantCode"]), LifecycleStatus = Convert.ToString(reader["LifecycleStatus"]),
                        IsHold = Convert.ToBoolean(reader["IsHold"]), HasDirectLocation = Convert.ToBoolean(reader["HasDirectLocation"])
                    });
                }
            }
            return result;
        }

        private static string ValidateChildren(List<ChildRow> children, string plantCode)
        {
            if (children.Count == 0) return "外箱当前没有可拆出的有效内包。";
            if (children.Any(x => x.HuType != "INNER_PACK" || x.LifecycleStatus != "ACTIVE")) return "外箱存在类型或生命周期不符合要求的有效子HU。";
            if (children.Any(x => x.IsHold)) return "外箱存在冻结内包，整箱拆包已取消。";
            if (children.Any(x => x.HasDirectLocation)) return "外箱存在已有直接货位的内包，整箱拆包已取消。";
            if (children.Any(x => !string.Equals(x.PlantCode, plantCode, StringComparison.OrdinalIgnoreCase))) return "外箱与内包厂别不一致。";
            return null;
        }

        private static bool CloseRelation(SqlConnection connection, SqlTransaction transaction, long relationId, string operationNo, string operatorCode, DateTime now)
        {
            const string sql = @"UPDATE dbo.WMS_FG_HU_Relation SET EffectiveTo=@Now,IsActive=0,CloseOperationNo=@OperationNo,CloseUser=@Operator,CloseReason=N'UNPACK_INNER' WHERE Id=@RelationId AND IsActive=1;";
            using (SqlCommand command = new SqlCommand(sql, connection, transaction))
            {
                command.Parameters.Add("@Now", SqlDbType.DateTime2).Value = now;
                command.Parameters.Add("@OperationNo", SqlDbType.VarChar, 50).Value = operationNo;
                command.Parameters.Add("@Operator", SqlDbType.VarChar, 50).Value = DbParameterValue.StringOrDbNull(operatorCode);
                command.Parameters.Add("@RelationId", SqlDbType.BigInt).Value = relationId;
                return command.ExecuteNonQuery() == 1;
            }
        }

        private static void InsertCurrentLocation(SqlConnection connection, SqlTransaction transaction, long huId, long locationId, long moveLogId, string moveNo, string operatorCode, DateTime now)
        {
            const string sql = @"INSERT INTO dbo.WMS_FG_HU_CurrentLocation(HUId,LocationId,LocationSince,LastMoveLogId,CreateBy) VALUES(@HUId,@LocationId,@Now,@MoveLogId,@Operator);";
            using (SqlCommand command = new SqlCommand(sql, connection, transaction))
            {
                command.Parameters.Add("@HUId", SqlDbType.BigInt).Value = huId;
                command.Parameters.Add("@LocationId", SqlDbType.BigInt).Value = locationId;
                command.Parameters.Add("@Now", SqlDbType.DateTime2).Value = now;
                command.Parameters.Add("@MoveLogId", SqlDbType.BigInt).Value = moveLogId;
                command.Parameters.Add("@MoveNo", SqlDbType.VarChar, 50).Value = moveNo;
                command.Parameters.Add("@Operator", SqlDbType.VarChar, 50).Value = DbParameterValue.StringOrDbNull(operatorCode);
                command.ExecuteNonQuery();
            }
        }

        private static string DbString(SqlDataReader reader, string name)
        {
            int index = reader.GetOrdinal(name);
            return reader.IsDBNull(index) ? null : Convert.ToString(reader.GetValue(index));
        }

        private static long? DbLong(SqlDataReader reader, string name)
        {
            int index = reader.GetOrdinal(name);
            return reader.IsDBNull(index) ? (long?)null : Convert.ToInt64(reader.GetValue(index));
        }

        private class HeaderRow { public string OperationNo { get; set; } public string BoxHuCode { get; set; } public DateTime OperationTime { get; set; } }
        private class ItemRow { public string InnerPackCode { get; set; } public string FromLocationCode { get; set; } public string ToLocationCode { get; set; } }
        private class SourceRow { public long Id { get; set; } public string HuCode { get; set; } public string HuType { get; set; } public string PlantCode { get; set; } public string LifecycleStatus { get; set; } public bool IsHold { get; set; } public long? LocationId { get; set; } public string LocationCode { get; set; } public string WarehouseCode { get; set; } public string ZoneCode { get; set; } }
        private class TargetRow { public long Id { get; set; } public string LocationCode { get; set; } public bool IsActive { get; set; } public bool IsVirtual { get; set; } public string PlantPolicy { get; set; } public string DedicatedPlantCode { get; set; } public string ZoneCode { get; set; } }
        private class ChildRow { public long RelationId { get; set; } public long Id { get; set; } public string HuCode { get; set; } public string HuType { get; set; } public string PlantCode { get; set; } public string LifecycleStatus { get; set; } public bool IsHold { get; set; } public bool HasDirectLocation { get; set; } }
    }
}
