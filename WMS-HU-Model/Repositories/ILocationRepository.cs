using System.Collections.Generic;
using WMS_HU_Model.Models.MasterData;

namespace WMS_HU_Model.Repositories
{
    public interface ILocationRepository
    {
        List<LocationDto> GetList(
            string warehouseCode,
            string zoneCode);

        LocationDto GetByCode(
            string warehouseCode,
            string locationCode);

        long? GetWarehouseId(string warehouseCode);

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
            long? parentLocationId,
            CreateLocationRequest request);

        bool Update(
            long locationId,
            long zoneId,
            long? parentLocationId,
            UpdateLocationRequest request);
    }
}