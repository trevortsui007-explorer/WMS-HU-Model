using System.Collections.Generic;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.MasterData;

namespace WMS_HU_Model.Services
{
    public interface ILocationService
    {
        List<LocationDto> GetList(
            string warehouseCode,
            string zoneCode);

        List<LocationDto> GetMaintenanceList(LocationMaintenanceQuery query);

        OperationResult<LocationDto> Get(
            string warehouseCode,
            string locationCode);

        OperationResult<LocationDto> Create(
            CreateLocationRequest request);

        OperationResult<LocationDto> Update(
            string warehouseCode,
            string locationCode,
            UpdateLocationRequest request);

        OperationResult<LocationDto> SetActive(string warehouseCode, string locationCode, bool isActive, MasterDataStatusRequest request);
    }
}
