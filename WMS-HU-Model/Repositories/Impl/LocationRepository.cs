using System;
using System.Collections.Generic;
using System.Data;
using Dapper;
using WMS_HU_Model.Infrastructure.Database;
using WMS_HU_Model.Models.MasterData;

namespace WMS_HU_Model.Repositories
{
    public class LocationRepository : ILocationRepository
    {
        public const string MaintenanceListSql = @"
SELECT l.Id, w.WarehouseCode, w.WarehouseName, z.ZoneCode, z.ZoneName,
    l.LocationCode, l.LocationName, l.LocationCode AS ScanCode,
    l.LocationTypeCode, l.IsVirtual, l.IsActive, l.PlantPolicy,
    l.DedicatedPlantCode, l.AllowMixedMaterial, l.AllowMixedBatch, l.AllowMixedOrder,
    l.PutawayPriority, l.PickingPriority, l.RouteSequence,
    l.PhysicalRackGroupCode, l.PhysicalRowCode, l.PositionSequence,
    CASE WHEN l.IsActive=1 THEN 'ACTIVE' ELSE 'INACTIVE' END AS MasterStatus, l.Remark,
    (SELECT COUNT(1) FROM dbo.WMS_FG_HU_CurrentLocation currentLocation WHERE currentLocation.LocationId=l.Id) AS CurrentHuCount
FROM dbo.WMS_FG_Location l
INNER JOIN dbo.WMS_FG_Warehouse w ON w.Id=l.WarehouseId
INNER JOIN dbo.WMS_FG_Zone z ON z.Id=l.ZoneId
WHERE (@WarehouseCode IS NULL OR w.WarehouseCode=@WarehouseCode)
  AND (@LocationCode IS NULL OR l.LocationCode LIKE '%' + @LocationCode + '%')
  AND (@LocationTypeCode IS NULL OR l.LocationTypeCode=@LocationTypeCode)
  AND (@IsActive IS NULL OR l.IsActive=@IsActive)
ORDER BY w.WarehouseCode,z.SortNo,l.RouteSequence,l.LocationCode;";

        public List<LocationDto> GetList(
            string warehouseCode,
            string zoneCode)
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

    l.LocationTypeCode,
    l.IsVirtual,
    l.IsActive,
    l.LocationCode AS ScanCode,
    l.PlantPolicy,
    l.DedicatedPlantCode,
    l.AllowMixedMaterial,
    l.AllowMixedBatch,
    l.AllowMixedOrder,
    l.PutawayPriority,
    l.PickingPriority,
    l.RouteSequence,
    l.PhysicalRackGroupCode,
    l.PhysicalRowCode,
    l.PositionSequence,
    CASE WHEN l.IsActive=1 THEN 'ACTIVE' ELSE 'INACTIVE' END AS MasterStatus,
    l.Remark

FROM dbo.WMS_FG_Location l

INNER JOIN dbo.WMS_FG_Warehouse w
    ON l.WarehouseId = w.Id

INNER JOIN dbo.WMS_FG_Zone z
    ON l.ZoneId = z.Id

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

            return DbExecutor.Query<LocationDto>(
                sql,
                new
                {
                    WarehouseCode = string.IsNullOrWhiteSpace(warehouseCode)
                        ? null
                        : warehouseCode,
                    ZoneCode = string.IsNullOrWhiteSpace(zoneCode)
                        ? null
                        : zoneCode
                });
        }

        public List<LocationDto> GetMaintenanceList(LocationMaintenanceQuery query)
        {
            return DbExecutor.Query<LocationDto>(MaintenanceListSql, new
            {
                WarehouseCode = string.IsNullOrWhiteSpace(query.WarehouseCode) ? null : query.WarehouseCode,
                LocationCode = string.IsNullOrWhiteSpace(query.LocationCode) ? null : query.LocationCode,
                LocationTypeCode = string.IsNullOrWhiteSpace(query.LocationTypeCode) ? null : query.LocationTypeCode,
                IsActive = string.IsNullOrWhiteSpace(query.MasterStatus)
                    ? (bool?)null
                    : string.Equals(query.MasterStatus, "ACTIVE", StringComparison.OrdinalIgnoreCase)
            });
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

    l.LocationTypeCode,
    l.IsVirtual,
    l.IsActive,
    l.LocationCode AS ScanCode,
    l.PlantPolicy,
    l.DedicatedPlantCode,
    l.AllowMixedMaterial,
    l.AllowMixedBatch,
    l.AllowMixedOrder,
    l.PutawayPriority,
    l.PickingPriority,
    l.RouteSequence,
    l.PhysicalRackGroupCode,
    l.PhysicalRowCode,
    l.PositionSequence,
    CASE WHEN l.IsActive=1 THEN 'ACTIVE' ELSE 'INACTIVE' END AS MasterStatus,
    l.Remark

FROM dbo.WMS_FG_Location l

INNER JOIN dbo.WMS_FG_Warehouse w
    ON l.WarehouseId = w.Id

INNER JOIN dbo.WMS_FG_Zone z
    ON l.ZoneId = z.Id

WHERE
    w.WarehouseCode = @WarehouseCode
AND l.LocationCode = @LocationCode;
";

            return DbExecutor.QueryFirstOrDefault<LocationDto>(
                sql,
                new
                {
                    WarehouseCode = warehouseCode,
                    LocationCode = locationCode
                });
        }

        public long? GetWarehouseId(string warehouseCode)
        {
            const string sql = @"
SELECT Id
FROM dbo.WMS_FG_Warehouse
WHERE WarehouseCode = @WarehouseCode
AND IsActive = 1;
";

            return DbExecutor.ExecuteScalar<long?>(
                sql,
                new { WarehouseCode = warehouseCode });
        }

        public long? GetWarehouseIdAny(string warehouseCode)
        {
            const string sql = "SELECT Id FROM dbo.WMS_FG_Warehouse WHERE WarehouseCode=@WarehouseCode;";
            return DbExecutor.ExecuteScalar<long?>(sql, new { WarehouseCode = warehouseCode });
        }

        public long? GetZoneId(
            long warehouseId,
            string zoneCode)
        {
            const string sql = @"
SELECT Id
FROM dbo.WMS_FG_Zone
WHERE WarehouseId = @WarehouseId
AND ZoneCode = @ZoneCode
AND IsActive = 1;
";

            return DbExecutor.ExecuteScalar<long?>(
                sql,
                new
                {
                    WarehouseId = warehouseId,
                    ZoneCode = zoneCode
                });
        }

        public long? GetLocationId(
            long warehouseId,
            string locationCode)
        {
            const string sql = @"
SELECT Id
FROM dbo.WMS_FG_Location
WHERE WarehouseId = @WarehouseId
AND LocationCode = @LocationCode;
";

            return DbExecutor.ExecuteScalar<long?>(
                sql,
                new
                {
                    WarehouseId = warehouseId,
                    LocationCode = locationCode
                });
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
            CreateLocationRequest request)
        {
            const string sql = @"
INSERT INTO dbo.WMS_FG_Location
(
    WarehouseId,
    ZoneId,
    LocationCode,
    LocationName,
    LocationTypeCode,
    IsVirtual,
    PlantPolicy,
    DedicatedPlantCode,
    AllowMixedMaterial,
    AllowMixedBatch,
    AllowMixedOrder,
    PutawayPriority,
    PickingPriority,
    RouteSequence,
    IsActive,
    Remark,
    CreateBy
)
VALUES
(
    @WarehouseId,
    @ZoneId,
    @LocationCode,
    @LocationName,
    @LocationTypeCode,
    @IsVirtual,
    @PlantPolicy,
    @DedicatedPlantCode,
    @AllowMixedMaterial,
    @AllowMixedBatch,
    @AllowMixedOrder,
    @PutawayPriority,
    @PickingPriority,
    @RouteSequence,
    1,
    @Remark,
    @CreateBy
);

SELECT CAST(SCOPE_IDENTITY() AS BIGINT);
";

            return DbExecutor.ExecuteScalar<long>(
                sql,
                CreateParameters(
                    warehouseId,
                    zoneId,
                    request));
        }

        public bool Update(
            long locationId,
            long zoneId,
            UpdateLocationRequest request)
        {
            const string sql = @"
UPDATE dbo.WMS_FG_Location

SET
    ZoneId              = @ZoneId,
    LocationName        = @LocationName,
    LocationTypeCode    = @LocationTypeCode,
    PlantPolicy         = @PlantPolicy,
    DedicatedPlantCode  = @DedicatedPlantCode,
    AllowMixedMaterial  = @AllowMixedMaterial,
    AllowMixedBatch     = @AllowMixedBatch,
    AllowMixedOrder     = @AllowMixedOrder,
    PutawayPriority     = @PutawayPriority,
    PickingPriority     = @PickingPriority,
    RouteSequence       = @RouteSequence,
    Remark              = @Remark,
    LastEditBy          = @LastEditBy,
    LastEditDt          = SYSDATETIME()

WHERE Id = @LocationId;
";

            DynamicParameters parameters = CreateParameters(
                zoneId,
                request);

            parameters.Add("@LocationId", locationId, DbType.Int64);

            return DbExecutor.Execute(sql, parameters) > 0;
        }

        public int CountCurrentHus(long locationId)
        {
            const string sql = "SELECT COUNT(1) FROM dbo.WMS_FG_HU_CurrentLocation WHERE LocationId=@LocationId;";
            return DbExecutor.ExecuteScalar<int>(sql, new { LocationId = locationId });
        }

        public bool SetActive(long locationId, bool isActive, string operatorName)
        {
            const string sql = @"UPDATE dbo.WMS_FG_Location SET IsActive=@IsActive,
LastEditBy=@Operator, LastEditDt=SYSDATETIME()
WHERE Id=@LocationId
  AND
  (
      @IsActive=1
      OR NOT EXISTS
      (
          SELECT 1 FROM dbo.WMS_FG_HU_CurrentLocation currentLocation
          WHERE currentLocation.LocationId=@LocationId
      )
  );";
            DynamicParameters parameters = new DynamicParameters();
            parameters.Add("@IsActive", isActive, DbType.Boolean);
            parameters.Add("@Operator", DbParameterValue.StringOrNull(operatorName), DbType.AnsiString, size: 50);
            parameters.Add("@LocationId", locationId, DbType.Int64);
            return DbExecutor.Execute(sql, parameters) > 0;
        }

        private static DynamicParameters CreateParameters(
            long warehouseId,
            long zoneId,
            CreateLocationRequest request)
        {
            DynamicParameters parameters = new DynamicParameters();
            parameters.Add("@WarehouseId", warehouseId, DbType.Int64);
            parameters.Add("@ZoneId", zoneId, DbType.Int64);
            parameters.Add("@LocationCode", request.LocationCode, DbType.AnsiString, size: 50);
            parameters.Add("@LocationName", DbParameterValue.StringOrNull(request.LocationName), DbType.String, size: 100);
            parameters.Add("@LocationTypeCode", request.LocationTypeCode, DbType.AnsiString, size: 30);
            parameters.Add("@IsVirtual", request.IsVirtual, DbType.Boolean);
            parameters.Add("@PlantPolicy", request.PlantPolicy, DbType.AnsiString, size: 30);
            parameters.Add("@DedicatedPlantCode", DbParameterValue.StringOrNull(request.DedicatedPlantCode), DbType.AnsiString, size: 20);
            parameters.Add("@AllowMixedMaterial", request.AllowMixedMaterial, DbType.Boolean);
            parameters.Add("@AllowMixedBatch", request.AllowMixedBatch, DbType.Boolean);
            parameters.Add("@AllowMixedOrder", request.AllowMixedOrder, DbType.Boolean);
            parameters.Add("@PutawayPriority", request.PutawayPriority, DbType.Int32);
            parameters.Add("@PickingPriority", request.PickingPriority, DbType.Int32);
            parameters.Add("@RouteSequence", request.RouteSequence, DbType.Int32);
            parameters.Add("@Remark", DbParameterValue.StringOrNull(request.Remark), DbType.String, size: 500);
            parameters.Add("@CreateBy", DbParameterValue.StringOrNull(request.Operator), DbType.AnsiString, size: 50);

            return parameters;
        }

        private static DynamicParameters CreateParameters(
            long zoneId,
            UpdateLocationRequest request)
        {
            DynamicParameters parameters = new DynamicParameters();
            parameters.Add("@ZoneId", zoneId, DbType.Int64);
            parameters.Add("@LocationName", DbParameterValue.StringOrNull(request.LocationName), DbType.String, size: 100);
            parameters.Add("@LocationTypeCode", request.LocationTypeCode, DbType.AnsiString, size: 30);
            parameters.Add("@PlantPolicy", request.PlantPolicy, DbType.AnsiString, size: 30);
            parameters.Add("@DedicatedPlantCode", DbParameterValue.StringOrNull(request.DedicatedPlantCode), DbType.AnsiString, size: 20);
            parameters.Add("@AllowMixedMaterial", request.AllowMixedMaterial, DbType.Boolean);
            parameters.Add("@AllowMixedBatch", request.AllowMixedBatch, DbType.Boolean);
            parameters.Add("@AllowMixedOrder", request.AllowMixedOrder, DbType.Boolean);
            parameters.Add("@PutawayPriority", request.PutawayPriority, DbType.Int32);
            parameters.Add("@PickingPriority", request.PickingPriority, DbType.Int32);
            parameters.Add("@RouteSequence", request.RouteSequence, DbType.Int32);
            parameters.Add("@Remark", DbParameterValue.StringOrNull(request.Remark), DbType.String, size: 500);
            parameters.Add("@LastEditBy", DbParameterValue.StringOrNull(request.Operator), DbType.AnsiString, size: 50);

            return parameters;
        }
    }
}
