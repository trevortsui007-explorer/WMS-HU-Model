using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using WMS_HU_Model.Infrastructure.Database;
using WMS_HU_Model.Models.MasterData;

namespace WMS_HU_Model.Repositories
{
    public class MasterDataMaintenanceRepository : IMasterDataMaintenanceRepository
    {
        public const string WarehouseQuerySql = @"
SELECT w.Id, w.WarehouseCode, w.WarehouseName, w.WarehouseType, w.IsActive, w.Remark,
    w.CreateDt AS CreateTime, w.LastEditBy AS ModifyUser, w.LastEditDt AS ModifyTime,
    (SELECT COUNT(1) FROM dbo.WMS_FG_Location l WHERE l.WarehouseId = w.Id AND l.IsActive = 1) AS ActiveLocationCount,
    (SELECT COUNT(1) FROM dbo.WMS_FG_HU_CurrentLocation currentLocation INNER JOIN dbo.WMS_FG_Location l ON l.Id = currentLocation.LocationId WHERE l.WarehouseId = w.Id) AS CurrentHuCount,
    (SELECT COUNT(1) FROM dbo.WMS_FG_WarehousePlant wp WHERE wp.WarehouseId = w.Id AND wp.IsActive = 1) AS ActiveMappingCount
FROM dbo.WMS_FG_Warehouse w
WHERE (@WarehouseCode IS NULL OR w.WarehouseCode LIKE '%' + @WarehouseCode + '%')
  AND (@WarehouseName IS NULL OR w.WarehouseName LIKE '%' + @WarehouseName + '%')
  AND (@IsActive IS NULL OR w.IsActive = @IsActive)
ORDER BY w.WarehouseCode;";

        public const string WarehousePlantQuerySql = @"
SELECT wp.Id, w.WarehouseCode, w.WarehouseName, wp.PlantCode,
    wp.SAPStorageLocation AS SapStorageLocation, wp.IsActive, wp.Remark, wp.CreateDt AS CreateTime
FROM dbo.WMS_FG_WarehousePlant wp
INNER JOIN dbo.WMS_FG_Warehouse w ON w.Id = wp.WarehouseId
WHERE (@WarehouseCode IS NULL OR w.WarehouseCode = @WarehouseCode)
  AND (@PlantCode IS NULL OR wp.PlantCode LIKE '%' + @PlantCode + '%')
  AND (@IsActive IS NULL OR wp.IsActive = @IsActive)
ORDER BY w.WarehouseCode, wp.PlantCode;";

        public List<WarehouseDto> QueryWarehouses(WarehouseQuery query)
        {
            List<WarehouseDto> result = new List<WarehouseDto>();
            using (SqlConnection connection = DbConnectionFactory.Create())
            using (SqlCommand command = new SqlCommand(WarehouseQuerySql, connection))
            {
                AddString(command, "@WarehouseCode", query.WarehouseCode, 20);
                AddString(command, "@WarehouseName", query.WarehouseName, 100, true);
                AddNullableBit(command, "@IsActive", query.IsActive);
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader()) while (reader.Read()) result.Add(ReadWarehouse(reader));
            }
            return result;
        }

        public WarehouseDto GetWarehouse(string warehouseCode)
        {
            WarehouseQuery query = new WarehouseQuery { WarehouseCode = warehouseCode };
            List<WarehouseDto> rows = QueryWarehouses(query);
            return rows.Find(x => string.Equals(x.WarehouseCode, warehouseCode, StringComparison.OrdinalIgnoreCase));
        }

        public bool WarehouseExists(string warehouseCode)
        {
            return ScalarCount("SELECT COUNT(1) FROM dbo.WMS_FG_Warehouse WHERE WarehouseCode = @Value;", warehouseCode) > 0;
        }

        public long InsertWarehouse(WarehouseCreateRequest request)
        {
            const string sql = @"INSERT INTO dbo.WMS_FG_Warehouse
(WarehouseCode, WarehouseName, WarehouseType, IsActive, Remark, CreateBy)
VALUES (@WarehouseCode, @WarehouseName, @WarehouseType, 1, @Remark, @Operator);
SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
            using (SqlConnection connection = DbConnectionFactory.Create())
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                AddWarehouseParameters(command, request.WarehouseCode, request.WarehouseName, request.WarehouseType, request.Remark, request.Operator);
                connection.Open();
                return Convert.ToInt64(command.ExecuteScalar());
            }
        }

        public bool UpdateWarehouse(string warehouseCode, WarehouseUpdateRequest request)
        {
            const string sql = @"UPDATE dbo.WMS_FG_Warehouse SET WarehouseName=@WarehouseName, WarehouseType=@WarehouseType,
Remark=@Remark, LastEditBy=@Operator, LastEditDt=SYSDATETIME() WHERE WarehouseCode=@WarehouseCode;";
            using (SqlConnection connection = DbConnectionFactory.Create())
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                AddWarehouseParameters(command, warehouseCode, request.WarehouseName, request.WarehouseType, request.Remark, request.Operator);
                connection.Open();
                return command.ExecuteNonQuery() > 0;
            }
        }

        public bool SetWarehouseActive(string warehouseCode, bool isActive, string operatorName)
        {
            const string sql = @"UPDATE dbo.WMS_FG_Warehouse SET IsActive=@IsActive, LastEditBy=@Operator,
LastEditDt=SYSDATETIME() WHERE WarehouseCode=@WarehouseCode;";
            using (SqlConnection connection = DbConnectionFactory.Create())
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                AddString(command, "@WarehouseCode", warehouseCode, 20);
                command.Parameters.Add("@IsActive", SqlDbType.Bit).Value = isActive;
                AddString(command, "@Operator", operatorName, 50);
                connection.Open();
                return command.ExecuteNonQuery() > 0;
            }
        }

        public bool HasActiveLocations(string warehouseCode) { return RelatedCount("dbo.WMS_FG_Location l", "l.IsActive=1", warehouseCode) > 0; }
        public bool HasCurrentHus(string warehouseCode) { return RelatedCount("dbo.WMS_FG_Location l INNER JOIN dbo.WMS_FG_HU_CurrentLocation c ON c.LocationId=l.Id", "1=1", warehouseCode) > 0; }
        public bool HasActiveMappings(string warehouseCode) { return RelatedCount("dbo.WMS_FG_WarehousePlant l", "l.IsActive=1", warehouseCode) > 0; }

        public List<WarehousePlantDto> QueryWarehousePlants(WarehousePlantQuery query)
        {
            List<WarehousePlantDto> result = new List<WarehousePlantDto>();
            using (SqlConnection connection = DbConnectionFactory.Create())
            using (SqlCommand command = new SqlCommand(WarehousePlantQuerySql, connection))
            {
                AddString(command, "@WarehouseCode", query.WarehouseCode, 20);
                AddString(command, "@PlantCode", query.PlantCode, 20);
                AddNullableBit(command, "@IsActive", query.IsActive);
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader()) while (reader.Read()) result.Add(ReadWarehousePlant(reader));
            }
            return result;
        }

        public WarehousePlantDto GetWarehousePlant(string warehouseCode, string plantCode)
        {
            return QueryWarehousePlants(new WarehousePlantQuery { WarehouseCode = warehouseCode, PlantCode = plantCode })
                .Find(x => string.Equals(x.PlantCode, plantCode, StringComparison.OrdinalIgnoreCase));
        }

        public bool WarehousePlantExists(string warehouseCode, string plantCode)
        {
            const string sql = @"SELECT COUNT(1) FROM dbo.WMS_FG_WarehousePlant wp INNER JOIN dbo.WMS_FG_Warehouse w ON w.Id=wp.WarehouseId
WHERE w.WarehouseCode=@WarehouseCode AND wp.PlantCode=@PlantCode;";
            using (SqlConnection connection = DbConnectionFactory.Create())
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                AddString(command, "@WarehouseCode", warehouseCode, 20);
                AddString(command, "@PlantCode", plantCode, 20);
                connection.Open();
                return Convert.ToInt32(command.ExecuteScalar()) > 0;
            }
        }

        public long InsertWarehousePlant(WarehousePlantCreateRequest request)
        {
            const string sql = @"INSERT INTO dbo.WMS_FG_WarehousePlant (WarehouseId, PlantCode, SAPStorageLocation, IsActive, Remark, CreateBy)
SELECT Id, @PlantCode, @SapStorageLocation, 1, @Remark, @Operator FROM dbo.WMS_FG_Warehouse WHERE WarehouseCode=@WarehouseCode;
SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
            using (SqlConnection connection = DbConnectionFactory.Create())
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                AddPlantParameters(command, request.WarehouseCode, request.PlantCode, request.SapStorageLocation, request.Remark, request.Operator);
                connection.Open();
                return Convert.ToInt64(command.ExecuteScalar());
            }
        }

        public bool UpdateWarehousePlant(string warehouseCode, string plantCode, WarehousePlantUpdateRequest request)
        {
            const string sql = @"UPDATE wp SET SAPStorageLocation=@SapStorageLocation, Remark=@Remark, LastEditBy=@Operator, LastEditDt=SYSDATETIME()
FROM dbo.WMS_FG_WarehousePlant wp INNER JOIN dbo.WMS_FG_Warehouse w ON w.Id=wp.WarehouseId
WHERE w.WarehouseCode=@WarehouseCode AND wp.PlantCode=@PlantCode;";
            return ExecutePlantUpdate(sql, warehouseCode, plantCode, request.SapStorageLocation, request.Remark, request.Operator, null);
        }

        public bool SetWarehousePlantActive(string warehouseCode, string plantCode, bool isActive, string operatorName)
        {
            const string sql = @"UPDATE wp SET IsActive=@IsActive, LastEditBy=@Operator, LastEditDt=SYSDATETIME() FROM dbo.WMS_FG_WarehousePlant wp
INNER JOIN dbo.WMS_FG_Warehouse w ON w.Id=wp.WarehouseId WHERE w.WarehouseCode=@WarehouseCode AND wp.PlantCode=@PlantCode;";
            return ExecutePlantUpdate(sql, warehouseCode, plantCode, null, null, operatorName, isActive);
        }

        public List<MasterDataOptionDto> GetWarehouseOptions()
        {
            return ReadOptions(@"SELECT WarehouseCode AS Code, WarehouseName AS Name, IsActive FROM dbo.WMS_FG_Warehouse ORDER BY WarehouseCode;", null);
        }

        public List<MasterDataOptionDto> GetZoneOptions(string warehouseCode)
        {
            const string sql = @"SELECT z.ZoneCode AS Code, z.ZoneName AS Name, z.IsActive FROM dbo.WMS_FG_Zone z
INNER JOIN dbo.WMS_FG_Warehouse w ON w.Id=z.WarehouseId WHERE w.WarehouseCode=@WarehouseCode ORDER BY z.SortNo,z.ZoneCode;";
            return ReadOptions(sql, warehouseCode);
        }

        private static int RelatedCount(string fromSql, string condition, string warehouseCode)
        {
            string sql = "SELECT COUNT(1) FROM " + fromSql + " INNER JOIN dbo.WMS_FG_Warehouse w ON w.Id=l.WarehouseId WHERE w.WarehouseCode=@Value AND " + condition + ";";
            return ScalarCount(sql, warehouseCode);
        }

        private static int ScalarCount(string sql, string value)
        {
            using (SqlConnection connection = DbConnectionFactory.Create())
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                AddString(command, "@Value", value, 20);
                connection.Open();
                return Convert.ToInt32(command.ExecuteScalar());
            }
        }

        private static bool ExecutePlantUpdate(string sql, string warehouseCode, string plantCode, string sapStorageLocation, string remark, string operatorName, bool? isActive)
        {
            using (SqlConnection connection = DbConnectionFactory.Create())
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                AddPlantParameters(command, warehouseCode, plantCode, sapStorageLocation, remark, operatorName);
                if (isActive.HasValue) command.Parameters.Add("@IsActive", SqlDbType.Bit).Value = isActive.Value;
                connection.Open();
                return command.ExecuteNonQuery() > 0;
            }
        }

        private static List<MasterDataOptionDto> ReadOptions(string sql, string warehouseCode)
        {
            List<MasterDataOptionDto> result = new List<MasterDataOptionDto>();
            using (SqlConnection connection = DbConnectionFactory.Create())
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                if (warehouseCode != null) AddString(command, "@WarehouseCode", warehouseCode, 20);
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader()) while (reader.Read()) result.Add(new MasterDataOptionDto
                {
                    Code = Convert.ToString(reader["Code"]), Name = Convert.ToString(reader["Name"]), IsActive = Convert.ToBoolean(reader["IsActive"])
                });
            }
            return result;
        }

        private static WarehouseDto ReadWarehouse(SqlDataReader reader)
        {
            return new WarehouseDto
            {
                Id = Convert.ToInt64(reader["Id"]), WarehouseCode = Convert.ToString(reader["WarehouseCode"]), WarehouseName = Convert.ToString(reader["WarehouseName"]),
                WarehouseType = Convert.ToString(reader["WarehouseType"]), IsActive = Convert.ToBoolean(reader["IsActive"]), Remark = DbString(reader, "Remark"),
                ActiveLocationCount = Convert.ToInt32(reader["ActiveLocationCount"]), CurrentHuCount = Convert.ToInt32(reader["CurrentHuCount"]),
                ActiveMappingCount = Convert.ToInt32(reader["ActiveMappingCount"]), CreateTime = Convert.ToDateTime(reader["CreateTime"]),
                ModifyUser = DbString(reader, "ModifyUser"), ModifyTime = DbDateTime(reader, "ModifyTime")
            };
        }

        private static WarehousePlantDto ReadWarehousePlant(SqlDataReader reader)
        {
            return new WarehousePlantDto
            {
                Id = Convert.ToInt64(reader["Id"]), WarehouseCode = Convert.ToString(reader["WarehouseCode"]), WarehouseName = Convert.ToString(reader["WarehouseName"]),
                PlantCode = Convert.ToString(reader["PlantCode"]), SapStorageLocation = DbString(reader, "SapStorageLocation"), IsActive = Convert.ToBoolean(reader["IsActive"]),
                Remark = DbString(reader, "Remark"), CreateTime = Convert.ToDateTime(reader["CreateTime"])
            };
        }

        private static void AddWarehouseParameters(SqlCommand command, string code, string name, string type, string remark, string operatorName)
        {
            AddString(command, "@WarehouseCode", code, 20); AddString(command, "@WarehouseName", name, 100, true);
            AddString(command, "@WarehouseType", type, 30); AddString(command, "@Remark", remark, 500, true); AddString(command, "@Operator", operatorName, 50);
        }

        private static void AddPlantParameters(SqlCommand command, string warehouseCode, string plantCode, string sapStorageLocation, string remark, string operatorName)
        {
            AddString(command, "@WarehouseCode", warehouseCode, 20); AddString(command, "@PlantCode", plantCode, 20);
            AddString(command, "@SapStorageLocation", sapStorageLocation, 20); AddString(command, "@Remark", remark, 500, true);
            AddString(command, "@Operator", operatorName, 50);
        }

        private static void AddNullableBit(SqlCommand command, string name, bool? value)
        {
            command.Parameters.Add(name, SqlDbType.Bit).Value = value.HasValue ? (object)value.Value : DBNull.Value;
        }

        private static void AddString(SqlCommand command, string name, string value, int size, bool unicode = false)
        {
            command.Parameters.Add(name, unicode ? SqlDbType.NVarChar : SqlDbType.VarChar, size).Value = string.IsNullOrWhiteSpace(value) ? (object)DBNull.Value : value;
        }

        private static string DbString(SqlDataReader reader, string name) { return reader[name] == DBNull.Value ? null : Convert.ToString(reader[name]); }
        private static DateTime? DbDateTime(SqlDataReader reader, string name) { return reader[name] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader[name]); }
    }
}
