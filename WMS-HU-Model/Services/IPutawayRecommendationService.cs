using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.Putaway;

namespace WMS_HU_Model.Services
{
    public interface IPutawayRecommendationService
    {
        OperationResult<PutawayRecommendationResultDto> GetRecommendations(string warehouseCode, string boxHuCode);
    }
}
