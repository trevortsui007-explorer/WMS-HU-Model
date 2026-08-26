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
    l.LocationCode, l.LocationName, p.LocationCode AS ParentLocationCode,
    l.LocationLevel, l.LocationTypeCode, l.ManageMode, l.IsVirtual, l.IsScannable,
    l.ScanCode, l.CapacityMode, l.CapacityQty, l.MaxWeightKg, l.PlantPolicy,
    l.DedicatedPlantCode, l.AllowMixedMaterial, l.AllowMixedBatch, l.AllowMixedOrder,
    l.PutawayPriority, l.PickingPriority, l.RouteSequence, l.MasterStatus, l.Remark,
    (SELECT COUNT(1) FROM dbo.WMS_HU_CurrentLocation currentLocation WHERE currentLocation.LocationId=l.Id) AS CurrentHuCount
FROM dbo.WMS_Location l
INNER JOIN dbo.WMS_Warehouse w ON w.Id=l.WarehouseId
INNER JOIN dbo.WMS_Zone z ON z.Id=l.ZoneId
LEFT JOIN dbo.WMS_Location p ON p.Id=l.ParentLocationId
WHERE (@WarehouseCode IS NULL OR w.WarehouseCode=@WarehouseCode)
  AND (@LocationCode IS NULL OR l.LocationCode LIKE '%' + @LocationCode + '%')
  AND (@LocationTypeCode IS NULL OR l.LocationTypeCode=@LocationTypeCode)
  AND (@MasterStatus IS NULL OR l.MasterStatus=@MasterStatus)
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
                MasterStatus = string.IsNullOrWhiteSpace(query.MasterStatus) ? null : query.MasterStatus
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
FROM dbo.WMS_Warehouse
WHERE WarehouseCode = @WarehouseCode
AND IsActive = 1;
";

            return DbExecutor.ExecuteScalar<long?>(
                sql,
                new { WarehouseCode = warehouseCode });
        }

        public long? GetWarehouseIdAny(string warehouseCode)
        {
            const string sql = "SELECT Id FROM dbo.WMS_Warehouse WHERE WarehouseCode=@WarehouseCode;";
            return DbExecutor.ExecuteScalar<long?>(sql, new { WarehouseCode = warehouseCode });
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
FROM dbo.WMS_Location
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

            return DbExecutor.ExecuteScalar<long>(
                sql,
                CreateParameters(
                    warehouseId,
                    zoneId,
                    parentLocationId,
                    request));
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

            DynamicParameters parameters = CreateParameters(
                zoneId,
                parentLocationId,
                request);

            parameters.Add("@LocationId", locationId, DbType.Int64);

            return DbExecutor.Execute(sql, parameters) > 0;
        }

        public int CountCurrentHus(long locationId)
        {
            const string sql = "SELECT COUNT(1) FROM dbo.WMS_HU_CurrentLocation WHERE LocationId=@LocationId;";
            return DbExecutor.ExecuteScalar<int>(sql, new { LocationId = locationId });
        }

        public bool SetMasterStatus(long locationId, string masterStatus, string operatorName)
        {
            const string sql = @"UPDATE dbo.WMS_Location SET MasterStatus=@MasterStatus,
ModifyUser=@Operator, ModifyTime=SYSDATETIME()
WHERE Id=@LocationId
  AND
  (
      @MasterStatus='ACTIVE'
      OR NOT EXISTS
      (
          SELECT 1 FROM dbo.WMS_HU_CurrentLocation currentLocation
          WHERE currentLocation.LocationId=@LocationId
      )
  );";
            DynamicParameters parameters = new DynamicParameters();
            parameters.Add("@MasterStatus", masterStatus, DbType.AnsiString, size: 20);
            parameters.Add("@Operator", DbParameterValue.StringOrNull(operatorName), DbType.AnsiString, size: 50);
            parameters.Add("@LocationId", locationId, DbType.Int64);
            return DbExecutor.Execute(sql, parameters) > 0;
        }

        private static DynamicParameters CreateParameters(
            long warehouseId,
            long zoneId,
            long? parentLocationId,
            CreateLocationRequest request)
        {
            DynamicParameters parameters = new DynamicParameters();
            parameters.Add("@WarehouseId", warehouseId, DbType.Int64);
            parameters.Add("@ZoneId", zoneId, DbType.Int64);
            parameters.Add("@LocationCode", request.LocationCode, DbType.AnsiString, size: 100);
            parameters.Add("@LocationName", DbParameterValue.StringOrNull(request.LocationName), DbType.String, size: 200);
            parameters.Add("@ParentLocationId", parentLocationId, DbType.Int64);
            parameters.Add("@LocationLevel", request.LocationLevel, DbType.AnsiString, size: 20);
            parameters.Add("@LocationTypeCode", request.LocationTypeCode, DbType.AnsiString, size: 30);
            parameters.Add("@ManageMode", request.ManageMode, DbType.AnsiString, size: 30);
            parameters.Add("@IsVirtual", request.IsVirtual, DbType.Boolean);
            parameters.Add("@IsScannable", request.IsScannable, DbType.Boolean);
            parameters.Add("@ScanCode", DbParameterValue.StringOrNull(request.ScanCode), DbType.AnsiString, size: 100);
            parameters.Add("@CapacityMode", request.CapacityMode, DbType.AnsiString, size: 30);
            parameters.Add("@CapacityQty", request.CapacityQty, DbType.Decimal, precision: 18, scale: 4);
            parameters.Add("@MaxWeightKg", request.MaxWeightKg, DbType.Decimal, precision: 18, scale: 4);
            parameters.Add("@PlantPolicy", request.PlantPolicy, DbType.AnsiString, size: 20);
            parameters.Add("@DedicatedPlantCode", DbParameterValue.StringOrNull(request.DedicatedPlantCode), DbType.AnsiString, size: 20);
            parameters.Add("@AllowMixedMaterial", request.AllowMixedMaterial, DbType.Boolean);
            parameters.Add("@AllowMixedBatch", request.AllowMixedBatch, DbType.Boolean);
            parameters.Add("@AllowMixedOrder", request.AllowMixedOrder, DbType.Boolean);
            parameters.Add("@PutawayPriority", request.PutawayPriority, DbType.Int32);
            parameters.Add("@PickingPriority", request.PickingPriority, DbType.Int32);
            parameters.Add("@RouteSequence", request.RouteSequence, DbType.Int32);
            parameters.Add("@Remark", DbParameterValue.StringOrNull(request.Remark), DbType.String, size: 500);
            parameters.Add("@CreateUser", DbParameterValue.StringOrNull(request.Operator), DbType.AnsiString, size: 50);

            return parameters;
        }

        private static DynamicParameters CreateParameters(
            long zoneId,
            long? parentLocationId,
            UpdateLocationRequest request)
        {
            DynamicParameters parameters = new DynamicParameters();
            parameters.Add("@ZoneId", zoneId, DbType.Int64);
            parameters.Add("@LocationName", DbParameterValue.StringOrNull(request.LocationName), DbType.String, size: 200);
            parameters.Add("@ParentLocationId", parentLocationId, DbType.Int64);
            parameters.Add("@LocationLevel", request.LocationLevel, DbType.AnsiString, size: 20);
            parameters.Add("@LocationTypeCode", request.LocationTypeCode, DbType.AnsiString, size: 30);
            parameters.Add("@ManageMode", request.ManageMode, DbType.AnsiString, size: 30);
            parameters.Add("@IsScannable", request.IsScannable, DbType.Boolean);
            parameters.Add("@ScanCode", DbParameterValue.StringOrNull(request.ScanCode), DbType.AnsiString, size: 100);
            parameters.Add("@CapacityMode", request.CapacityMode, DbType.AnsiString, size: 30);
            parameters.Add("@CapacityQty", request.CapacityQty, DbType.Decimal, precision: 18, scale: 4);
            parameters.Add("@MaxWeightKg", request.MaxWeightKg, DbType.Decimal, precision: 18, scale: 4);
            parameters.Add("@PlantPolicy", request.PlantPolicy, DbType.AnsiString, size: 20);
            parameters.Add("@DedicatedPlantCode", DbParameterValue.StringOrNull(request.DedicatedPlantCode), DbType.AnsiString, size: 20);
            parameters.Add("@AllowMixedMaterial", request.AllowMixedMaterial, DbType.Boolean);
            parameters.Add("@AllowMixedBatch", request.AllowMixedBatch, DbType.Boolean);
            parameters.Add("@AllowMixedOrder", request.AllowMixedOrder, DbType.Boolean);
            parameters.Add("@PutawayPriority", request.PutawayPriority, DbType.Int32);
            parameters.Add("@PickingPriority", request.PickingPriority, DbType.Int32);
            parameters.Add("@RouteSequence", request.RouteSequence, DbType.Int32);
            parameters.Add("@MasterStatus", request.MasterStatus, DbType.AnsiString, size: 20);
            parameters.Add("@Remark", DbParameterValue.StringOrNull(request.Remark), DbType.String, size: 500);
            parameters.Add("@ModifyUser", DbParameterValue.StringOrNull(request.Operator), DbType.AnsiString, size: 50);

            return parameters;
        }
    }
}
