using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using WMS_HU_Model.Infrastructure.Database;
using WMS_HU_Model.Models.Repack;
using WMS_HU_Model.Rules;

namespace WMS_HU_Model.Repositories
{
    internal class MergeBoxRepository
    {
        public MergeBoxResultDto GetByRequestId(string requestId)
        {
            if (string.IsNullOrWhiteSpace(requestId)) return null;
            using (SqlConnection connection = DbConnectionFactory.Create())
            using (SqlCommand command = new SqlCommand(@"
SELECT TOP 1 DetailJson
FROM dbo.WMS_FG_HU_OperationLog
WHERE RequestId=@RequestId AND OperationType='MERGE_BOX'
ORDER BY Id;", connection))
            {
                command.Parameters.Add("@RequestId", SqlDbType.VarChar, 100).Value = requestId;
                connection.Open();
                object value = command.ExecuteScalar();
                return value == null || value == DBNull.Value ? null : ReadSnapshot(Convert.ToString(value));
            }
        }

        public MergeBoxPersistenceResult Execute(MergeBoxCommand command)
        {
            using (SqlConnection connection = DbConnectionFactory.Create())
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        string commandError = ValidateCommand(command);
                        if (commandError != null) return Rollback(transaction, commandError);

                        ExistingRequest existing = ReadExistingRequest(connection, transaction, command.RequestId);
                        if (existing.Exists)
                        {
                            if (existing.Result == null) return Rollback(transaction, "RequestId已经被其他WMS业务使用。");
                            if (!SameRequest(existing.Result, command)) return Rollback(transaction, "RequestId已经被其他合箱业务使用。");
                            existing.Result.IsDuplicate = true;
                            transaction.Rollback();
                            return MergeBoxPersistenceResult.Success(existing.Result);
                        }

                        List<MergeBoxPackFact> packs = ReadPacks(connection, transaction, command.InnerPackHuCodes);
                        string packError = MergeBoxRules.Validate(command.InnerPackHuCodes, packs, command.WarehouseCode);
                        if (packError != null) return Rollback(transaction, packError);

                        MergeBoxPackFact first = packs.First(x => string.Equals(x.HuCode, command.InnerPackHuCodes[0], StringComparison.OrdinalIgnoreCase));
                        TargetLocation target = ReadTarget(connection, transaction, command.WarehouseCode);
                        string targetError = ValidateTarget(connection, transaction, target, command.WarehouseCode, first.PlantCode);
                        if (targetError != null) return Rollback(transaction, targetError);

                        DateTime now = DateTime.Now;
                        string allocationError;
                        int? sequenceNo = AllocateSequence(connection, transaction, now.Date, first.PlantCode, command.Operator, now, out allocationError);
                        if (!sequenceNo.HasValue) return Rollback(transaction, allocationError);

                        string newBoxCode = CanonicalOuterBoxCode.Build(now.Date, first.PlantCode, sequenceNo.Value);
                        long newBoxId = InsertBox(connection, transaction, newBoxCode, first.PlantCode, command, now);
                        InsertNewBoxLocation(connection, transaction, newBoxId, target.Id, command, now);
                        HuOperationLogWriter.Insert(connection, transaction, command.OperationNo, "CREATE_HU", newBoxId, null,
                            command.BusinessNo, command.RequestId, command.Operator, now, null, null, "创建外箱：合箱外箱创建完成");

                        Dictionary<string, MergeBoxPackFact> byCode = packs.ToDictionary(x => x.HuCode, StringComparer.OrdinalIgnoreCase);
                        for (int index = 0; index < command.InnerPackHuCodes.Count; index++)
                        {
                            MergeBoxPackFact pack = byCode[command.InnerPackHuCodes[index]];
                            string itemOperationNo = command.OperationNo + "-" + (index + 1).ToString("000");
                            string itemRequestId = command.RequestId + ":" + (index + 1).ToString("000");
                            InsertRelation(connection, transaction, newBoxId, pack.HuId, itemOperationNo, command.Operator, now);
                            HuMoveLogWriter.Insert(connection, transaction, itemOperationNo, pack.HuId, pack.LocationId.Value, target.Id,
                                "PACK_INNER", null, command.BusinessNo, null, itemRequestId, "MERGE_BOX", command.Operator, now, command.Remark);
                            if (!DeleteDirectLocation(connection, transaction, pack.HuId, pack.LocationId.Value))
                                return Rollback(transaction, "内包[" + pack.HuCode + "]直接货位已变化，合箱已取消。");
                            HuOperationLogWriter.Insert(connection, transaction, itemOperationNo, "ATTACH_HU", newBoxId, pack.HuId,
                                command.BusinessNo, command.RequestId, command.Operator, now, null, null, "合箱绑定：建立外箱与内包关系");
                            HuOperationLogWriter.Insert(connection, transaction, itemOperationNo, "PACK_INNER", pack.HuId, newBoxId,
                                command.BusinessNo, command.RequestId, command.Operator, now, null, command.Remark);
                        }

                        MergeBoxResultDto result = new MergeBoxResultDto
                        {
                            NewBoxHuCode = newBoxCode,
                            PlantCode = first.PlantCode,
                            WarehouseCode = command.WarehouseCode,
                            MaterialCode = first.MaterialCode,
                            MaterialRevision = first.MaterialRevision,
                            InnerPackCount = command.InnerPackHuCodes.Count,
                            TotalQty = packs.Sum(x => x.TotalQty),
                            InnerPackHuCodes = new List<string>(command.InnerPackHuCodes),
                            LocationCode = target.LocationCode,
                            ZoneCode = target.ZoneCode,
                            IsDuplicate = false
                        };
                        string detailJson = JsonConvert.SerializeObject(result);
                        HuOperationLogWriter.Insert(connection, transaction, command.OperationNo, "MERGE_BOX", newBoxId, null,
                            command.BusinessNo, command.RequestId, command.Operator, now, detailJson, command.Remark);

                        transaction.Commit();
                        return MergeBoxPersistenceResult.Success(result);
                    }
                    catch
                    {
                        if (transaction.Connection != null) transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        private static string ValidateCommand(MergeBoxCommand command)
        {
            if (command == null) return "请求不能为空。";
            if (string.IsNullOrWhiteSpace(command.WarehouseCode)) return "WarehouseCode不能为空。";
            if (command.InnerPackHuCodes == null || command.InnerPackHuCodes.Count == 0) return "InnerPackHuCodes不能为空。";
            if (command.InnerPackHuCodes.Any(string.IsNullOrWhiteSpace)) return "InnerPackHuCodes不能包含空值。";
            if (command.InnerPackHuCodes.Distinct(StringComparer.OrdinalIgnoreCase).Count() != command.InnerPackHuCodes.Count) return "InnerPackHuCodes存在重复内包。";
            if (string.IsNullOrWhiteSpace(command.RequestId)) return "RequestId不能为空。";
            if (string.IsNullOrWhiteSpace(command.Operator)) return "Operator不能为空。";
            return null;
        }

        private static bool SameRequest(MergeBoxResultDto result, MergeBoxCommand command)
        {
            return string.Equals(result.WarehouseCode, command.WarehouseCode, StringComparison.OrdinalIgnoreCase)
                && result.InnerPackHuCodes.Count == command.InnerPackHuCodes.Count
                && !result.InnerPackHuCodes.Except(command.InnerPackHuCodes, StringComparer.OrdinalIgnoreCase).Any();
        }

        private static ExistingRequest ReadExistingRequest(SqlConnection connection, SqlTransaction transaction, string requestId)
        {
            const string sql = @"
SELECT OperationType,DetailJson
FROM dbo.WMS_FG_HU_OperationLog WITH (UPDLOCK,HOLDLOCK)
WHERE RequestId=@RequestId
ORDER BY Id;";
            bool exists = false;
            MergeBoxResultDto result = null;
            using (SqlCommand command = new SqlCommand(sql, connection, transaction))
            {
                command.Parameters.Add("@RequestId", SqlDbType.VarChar, 100).Value = requestId;
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        exists = true;
                        if (Convert.ToString(reader["OperationType"]) == "MERGE_BOX" && !reader.IsDBNull(reader.GetOrdinal("DetailJson")))
                            result = ReadSnapshot(Convert.ToString(reader["DetailJson"]));
                    }
                }
            }
            return new ExistingRequest { Exists = exists, Result = result };
        }

        private static MergeBoxResultDto ReadSnapshot(string json)
        {
            MergeBoxResultDto result;
            try
            {
                result = JsonConvert.DeserializeObject<MergeBoxResultDto>(json);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException("MERGE_BOX_IDEMPOTENCY_DATA_INVALID: 合箱幂等快照无法读取。", ex);
            }
            if (result == null || string.IsNullOrWhiteSpace(result.NewBoxHuCode) || result.InnerPackHuCodes == null)
                throw new InvalidOperationException("MERGE_BOX_IDEMPOTENCY_DATA_INVALID: 合箱幂等快照不完整。");
            return result;
        }

        private static List<MergeBoxPackFact> ReadPacks(SqlConnection connection, SqlTransaction transaction, IList<string> huCodes)
        {
            StringBuilder sql = new StringBuilder(@"
SELECT h.Id,h.HUCode,h.HUType,h.PlantCode,h.LifecycleStatus,h.IsHold,
       CAST(CASE WHEN EXISTS(SELECT 1 FROM dbo.WMS_FG_HU_Relation ar WITH (UPDLOCK,HOLDLOCK) WHERE ar.ChildHUId=h.Id AND ar.IsActive=1) THEN 1 ELSE 0 END AS bit) AS HasActiveParent,
       cl.LocationId,l.LocationCode,l.IsActive AS LocationIsActive,l.IsVirtual,
       w.WarehouseCode,w.IsActive AS WarehouseIsActive,
       z.ZoneCode,z.IsActive AS ZoneIsActive,
       itemFacts.ActiveItemCount,itemFacts.MaterialCodeCount,itemFacts.MaterialRevisionCount,
       itemFacts.MaterialCode,itemFacts.MaterialRevision,itemFacts.TotalQty
FROM dbo.WMS_FG_HU h WITH (UPDLOCK,HOLDLOCK)
LEFT JOIN dbo.WMS_FG_HU_CurrentLocation cl WITH (UPDLOCK,HOLDLOCK) ON cl.HUId=h.Id
LEFT JOIN dbo.WMS_FG_Location l WITH (UPDLOCK,HOLDLOCK) ON l.Id=cl.LocationId
LEFT JOIN dbo.WMS_FG_Warehouse w WITH (UPDLOCK,HOLDLOCK) ON w.Id=l.WarehouseId
LEFT JOIN dbo.WMS_FG_Zone z WITH (UPDLOCK,HOLDLOCK) ON z.Id=l.ZoneId
OUTER APPLY
(
    SELECT COUNT(1) AS ActiveItemCount,
           COUNT(DISTINCT item.ProdNo) AS MaterialCodeCount,
           COUNT(DISTINCT ISNULL(item.ProdRev,'<NULL>')) AS MaterialRevisionCount,
           MIN(item.ProdNo) AS MaterialCode,
           MIN(item.ProdRev) AS MaterialRevision,
           ISNULL(SUM(item.Qty),0) AS TotalQty
    FROM dbo.WMS_FG_HU_Item item WITH (UPDLOCK,HOLDLOCK)
    WHERE item.HUId=h.Id AND item.IsActive=1
) itemFacts
WHERE h.HUCode IN (");
            for (int index = 0; index < huCodes.Count; index++)
            {
                if (index > 0) sql.Append(",");
                sql.Append("@HuCode").Append(index);
            }
            sql.Append(") ORDER BY h.HUCode;");

            List<MergeBoxPackFact> result = new List<MergeBoxPackFact>();
            using (SqlCommand command = new SqlCommand(sql.ToString(), connection, transaction))
            {
                for (int index = 0; index < huCodes.Count; index++)
                    command.Parameters.Add("@HuCode" + index, SqlDbType.VarChar, 100).Value = huCodes[index];
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read()) result.Add(new MergeBoxPackFact
                    {
                        HuId = Convert.ToInt64(reader["Id"]), HuCode = Convert.ToString(reader["HUCode"]), HuType = Convert.ToString(reader["HUType"]),
                        PlantCode = Convert.ToString(reader["PlantCode"]), LifecycleStatus = Convert.ToString(reader["LifecycleStatus"]), IsHold = Convert.ToBoolean(reader["IsHold"]),
                        HasActiveParent = Convert.ToBoolean(reader["HasActiveParent"]), LocationId = NullableLong(reader, "LocationId"), LocationCode = NullableString(reader, "LocationCode"),
                        WarehouseCode = NullableString(reader, "WarehouseCode"), WarehouseIsActive = NullableBoolean(reader, "WarehouseIsActive"), ZoneCode = NullableString(reader, "ZoneCode"),
                        ZoneIsActive = NullableBoolean(reader, "ZoneIsActive"), LocationIsActive = NullableBoolean(reader, "LocationIsActive"), IsVirtual = NullableBoolean(reader, "IsVirtual"),
                        ActiveItemCount = Convert.ToInt32(reader["ActiveItemCount"]), MaterialCodeCount = Convert.ToInt32(reader["MaterialCodeCount"]),
                        MaterialRevisionCount = Convert.ToInt32(reader["MaterialRevisionCount"]), MaterialCode = NullableString(reader, "MaterialCode"),
                        MaterialRevision = NullableString(reader, "MaterialRevision"), TotalQty = Convert.ToDecimal(reader["TotalQty"])
                    });
                }
            }
            return result;
        }

        private static TargetLocation ReadTarget(SqlConnection connection, SqlTransaction transaction, string warehouseCode)
        {
            const string sql = @"
SELECT l.Id,l.LocationCode,l.IsActive,l.IsVirtual,l.PlantPolicy,l.DedicatedPlantCode,
       z.ZoneCode,z.IsActive AS ZoneIsActive,w.IsActive AS WarehouseIsActive
FROM dbo.WMS_FG_Location l WITH (UPDLOCK,HOLDLOCK)
INNER JOIN dbo.WMS_FG_Warehouse w WITH (UPDLOCK,HOLDLOCK) ON w.Id=l.WarehouseId
INNER JOIN dbo.WMS_FG_Zone z WITH (UPDLOCK,HOLDLOCK) ON z.Id=l.ZoneId
WHERE w.WarehouseCode=@WarehouseCode AND l.LocationCode='INBOUND-PENDING';";
            using (SqlCommand command = new SqlCommand(sql, connection, transaction))
            {
                command.Parameters.Add("@WarehouseCode", SqlDbType.VarChar, 30).Value = warehouseCode;
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read()) return null;
                    TargetLocation result = new TargetLocation
                    {
                        Id = Convert.ToInt64(reader["Id"]), LocationCode = Convert.ToString(reader["LocationCode"]), IsActive = Convert.ToBoolean(reader["IsActive"]),
                        IsVirtual = Convert.ToBoolean(reader["IsVirtual"]), PlantPolicy = Convert.ToString(reader["PlantPolicy"]), DedicatedPlantCode = NullableString(reader, "DedicatedPlantCode"),
                        ZoneCode = Convert.ToString(reader["ZoneCode"]), ZoneIsActive = Convert.ToBoolean(reader["ZoneIsActive"]), WarehouseIsActive = Convert.ToBoolean(reader["WarehouseIsActive"])
                    };
                    if (reader.Read()) result.IsAmbiguous = true;
                    return result;
                }
            }
        }

        private static string ValidateTarget(SqlConnection connection, SqlTransaction transaction, TargetLocation target, string warehouseCode, string plantCode)
        {
            if (target == null) return "未找到当前仓库的INBOUND-PENDING库位。";
            if (target.IsAmbiguous) return "当前仓库存在多个INBOUND-PENDING库位。";
            if (!target.WarehouseIsActive || !target.ZoneIsActive || !target.IsActive || !target.IsVirtual || target.ZoneCode != "RECEIVE")
                return "INBOUND-PENDING必须是当前仓库可用的RECEIVE虚拟库位。";
            if (target.PlantPolicy == "DEDICATED" && !string.Equals(target.DedicatedPlantCode, plantCode, StringComparison.OrdinalIgnoreCase))
                return "INBOUND-PENDING属于其他厂别专用库位。";

            const string warehousePlantSql = @"
SELECT COUNT(1)
FROM dbo.WMS_FG_WarehousePlant wp WITH (UPDLOCK,HOLDLOCK)
INNER JOIN dbo.WMS_FG_Warehouse w WITH (UPDLOCK,HOLDLOCK) ON w.Id=wp.WarehouseId
WHERE w.WarehouseCode=@WarehouseCode AND w.IsActive=1 AND wp.PlantCode=@PlantCode AND wp.IsActive=1;";
            using (SqlCommand command = new SqlCommand(warehousePlantSql, connection, transaction))
            {
                command.Parameters.Add("@WarehouseCode", SqlDbType.VarChar, 30).Value = warehouseCode;
                command.Parameters.Add("@PlantCode", SqlDbType.VarChar, 20).Value = plantCode;
                if (Convert.ToInt32(command.ExecuteScalar()) != 1) return "目标仓库不允许存储该厂别库存。";
            }

            if (target.PlantPolicy == "SAME_PLANT")
            {
                const string samePlantSql = @"
SELECT COUNT(1)
FROM dbo.WMS_FG_HU_CurrentLocation cl WITH (UPDLOCK,HOLDLOCK)
INNER JOIN dbo.WMS_FG_HU h WITH (UPDLOCK,HOLDLOCK) ON h.Id=cl.HUId
WHERE cl.LocationId=@LocationId AND h.LifecycleStatus='ACTIVE' AND h.PlantCode<>@PlantCode;";
                using (SqlCommand command = new SqlCommand(samePlantSql, connection, transaction))
                {
                    command.Parameters.Add("@LocationId", SqlDbType.BigInt).Value = target.Id;
                    command.Parameters.Add("@PlantCode", SqlDbType.VarChar, 20).Value = plantCode;
                    if (Convert.ToInt32(command.ExecuteScalar()) > 0) return "INBOUND-PENDING存在其他厂别库存。";
                }
            }
            return null;
        }

        private static int? AllocateSequence(SqlConnection connection, SqlTransaction transaction, DateTime businessDate, string plantCode, string operatorCode, DateTime now, out string error)
        {
            error = null;
            string sequenceKey = CanonicalOuterBoxCode.BuildSequenceKey(businessDate, plantCode);
            const string selectSql = @"SELECT CurrentValue FROM dbo.WMS_FG_BoxCodeSequence WITH (UPDLOCK,HOLDLOCK) WHERE SequenceKey=@SequenceKey;";
            int? current;
            using (SqlCommand command = new SqlCommand(selectSql, connection, transaction))
            {
                command.Parameters.Add("@SequenceKey", SqlDbType.VarChar, 100).Value = sequenceKey;
                object value = command.ExecuteScalar();
                current = value == null || value == DBNull.Value ? (int?)null : Convert.ToInt32(value);
            }
            if (!current.HasValue)
            {
                const string insertSql = @"INSERT INTO dbo.WMS_FG_BoxCodeSequence(SequenceKey,CurrentValue,CreateDt,CreateBy) VALUES(@SequenceKey,1,@Now,@Operator);";
                using (SqlCommand command = new SqlCommand(insertSql, connection, transaction))
                {
                    command.Parameters.Add("@SequenceKey", SqlDbType.VarChar, 100).Value = sequenceKey;
                    command.Parameters.Add("@Now", SqlDbType.DateTime2).Value = now;
                    command.Parameters.Add("@Operator", SqlDbType.VarChar, 50).Value = DbParameterValue.StringOrDbNull(operatorCode);
                    command.ExecuteNonQuery();
                }
                return 1;
            }
            if (current.Value >= 9999)
            {
                error = "BOX_CODE_DAILY_SEQUENCE_EXHAUSTED: 当前厂别当日OUTER_BOX标准箱码已用尽。";
                return null;
            }
            const string updateSql = @"UPDATE dbo.WMS_FG_BoxCodeSequence SET CurrentValue=CurrentValue+1,LastEditDt=@Now,LastEditBy=@Operator WHERE SequenceKey=@SequenceKey AND CurrentValue=@CurrentValue;";
            using (SqlCommand command = new SqlCommand(updateSql, connection, transaction))
            {
                command.Parameters.Add("@Now", SqlDbType.DateTime2).Value = now;
                command.Parameters.Add("@Operator", SqlDbType.VarChar, 50).Value = DbParameterValue.StringOrDbNull(operatorCode);
                command.Parameters.Add("@SequenceKey", SqlDbType.VarChar, 100).Value = sequenceKey;
                command.Parameters.Add("@CurrentValue", SqlDbType.Int).Value = current.Value;
                if (command.ExecuteNonQuery() != 1) throw new InvalidOperationException("OUTER_BOX标准箱码序列并发更新失败。");
            }
            return current.Value + 1;
        }

        private static long InsertBox(SqlConnection connection, SqlTransaction transaction, string huCode, string plantCode, MergeBoxCommand command, DateTime now)
        {
            const string sql = @"
INSERT INTO dbo.WMS_FG_HU(HUCode,HUType,PlantCode,LifecycleStatus,IsHold,SourceSystem,SourceEntityId,SourceBusinessNo,Remark,CreateBy,CreateDt)
OUTPUT INSERTED.Id
VALUES(@HUCode,'OUTER_BOX',@PlantCode,'ACTIVE',0,'WMS',@SourceEntityId,@BusinessNo,@Remark,@Operator,@Now);";
            using (SqlCommand sqlCommand = new SqlCommand(sql, connection, transaction))
            {
                sqlCommand.Parameters.Add("@HUCode", SqlDbType.VarChar, 100).Value = huCode;
                sqlCommand.Parameters.Add("@PlantCode", SqlDbType.VarChar, 20).Value = plantCode;
                sqlCommand.Parameters.Add("@SourceEntityId", SqlDbType.VarChar, 100).Value = command.OperationNo;
                sqlCommand.Parameters.Add("@BusinessNo", SqlDbType.VarChar, 100).Value = DbParameterValue.StringOrDbNull(command.BusinessNo);
                sqlCommand.Parameters.Add("@Remark", SqlDbType.NVarChar, 1000).Value = DbParameterValue.StringOrDbNull(command.Remark);
                sqlCommand.Parameters.Add("@Operator", SqlDbType.VarChar, 50).Value = DbParameterValue.StringOrDbNull(command.Operator);
                sqlCommand.Parameters.Add("@Now", SqlDbType.DateTime2).Value = now;
                return Convert.ToInt64(sqlCommand.ExecuteScalar());
            }
        }

        private static void InsertNewBoxLocation(SqlConnection connection, SqlTransaction transaction, long boxHuId, long locationId, MergeBoxCommand command, DateTime now)
        {
            string moveNo = command.OperationNo + "-BOX";
            long moveLogId = HuMoveLogWriter.Insert(connection, transaction, moveNo, boxHuId, null, locationId, "RECEIVE", null,
                command.BusinessNo, null, command.RequestId + ":BOX", "MERGE_BOX", command.Operator, now, command.Remark);
            const string sql = @"INSERT INTO dbo.WMS_FG_HU_CurrentLocation(HUId,LocationId,LocationSince,LastMoveLogId,CreateBy) VALUES(@HUId,@LocationId,@Now,@MoveLogId,@Operator);";
            using (SqlCommand sqlCommand = new SqlCommand(sql, connection, transaction))
            {
                sqlCommand.Parameters.Add("@HUId", SqlDbType.BigInt).Value = boxHuId;
                sqlCommand.Parameters.Add("@LocationId", SqlDbType.BigInt).Value = locationId;
                sqlCommand.Parameters.Add("@Now", SqlDbType.DateTime2).Value = now;
                sqlCommand.Parameters.Add("@MoveLogId", SqlDbType.BigInt).Value = moveLogId;
                sqlCommand.Parameters.Add("@MoveNo", SqlDbType.VarChar, 50).Value = moveNo;
                sqlCommand.Parameters.Add("@Operator", SqlDbType.VarChar, 50).Value = DbParameterValue.StringOrDbNull(command.Operator);
                sqlCommand.ExecuteNonQuery();
            }
        }

        private static void InsertRelation(SqlConnection connection, SqlTransaction transaction, long parentHuId, long childHuId, string operationNo, string operatorCode, DateTime now)
        {
            const string sql = @"INSERT INTO dbo.WMS_FG_HU_Relation(ParentHUId,ChildHUId,EffectiveFrom,IsActive,OperationNo,CreateBy) VALUES(@ParentHUId,@ChildHUId,@Now,1,@OperationNo,@Operator);";
            using (SqlCommand command = new SqlCommand(sql, connection, transaction))
            {
                command.Parameters.Add("@ParentHUId", SqlDbType.BigInt).Value = parentHuId;
                command.Parameters.Add("@ChildHUId", SqlDbType.BigInt).Value = childHuId;
                command.Parameters.Add("@Now", SqlDbType.DateTime2).Value = now;
                command.Parameters.Add("@OperationNo", SqlDbType.VarChar, 50).Value = operationNo;
                command.Parameters.Add("@Operator", SqlDbType.VarChar, 50).Value = DbParameterValue.StringOrDbNull(operatorCode);
                command.ExecuteNonQuery();
            }
        }

        private static bool DeleteDirectLocation(SqlConnection connection, SqlTransaction transaction, long huId, long locationId)
        {
            const string sql = @"DELETE FROM dbo.WMS_FG_HU_CurrentLocation WHERE HUId=@HUId AND LocationId=@LocationId;";
            using (SqlCommand command = new SqlCommand(sql, connection, transaction))
            {
                command.Parameters.Add("@HUId", SqlDbType.BigInt).Value = huId;
                command.Parameters.Add("@LocationId", SqlDbType.BigInt).Value = locationId;
                return command.ExecuteNonQuery() == 1;
            }
        }

        private static MergeBoxPersistenceResult Rollback(SqlTransaction transaction, string message)
        {
            if (transaction.Connection != null) transaction.Rollback();
            return MergeBoxPersistenceResult.Fail(message);
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

        private class ExistingRequest { public bool Exists { get; set; } public MergeBoxResultDto Result { get; set; } }
        private class TargetLocation
        {
            public long Id { get; set; }
            public string LocationCode { get; set; }
            public bool IsActive { get; set; }
            public bool IsVirtual { get; set; }
            public string PlantPolicy { get; set; }
            public string DedicatedPlantCode { get; set; }
            public string ZoneCode { get; set; }
            public bool ZoneIsActive { get; set; }
            public bool WarehouseIsActive { get; set; }
            public bool IsAmbiguous { get; set; }
        }
    }
}
