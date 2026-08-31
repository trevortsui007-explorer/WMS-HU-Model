using System.Collections.Generic;
using WMS_HU_Model.Models.MasterData;

namespace WMS_HU_Model.Repositories
{
    public interface ILocationRepository
    {
        List<LocationDto> GetList(
            string warehouseCode,
            string zoneCode);

        List<LocationDto> GetMaintenanceList(LocationMaintenanceQuery query);

        LocationDto GetByCode(
            string warehouseCode,
            string locationCode);

        long? GetWarehouseId(string warehouseCode);

        long? GetWarehouseIdAny(string warehouseCode);

        long? GetZoneId(
            long warehouseId,
            string zoneCode);

        long? GetLocationId(
            long warehouseId,
            string locationCode);

        bool Exists(
            long warehouseId,
            string locationCode);

        long Insert(
            long warehouseId,
            long zoneId,
            CreateLocationRequest request);

        bool Update(
            long locationId,
            long zoneId,
            UpdateLocationRequest request);

        int CountCurrentHus(long locationId);

        bool SetActive(long locationId, bool isActive, string operatorName);
    }
}
