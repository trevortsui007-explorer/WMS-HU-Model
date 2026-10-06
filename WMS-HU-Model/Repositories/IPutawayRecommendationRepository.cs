using WMS_HU_Model.Models.Putaway;

namespace WMS_HU_Model.Repositories
{
    public interface IPutawayRecommendationRepository
    {
        PutawayRecommendationSnapshot LoadSnapshot(string warehouseCode, string boxHuCode);
    }
}
