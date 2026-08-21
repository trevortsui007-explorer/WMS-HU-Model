using WMS_HU_Model.Models.Inbound;

namespace WMS_HU_Model.Repositories
{
    public interface IMesFinishedGoodsInboundRepository
    {
        MesFinishedGoodsInboundSourceGraph GetPlanStock(string planStockSourceId);
    }
}
