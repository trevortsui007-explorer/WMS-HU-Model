using WMS_HU_Model.Models.Outbound.Formal;

namespace WMS_HU_Model.Repositories
{
    public interface IOutboundExceptionRepository
    {
        OutboundActionPersistenceResult ReturnPickDetail(long pickDetailId, ReturnPickDetailRequest request);
        OutboundActionPersistenceResult ReturnToStock(string huCode, ReturnToStockRequest request);
        StartPickingPersistenceResult ReserveReplacement(string pickBatchNo, ReplacementRequest request);
    }
}
