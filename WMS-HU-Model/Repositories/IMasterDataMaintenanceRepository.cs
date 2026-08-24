using System.Collections.Generic;
using WMS_HU_Model.Models.MasterData;

namespace WMS_HU_Model.Repositories
{
    public interface IMasterDataMaintenanceRepository
    {
        List<WarehouseDto> QueryWarehouses(WarehouseQuery query);
        WarehouseDto GetWarehouse(string warehouseCode);
        bool WarehouseExists(string warehouseCode);
        long InsertWarehouse(WarehouseCreateRequest request);
        bool UpdateWarehouse(string warehouseCode, WarehouseUpdateRequest request);
        bool SetWarehouseActive(string warehouseCode, bool isActive, string operatorName);
        bool HasActiveLocations(string warehouseCode);
        bool HasCurrentHus(string warehouseCode);
        bool HasActiveMappings(string warehouseCode);
        List<WarehousePlantDto> QueryWarehousePlants(WarehousePlantQuery query);
        WarehousePlantDto GetWarehousePlant(string warehouseCode, string plantCode);
        bool WarehousePlantExists(string warehouseCode, string plantCode);
        long InsertWarehousePlant(WarehousePlantCreateRequest request);
        bool UpdateWarehousePlant(string warehouseCode, string plantCode, WarehousePlantUpdateRequest request);
        bool SetWarehousePlantActive(string warehouseCode, string plantCode, bool isActive);
        List<MasterDataOptionDto> GetWarehouseOptions();
        List<MasterDataOptionDto> GetZoneOptions(string warehouseCode);
    }
}
