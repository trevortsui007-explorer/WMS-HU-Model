using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using WMS_HU_Model.Infrastructure.Database;
using WMS_HU_Model.Models.MasterData;

namespace WMS_HU_Model.Repositories
{
    public class LocationRepository : ILocationRepository
    {
        public List<LocationDto> GetList(
            string warehouseCode,
            string zoneCode)
        {
            var list = new List<LocationDto>();

            const string sql = @"
SELECT
    l.Id,
    w.WarehouseCode,
    w.WarehouseName,
    z.ZoneCode,
    z.ZoneName,

    l.LocationCode,
    l.LocationName,

    p.LocationCode AS ParentLocationCode,

    l.LocationLevel,
    l.LocationTypeCode,
    l.ManageMode,
    l.IsVirtual,
    l.IsScannable,
    l.ScanCode,
    l.CapacityMode,
    l.CapacityQty,
    l.MaxWeightKg,
    l.PlantPolicy,
    l.DedicatedPlantCode,
    l.AllowMixedMaterial,
    l.AllowMixedBatch,
    l.AllowMixedOrder,
    l.PutawayPriority,
    l.PickingPriority,
    l.RouteSequence,
    l.MasterStatus,
    l.Remark

FROM dbo.WMS_Location l

INNER JOIN dbo.WMS_Warehouse w
    ON l.WarehouseId = w.Id

INNER JOIN dbo.WMS_Zone z
    ON l.ZoneId = z.Id

LEFT JOIN dbo.WMS_Location p
    ON l.ParentLocationId = p.Id

WHERE
    (@WarehouseCode IS NULL
        OR w.WarehouseCode = @WarehouseCode)

AND
    (@ZoneCode IS NULL
        OR z.ZoneCode = @ZoneCode)

ORDER BY
    w.WarehouseCode,
    z.SortNo,
    l.RouteSequence,
    l.LocationCode;
";

            using (var conn = DbConnectionFactory.Create())
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue(
                    "@WarehouseCode",
                    string.IsNullOrWhiteSpace(warehouseCode)
                        ? (object)DBNull.Value
                        : warehouseCode);

                cmd.Parameters.AddWithValue(
                    "@ZoneCode",
                    string.IsNullOrWhiteSpace(zoneCode)
                        ? (object)DBNull.Value
                        : zoneCode);

                conn.Open();

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(Map(reader));
                    }
                }
            }

            return list;
        }

        public LocationDto GetByCode(
            string warehouseCode,
            string locationCode)
        {
            const string sql = @"
SELECT
    l.Id,
    w.WarehouseCode,
    w.WarehouseName,
    z.ZoneCode,
    z.ZoneName,

    l.LocationCode,
    l.LocationName,

    p.LocationCode AS ParentLocationCode,

    l.LocationLevel,
    l.LocationTypeCode,
    l.ManageMode,
    l.IsVirtual,
    l.IsScannable,
    l.ScanCode,
    l.CapacityMode,
    l.CapacityQty,
    l.MaxWeightKg,
    l.PlantPolicy,
    l.DedicatedPlantCode,
    l.AllowMixedMaterial,
    l.AllowMixedBatch,
    l.AllowMixedOrder,
    l.PutawayPriority,
    l.PickingPriority,
    l.RouteSequence,
    l.MasterStatus,
    l.Remark

FROM dbo.WMS_Location l

INNER JOIN dbo.WMS_Warehouse w
    ON l.WarehouseId = w.Id

INNER JOIN dbo.WMS_Zone z
    ON l.ZoneId = z.Id

LEFT JOIN dbo.WMS_Location p
    ON l.ParentLocationId = p.Id

WHERE
    w.WarehouseCode = @WarehouseCode
AND l.LocationCode = @LocationCode;
";

            using (var conn = DbConnectionFactory.Create())
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue(
                    "@WarehouseCode",
                    warehouseCode);

                cmd.Parameters.AddWithValue(
                    "@LocationCode",
                    locationCode);

                conn.Open();

                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.Read())
                        return null;

                    return Map(reader);
                }
            }
        }

        public long? GetWarehouseId(string warehouseCode)
        {
            const string sql = @"
SELECT Id
FROM dbo.WMS_Warehouse
WHERE WarehouseCode = @WarehouseCode
AND IsActive = 1;
";

            return ExecuteScalarLong(
                sql,
                new SqlParameter(
                    "@WarehouseCode",
                    warehouseCode));
        }

        public long? GetZoneId(
            long warehouseId,
            string zoneCode)
        {
            const string sql = @"
SELECT Id
FROM dbo.WMS_Zone
WHERE WarehouseId = @WarehouseId
AND ZoneCode = @ZoneCode
AND IsActive = 1;
";

            using (var conn = DbConnectionFactory.Create())
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue(
                    "@WarehouseId",
                    warehouseId);

                cmd.Parameters.AddWithValue(
                    "@ZoneCode",
                    zoneCode);

                conn.Open();

                var value = cmd.ExecuteScalar();

                if (value == null ||
                    value == DBNull.Value)
                    return null;

                return Convert.ToInt64(value);
            }
        }

        public long? GetLocationId(
            long warehouseId,
            string locationCode)
        {
            const string sql = @"
SELECT Id
FROM dbo.WMS_Location
WHERE WarehouseId = @WarehouseId
AND LocationCode = @LocationCode;
";

            using (var conn = DbConnectionFactory.Create())
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue(
                    "@WarehouseId",
                    warehouseId);

                cmd.Parameters.AddWithValue(
                    "@LocationCode",
                    locationCode);

                conn.Open();

                var value = cmd.ExecuteScalar();

                if (value == null ||
                    value == DBNull.Value)
                    return null;

                return Convert.ToInt64(value);
            }
        }

        public bool Exists(
            long warehouseId,
            string locationCode)
        {
            return GetLocationId(
                warehouseId,
                locationCode).HasValue;
        }

        public long Insert(
            long warehouseId,
            long zoneId,
            long? parentLocationId,
            CreateLocationRequest request)
        {
            const string sql = @"
INSERT INTO dbo.WMS_Location
(
    WarehouseId,
    ZoneId,
    LocationCode,
    LocationName,
    ParentLocationId,
    LocationLevel,
    LocationTypeCode,
    ManageMode,
    IsVirtual,
    IsScannable,
    ScanCode,
    CapacityMode,
    CapacityQty,
    MaxWeightKg,
    PlantPolicy,
    DedicatedPlantCode,
    AllowMixedMaterial,
    AllowMixedBatch,
    AllowMixedOrder,
    PutawayPriority,
    PickingPriority,
    RouteSequence,
    MasterStatus,
    Remark,
    CreateUser
)
VALUES
(
    @WarehouseId,
    @ZoneId,
    @LocationCode,
    @LocationName,
    @ParentLocationId,
    @LocationLevel,
    @LocationTypeCode,
    @ManageMode,
    @IsVirtual,
    @IsScannable,
    @ScanCode,
    @CapacityMode,
    @CapacityQty,
    @MaxWeightKg,
    @PlantPolicy,
    @DedicatedPlantCode,
    @AllowMixedMaterial,
    @AllowMixedBatch,
    @AllowMixedOrder,
    @PutawayPriority,
    @PickingPriority,
    @RouteSequence,
    'ACTIVE',
    @Remark,
    @CreateUser
);

SELECT CAST(SCOPE_IDENTITY() AS BIGINT);
";

            using (var conn = DbConnectionFactory.Create())
            using (var cmd = new SqlCommand(sql, conn))
            {
                AddParameters(
                    cmd,
                    warehouseId,
                    zoneId,
                    parentLocationId,
                    request);

                conn.Open();

                return Convert.ToInt64(
                    cmd.ExecuteScalar());
            }
        }

        public bool Update(
            long locationId,
            long zoneId,
            long? parentLocationId,
            UpdateLocationRequest request)
        {
            const string sql = @"
UPDATE dbo.WMS_Location

SET
    ZoneId              = @ZoneId,
    LocationName        = @LocationName,
    ParentLocationId    = @ParentLocationId,
    LocationLevel       = @LocationLevel,
    LocationTypeCode    = @LocationTypeCode,
    ManageMode          = @ManageMode,
    IsScannable         = @IsScannable,
    ScanCode            = @ScanCode,
    CapacityMode        = @CapacityMode,
    CapacityQty         = @CapacityQty,
    MaxWeightKg         = @MaxWeightKg,
    PlantPolicy         = @PlantPolicy,
    DedicatedPlantCode  = @DedicatedPlantCode,
    AllowMixedMaterial  = @AllowMixedMaterial,
    AllowMixedBatch     = @AllowMixedBatch,
    AllowMixedOrder     = @AllowMixedOrder,
    PutawayPriority     = @PutawayPriority,
    PickingPriority     = @PickingPriority,
    RouteSequence       = @RouteSequence,
    MasterStatus        = @MasterStatus,
    Remark              = @Remark,
    ModifyUser          = @ModifyUser,
    ModifyTime          = SYSDATETIME()

WHERE Id = @LocationId;
";

            using (var conn = DbConnectionFactory.Create())
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue(
                    "@LocationId",
                    locationId);

                cmd.Parameters.AddWithValue(
                    "@ZoneId",
                    zoneId);

                cmd.Parameters.AddWithValue(
                    "@ParentLocationId",
                    (object)parentLocationId
                    ?? DBNull.Value);

                cmd.Parameters.AddWithValue(
                    "@LocationName",
                    (object)request.LocationName
                    ?? DBNull.Value);

                cmd.Parameters.AddWithValue(
                    "@LocationLevel",
                    request.LocationLevel);

                cmd.Parameters.AddWithValue(
                    "@LocationTypeCode",
                    request.LocationTypeCode);

                cmd.Parameters.AddWithValue(
                    "@ManageMode",
                    request.ManageMode);

                cmd.Parameters.AddWithValue(
                    "@IsScannable",
                    request.IsScannable);

                cmd.Parameters.AddWithValue(
                    "@ScanCode",
                    (object)request.ScanCode
                    ?? DBNull.Value);

                cmd.Parameters.AddWithValue(
                    "@CapacityMode",
                    request.CapacityMode);

                cmd.Parameters.AddWithValue(
                    "@CapacityQty",
                    (object)request.CapacityQty
                    ?? DBNull.Value);

                cmd.Parameters.AddWithValue(
                    "@MaxWeightKg",
                    (object)request.MaxWeightKg
                    ?? DBNull.Value);

                cmd.Parameters.AddWithValue(
                    "@PlantPolicy",
                    request.PlantPolicy);

                cmd.Parameters.AddWithValue(
                    "@DedicatedPlantCode",
                    (object)request.DedicatedPlantCode
                    ?? DBNull.Value);

                cmd.Parameters.AddWithValue(
                    "@AllowMixedMaterial",
                    request.AllowMixedMaterial);

                cmd.Parameters.AddWithValue(
                    "@AllowMixedBatch",
                    request.AllowMixedBatch);

                cmd.Parameters.AddWithValue(
                    "@AllowMixedOrder",
                    request.AllowMixedOrder);

                cmd.Parameters.AddWithValue(
                    "@PutawayPriority",
                    request.PutawayPriority);

                cmd.Parameters.AddWithValue(
                    "@PickingPriority",
                    request.PickingPriority);

                cmd.Parameters.AddWithValue(
                    "@RouteSequence",
                    request.RouteSequence);

                cmd.Parameters.AddWithValue(
                    "@MasterStatus",
                    request.MasterStatus);

                cmd.Parameters.AddWithValue(
                    "@Remark",
                    (object)request.Remark
                    ?? DBNull.Value);

                cmd.Parameters.AddWithValue(
                    "@ModifyUser",
                    (object)request.Operator
                    ?? DBNull.Value);

                conn.Open();

                return cmd.ExecuteNonQuery() > 0;
            }
        }

        private static void AddParameters(
            SqlCommand cmd,
            long warehouseId,
            long zoneId,
            long? parentLocationId,
            CreateLocationRequest request)
        {
            cmd.Parameters.AddWithValue(
                "@WarehouseId",
                warehouseId);

            cmd.Parameters.AddWithValue(
                "@ZoneId",
                zoneId);

            cmd.Parameters.AddWithValue(
                "@LocationCode",
                request.LocationCode);

            cmd.Parameters.AddWithValue(
                "@LocationName",
                (object)request.LocationName
                ?? DBNull.Value);

            cmd.Parameters.AddWithValue(
                "@ParentLocationId",
                (object)parentLocationId
                ?? DBNull.Value);

            cmd.Parameters.AddWithValue(
                "@LocationLevel",
                request.LocationLevel);

            cmd.Parameters.AddWithValue(
                "@LocationTypeCode",
                request.LocationTypeCode);

            cmd.Parameters.AddWithValue(
                "@ManageMode",
                request.ManageMode);

            cmd.Parameters.AddWithValue(
                "@IsVirtual",
                request.IsVirtual);

            cmd.Parameters.AddWithValue(
                "@IsScannable",
                request.IsScannable);

            cmd.Parameters.AddWithValue(
                "@ScanCode",
                (object)request.ScanCode
                ?? DBNull.Value);

            cmd.Parameters.AddWithValue(
                "@CapacityMode",
                request.CapacityMode);

            cmd.Parameters.AddWithValue(
                "@CapacityQty",
                (object)request.CapacityQty
                ?? DBNull.Value);

            cmd.Parameters.AddWithValue(
                "@MaxWeightKg",
                (object)request.MaxWeightKg
                ?? DBNull.Value);

            cmd.Parameters.AddWithValue(
                "@PlantPolicy",
                request.PlantPolicy);

            cmd.Parameters.AddWithValue(
                "@DedicatedPlantCode",
                (object)request.DedicatedPlantCode
                ?? DBNull.Value);

            cmd.Parameters.AddWithValue(
                "@AllowMixedMaterial",
                request.AllowMixedMaterial);

            cmd.Parameters.AddWithValue(
                "@AllowMixedBatch",
                request.AllowMixedBatch);

            cmd.Parameters.AddWithValue(
                "@AllowMixedOrder",
                request.AllowMixedOrder);

            cmd.Parameters.AddWithValue(
                "@PutawayPriority",
                request.PutawayPriority);

            cmd.Parameters.AddWithValue(
                "@PickingPriority",
                request.PickingPriority);

            cmd.Parameters.AddWithValue(
                "@RouteSequence",
                request.RouteSequence);

            cmd.Parameters.AddWithValue(
                "@Remark",
                (object)request.Remark
                ?? DBNull.Value);

            cmd.Parameters.AddWithValue(
                "@CreateUser",
                (object)request.Operator
                ?? DBNull.Value);
        }

        private static long? ExecuteScalarLong(
            string sql,
            SqlParameter parameter)
        {
            using (var conn =
                DbConnectionFactory.Create())

            using (var cmd =
                new SqlCommand(sql, conn))
            {
                cmd.Parameters.Add(parameter);

                conn.Open();

                var value = cmd.ExecuteScalar();

                if (value == null ||
                    value == DBNull.Value)
                    return null;

                return Convert.ToInt64(value);
            }
        }

        private static LocationDto Map(
            IDataRecord reader)
        {
            return new LocationDto
            {
                Id =
                    Convert.ToInt64(reader["Id"]),

                WarehouseCode =
                    reader["WarehouseCode"].ToString(),

                WarehouseName =
                    reader["WarehouseName"].ToString(),

                ZoneCode =
                    reader["ZoneCode"].ToString(),

                ZoneName =
                    reader["ZoneName"].ToString(),

                LocationCode =
                    reader["LocationCode"].ToString(),

                LocationName =
                    DbString(reader["LocationName"]),

                ParentLocationCode =
                    DbString(reader["ParentLocationCode"]),

                LocationLevel =
                    reader["LocationLevel"].ToString(),

                LocationTypeCode =
                    reader["LocationTypeCode"].ToString(),

                ManageMode =
                    reader["ManageMode"].ToString(),

                IsVirtual =
                    Convert.ToBoolean(reader["IsVirtual"]),

                IsScannable =
                    Convert.ToBoolean(reader["IsScannable"]),

                ScanCode =
                    DbString(reader["ScanCode"]),

                CapacityMode =
                    reader["CapacityMode"].ToString(),

                CapacityQty =
                    DbDecimal(reader["CapacityQty"]),

                MaxWeightKg =
                    DbDecimal(reader["MaxWeightKg"]),

                PlantPolicy =
                    reader["PlantPolicy"].ToString(),

                DedicatedPlantCode =
                    DbString(reader["DedicatedPlantCode"]),

                AllowMixedMaterial =
                    Convert.ToBoolean(
                        reader["AllowMixedMaterial"]),

                AllowMixedBatch =
                    Convert.ToBoolean(
                        reader["AllowMixedBatch"]),

                AllowMixedOrder =
                    Convert.ToBoolean(
                        reader["AllowMixedOrder"]),

                PutawayPriority =
                    Convert.ToInt32(
                        reader["PutawayPriority"]),

                PickingPriority =
                    Convert.ToInt32(
                        reader["PickingPriority"]),

                RouteSequence =
                    Convert.ToInt32(
                        reader["RouteSequence"]),

                MasterStatus =
                    reader["MasterStatus"].ToString(),

                Remark =
                    DbString(reader["Remark"])
            };
        }

        private static string DbString(object value)
        {
            return value == DBNull.Value
                ? null
                : value.ToString();
        }

        private static decimal? DbDecimal(object value)
        {
            return value == DBNull.Value
                ? (decimal?)null
                : Convert.ToDecimal(value);
        }
    }
}
