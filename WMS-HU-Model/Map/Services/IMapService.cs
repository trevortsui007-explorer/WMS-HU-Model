using System.Collections.Generic;
using System.IO;
using WMS_HU_Model.Map.Dtos;
using WMS_HU_Model.Models.Common;

namespace WMS_HU_Model.Map.Services
{
    public interface IMapService
    {
        List<MapLayoutDto> GetLayouts(string warehouseCode);
        OperationResult<MapLayoutDetailDto> GetLayout(long layoutId);
        OperationResult<MapLayoutDetailDto> GetRuntimeLayout(string warehouseCode);
        OperationResult<MapLayoutDto> CreateDraft(CreateMapLayoutRequest request);
        OperationResult<MapValidationResultDto> Validate(long layoutId);
        OperationResult<MapLayoutDetailDto> SaveObjects(long layoutId, SaveMapObjectsRequest request);
        OperationResult<MapImportResultDto> ImportExcel(long layoutId, Stream stream, string fileName, string operatorName);
    }
}
