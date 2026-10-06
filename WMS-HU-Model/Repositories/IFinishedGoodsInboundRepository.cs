using WMS_HU_Model.Models.Inbound;

namespace WMS_HU_Model.Repositories
{
    public interface IFinishedGoodsInboundRepository
    {
        FinishedGoodsInboundPersistenceResult FindExisting(FinishedGoodsInboundCommand command);
        FinishedGoodsInboundPersistenceResult Persist(FinishedGoodsInboundCommand command);
    }
}
