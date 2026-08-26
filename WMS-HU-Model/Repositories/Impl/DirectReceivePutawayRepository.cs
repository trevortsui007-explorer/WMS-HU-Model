using System;
using System.Data;
using System.Data.SqlClient;
using WMS_HU_Model.Infrastructure.Database;
using WMS_HU_Model.Models.Putaway;

namespace WMS_HU_Model.Repositories
{
    internal class DirectReceivePutawayRepository
    {
        public PutawayPersistenceResult Execute(DirectReceivePutawayCommand command)
        {
            using (SqlConnection connection = DbConnectionFactory.Create())
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        if (PutawayIdempotencyReader.RequestIdExists(connection, transaction, command.RequestId))
                        {
                            PutawayResultDto duplicate = PutawayIdempotencyReader.Read(connection, transaction, command.RequestId, true);
                            if (duplicate == null) return Rollback(transaction, "RequestId已经被其他WMS业务使用。");
                            if (!string.Equals(duplicate.BoxHuCode, command.BoxHuCode, StringComparison.OrdinalIgnoreCase)
                                || !string.Equals(duplicate.ToLocationCode, command.ToLocationCode, StringComparison.OrdinalIgnoreCase))
                                return Rollback(transaction, "RequestId对应的上架业务与当前请求不一致。");
                            transaction.Rollback();
                            duplicate.IsDuplicate = true;
                            return PutawayPersistenceResult.Success(duplicate);
                        }

                        DirectSourceRow source = ReadSource(connection, transaction, command.BoxHuId);
                        string sourceError = ValidateSource(source, command);
                        if (sourceError != null) return Rollback(transaction, sourceError);

                        DirectTargetRow target = ReadTarget(connection, transaction, command.ToLocationId);
                        string targetError = ValidateTarget(connection, transaction, target, command);
                        if (targetError != null) return Rollback(transaction, targetError);
                        if (source.LocationId == target.Id) return Rollback(transaction, "目标货位不能与外箱当前收货货位相同。");

                        DateTime now = DateTime.Now;
                        long moveLogId = HuMoveLogWriter.Insert(connection, transaction, command.OperationNo, command.BoxHuId,
                            source.LocationId.Value, target.Id, "PUTAWAY", command.ScannedLocationCode, command.BusinessNo, null,
                            command.RequestId, null, command.Operator, now, command.Remark);
                        if (!UpdateCurrentLocation(connection, transaction, command, target.Id, moveLogId, now))
                            return Rollback(transaction, "外箱当前位置已变化，请重新执行上架。");

                        HuOperationLogWriter.Insert(connection, transaction, command.OperationNo, "PUTAWAY", command.BoxHuId, null,
                            command.BusinessNo, command.RequestId, command.Operator, now, null, command.Remark);
                        transaction.Commit();
                        return PutawayPersistenceResult.Success(new PutawayResultDto
                        {
                            OperationNo = command.OperationNo,
                            MoveLogId = moveLogId,
                            SourceMode = "DIRECT_RECEIVE",
                            PalletHuCode = null,
                            BoxHuCode = command.BoxHuCode,
                            FromLocationCode = source.LocationCode,
                            ToLocationCode = target.LocationCode,
                            RemainingBoxCount = 0,
                            IsPalletEmpty = false,
                            IsPalletClosed = false,
                            IsDuplicate = false,
                            PutawayTime = now
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

        private static DirectSourceRow ReadSource(SqlConnection connection, SqlTransaction transaction, long boxHuId)
        {
            const string sql = @"
SELECT box.Id,box.HUCode,box.HUType,box.PlantCode,box.LifecycleStatus,box.IsHold,
       parent.Id AS ParentHuId,parent.HUType AS ParentHuType,
       cl.LocationId,loc.LocationCode,loc.MasterStatus,loc.IsVirtual,
       zone.ZoneCode,zone.IsActive AS ZoneIsActive,
       warehouse.WarehouseCode,warehouse.IsActive AS WarehouseIsActive
FROM dbo.WMS_HU box WITH (UPDLOCK,HOLDLOCK)
LEFT JOIN dbo.WMS_HU_Relation relation WITH (UPDLOCK,HOLDLOCK) ON relation.ChildHUId=box.Id AND relation.IsActive=1
LEFT JOIN dbo.WMS_HU parent WITH (UPDLOCK,HOLDLOCK) ON parent.Id=relation.ParentHUId
LEFT JOIN dbo.WMS_HU_CurrentLocation cl WITH (UPDLOCK,HOLDLOCK) ON cl.HUId=box.Id
LEFT JOIN dbo.WMS_Location loc WITH (UPDLOCK,HOLDLOCK) ON loc.Id=cl.LocationId
LEFT JOIN dbo.WMS_Zone zone WITH (UPDLOCK,HOLDLOCK) ON zone.Id=loc.ZoneId
LEFT JOIN dbo.WMS_Warehouse warehouse WITH (UPDLOCK,HOLDLOCK) ON warehouse.Id=loc.WarehouseId
WHERE box.Id=@BoxHuId;";
            using (SqlCommand command = new SqlCommand(sql, connection, transaction))
            {
                command.Parameters.Add("@BoxHuId", SqlDbType.BigInt).Value = boxHuId;
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read()) return null;
                    return new DirectSourceRow
                    {
                        Id = Convert.ToInt64(reader["Id"]), HuCode = Convert.ToString(reader["HUCode"]), HuType = Convert.ToString(reader["HUType"]),
                        PlantCode = Convert.ToString(reader["PlantCode"]), LifecycleStatus = Convert.ToString(reader["LifecycleStatus"]), IsHold = Convert.ToBoolean(reader["IsHold"]),
                        ParentHuId = NullableLong(reader, "ParentHuId"), ParentHuType = NullableString(reader, "ParentHuType"), LocationId = NullableLong(reader, "LocationId"),
                        LocationCode = NullableString(reader, "LocationCode"), MasterStatus = NullableString(reader, "MasterStatus"), IsVirtual = NullableBoolean(reader, "IsVirtual"),
                        ZoneCode = NullableString(reader, "ZoneCode"), ZoneIsActive = NullableBoolean(reader, "ZoneIsActive"),
                        WarehouseCode = NullableString(reader, "WarehouseCode"), WarehouseIsActive = NullableBoolean(reader, "WarehouseIsActive")
                    };
                }
            }
        }

        private static string ValidateSource(DirectSourceRow source, DirectReceivePutawayCommand command)
        {
            if (source == null) return "外箱HU不存在。";
            if (source.HuType != "OUTER_BOX") return "当前HU不是OUTER_BOX。";
            if (source.LifecycleStatus != "ACTIVE") return "只有ACTIVE状态外箱允许上架。";
            if (source.IsHold) return "外箱当前已冻结。";
            if (source.ParentHuId.HasValue) return "DIRECT_RECEIVE外箱不能存在有效父HU关系。";
            if (!source.LocationId.HasValue) return "外箱没有直接当前位置，库存位置状态不完整。";
            if (source.LocationId.Value != command.FromLocationId) return "外箱当前位置已变化，请重新执行上架。";
            if (source.MasterStatus != "ACTIVE" || !source.ZoneIsActive || !source.WarehouseIsActive) return "外箱当前收货货位不可用。";
            if (source.ZoneCode == "STORAGE") return "外箱已位于正式储存区，如需调整货位请使用移库作业。";
            if (source.ZoneCode != "RECEIVE") return "DIRECT_RECEIVE外箱必须位于有效RECEIVE收货区域。";
            if (!string.Equals(source.WarehouseCode, command.WarehouseCode, StringComparison.OrdinalIgnoreCase)) return "外箱当前收货货位不属于指定仓库。";
            if (!string.Equals(source.HuCode, command.BoxHuCode, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(source.PlantCode, command.PlantCode, StringComparison.OrdinalIgnoreCase)) return "外箱身份或厂别已变化。";
            return null;
        }

        private static DirectTargetRow ReadTarget(SqlConnection connection, SqlTransaction transaction, long locationId)
        {
            const string sql = @"
SELECT loc.Id,loc.LocationCode,loc.MasterStatus,loc.IsVirtual,loc.PlantPolicy,loc.DedicatedPlantCode,
       zone.ZoneCode,zone.IsActive AS ZoneIsActive,
       warehouse.WarehouseCode,warehouse.IsActive AS WarehouseIsActive
FROM dbo.WMS_Location loc WITH (UPDLOCK,HOLDLOCK)
INNER JOIN dbo.WMS_Zone zone WITH (UPDLOCK,HOLDLOCK) ON zone.Id=loc.ZoneId
INNER JOIN dbo.WMS_Warehouse warehouse WITH (UPDLOCK,HOLDLOCK) ON warehouse.Id=loc.WarehouseId
WHERE loc.Id=@LocationId;";
            using (SqlCommand command = new SqlCommand(sql, connection, transaction))
            {
                command.Parameters.Add("@LocationId", SqlDbType.BigInt).Value = locationId;
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read()) return null;
                    return new DirectTargetRow
                    {
                        Id = Convert.ToInt64(reader["Id"]), LocationCode = Convert.ToString(reader["LocationCode"]), MasterStatus = Convert.ToString(reader["MasterStatus"]),
                        IsVirtual = Convert.ToBoolean(reader["IsVirtual"]), PlantPolicy = Convert.ToString(reader["PlantPolicy"]), DedicatedPlantCode = NullableString(reader, "DedicatedPlantCode"),
                        ZoneCode = Convert.ToString(reader["ZoneCode"]), ZoneIsActive = Convert.ToBoolean(reader["ZoneIsActive"]),
                        WarehouseCode = Convert.ToString(reader["WarehouseCode"]), WarehouseIsActive = Convert.ToBoolean(reader["WarehouseIsActive"])
                    };
                }
            }
        }

        private static string ValidateTarget(SqlConnection connection, SqlTransaction transaction, DirectTargetRow target, DirectReceivePutawayCommand command)
        {
            if (target == null) return "目标货位不存在。";
            if (target.MasterStatus != "ACTIVE" || !target.ZoneIsActive || !target.WarehouseIsActive) return "目标货位不可用。";
            if (target.IsVirtual) return "正常上架不能使用虚拟货位。";
            if (target.ZoneCode != "STORAGE") return "正常上架目标必须属于STORAGE区域。";
            if (!string.Equals(target.WarehouseCode, command.WarehouseCode, StringComparison.OrdinalIgnoreCase)) return "目标货位不属于指定仓库。";
            if (target.PlantPolicy == "DEDICATED" && !string.Equals(target.DedicatedPlantCode, command.PlantCode, StringComparison.OrdinalIgnoreCase))
                return "目标货位为其他厂别专用货位。";

            const string warehousePlantSql = @"
SELECT COUNT(1)
FROM dbo.WMS_WarehousePlant mapping WITH (UPDLOCK,HOLDLOCK)
INNER JOIN dbo.WMS_Warehouse warehouse WITH (UPDLOCK,HOLDLOCK) ON warehouse.Id=mapping.WarehouseId
WHERE warehouse.WarehouseCode=@WarehouseCode AND warehouse.IsActive=1 AND mapping.PlantCode=@PlantCode AND mapping.IsActive=1;";
            using (SqlCommand sqlCommand = new SqlCommand(warehousePlantSql, connection, transaction))
            {
                sqlCommand.Parameters.Add("@WarehouseCode", SqlDbType.VarChar, 30).Value = command.WarehouseCode;
                sqlCommand.Parameters.Add("@PlantCode", SqlDbType.VarChar, 20).Value = command.PlantCode;
                if (Convert.ToInt32(sqlCommand.ExecuteScalar()) == 0) return "当前仓库不允许存储该厂别。";
            }

            if (target.PlantPolicy == "SAME_PLANT")
            {
                const string samePlantSql = @"
SELECT COUNT(1)
FROM dbo.WMS_HU_CurrentLocation currentLocation WITH (UPDLOCK,HOLDLOCK)
INNER JOIN dbo.WMS_HU hu WITH (UPDLOCK,HOLDLOCK) ON hu.Id=currentLocation.HUId
WHERE currentLocation.LocationId=@LocationId AND hu.PlantCode<>@PlantCode AND hu.LifecycleStatus='ACTIVE';";
                using (SqlCommand sqlCommand = new SqlCommand(samePlantSql, connection, transaction))
                {
                    sqlCommand.Parameters.Add("@LocationId", SqlDbType.BigInt).Value = target.Id;
                    sqlCommand.Parameters.Add("@PlantCode", SqlDbType.VarChar, 20).Value = command.PlantCode;
                    if (Convert.ToInt32(sqlCommand.ExecuteScalar()) > 0) return "目标货位当前存在其他厂别库存。";
                }
            }
            return null;
        }

        private static bool UpdateCurrentLocation(SqlConnection connection, SqlTransaction transaction, DirectReceivePutawayCommand command, long targetLocationId, long moveLogId, DateTime now)
        {
            const string sql = @"
UPDATE dbo.WMS_HU_CurrentLocation
SET LocationId=@TargetLocationId,LocationSince=@Now,LastMoveLogId=@MoveLogId,LastMoveNo=@MoveNo,
    LastMoveType='PUTAWAY',LastOperator=@Operator,ModifyTime=@Now
WHERE HUId=@BoxHuId AND LocationId=@SourceLocationId;";
            using (SqlCommand sqlCommand = new SqlCommand(sql, connection, transaction))
            {
                sqlCommand.Parameters.Add("@TargetLocationId", SqlDbType.BigInt).Value = targetLocationId;
                sqlCommand.Parameters.Add("@Now", SqlDbType.DateTime2).Value = now;
                sqlCommand.Parameters.Add("@MoveLogId", SqlDbType.BigInt).Value = moveLogId;
                sqlCommand.Parameters.Add("@MoveNo", SqlDbType.VarChar, 50).Value = command.OperationNo;
                sqlCommand.Parameters.Add("@Operator", SqlDbType.VarChar, 50).Value = DbParameterValue.StringOrDbNull(command.Operator);
                sqlCommand.Parameters.Add("@BoxHuId", SqlDbType.BigInt).Value = command.BoxHuId;
                sqlCommand.Parameters.Add("@SourceLocationId", SqlDbType.BigInt).Value = command.FromLocationId;
                return sqlCommand.ExecuteNonQuery() == 1;
            }
        }

        private static PutawayPersistenceResult Rollback(SqlTransaction transaction, string message)
        {
            if (transaction.Connection != null) transaction.Rollback();
            return PutawayPersistenceResult.Fail(message);
        }

        private static string NullableString(SqlDataReader reader, string name)
        {
            int ordinal = reader.GetOrdinal(name);
            return reader.IsDBNull(ordinal) ? null : Convert.ToString(reader.GetValue(ordinal));
        }

        private static long? NullableLong(SqlDataReader reader, string name)
        {
            int ordinal = reader.GetOrdinal(name);
            return reader.IsDBNull(ordinal) ? (long?)null : Convert.ToInt64(reader.GetValue(ordinal));
        }

        private static bool NullableBoolean(SqlDataReader reader, string name)
        {
            int ordinal = reader.GetOrdinal(name);
            return !reader.IsDBNull(ordinal) && Convert.ToBoolean(reader.GetValue(ordinal));
        }

        private class DirectSourceRow
        {
            public long Id { get; set; }
            public string HuCode { get; set; }
            public string HuType { get; set; }
            public string PlantCode { get; set; }
            public string LifecycleStatus { get; set; }
            public bool IsHold { get; set; }
            public long? ParentHuId { get; set; }
            public string ParentHuType { get; set; }
            public long? LocationId { get; set; }
            public string LocationCode { get; set; }
            public string MasterStatus { get; set; }
            public bool IsVirtual { get; set; }
            public string ZoneCode { get; set; }
            public bool ZoneIsActive { get; set; }
            public string WarehouseCode { get; set; }
            public bool WarehouseIsActive { get; set; }
        }

        private class DirectTargetRow
        {
            public long Id { get; set; }
            public string LocationCode { get; set; }
            public string MasterStatus { get; set; }
            public bool IsVirtual { get; set; }
            public string PlantPolicy { get; set; }
            public string DedicatedPlantCode { get; set; }
            public string ZoneCode { get; set; }
            public bool ZoneIsActive { get; set; }
            public string WarehouseCode { get; set; }
            public bool WarehouseIsActive { get; set; }
        }
    }
}
