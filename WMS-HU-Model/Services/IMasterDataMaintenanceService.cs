using System.Collections.Generic;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.MasterData;

namespace WMS_HU_Model.Services
{
    public interface IMasterDataMaintenanceService
    {
        List<WarehouseDto> QueryWarehouses(WarehouseQuery query);
        OperationResult<WarehouseDto> GetWarehouse(string warehouseCode);
        OperationResult<WarehouseDto> CreateWarehouse(WarehouseCreateRequest request);
        OperationResult<WarehouseDto> UpdateWarehouse(string warehouseCode, WarehouseUpdateRequest request);
        OperationResult<WarehouseDto> SetWarehouseActive(string warehouseCode, bool isActive, MasterDataStatusRequest request);
        List<WarehousePlantDto> QueryWarehousePlants(WarehousePlantQuery query);
        OperationResult<WarehousePlantDto> GetWarehousePlant(string warehouseCode, string plantCode);
        OperationResult<WarehousePlantDto> CreateWarehousePlant(WarehousePlantCreateRequest request);
        OperationResult<WarehousePlantDto> UpdateWarehousePlant(string warehouseCode, string plantCode, WarehousePlantUpdateRequest request);
        OperationResult<WarehousePlantDto> SetWarehousePlantActive(string warehouseCode, string plantCode, bool isActive, MasterDataStatusRequest request);
        MasterDataOptionsDto GetOptions(string warehouseCode);
    }
}
