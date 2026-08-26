using System.Collections.Generic;
using WMS_HU_Model.Models.Outbound.Formal;

namespace WMS_HU_Model.Repositories
{
    public interface IFormalOutboundRepository
    {
        List<OutboundOrderDto> GetOrders(OutboundOrderQuery query);
        OutboundOrderDto GetOrder(string sapDeliveryNo);
        List<OutboundAllocationCandidateDto> GetAllocationCandidates(
            OutboundOrderDto order,
            string materialCode,
            string uom);
        StartPickingResultDto GetStartPickingByRequestId(string requestId);
        StartPickingPersistenceResult ExecuteStartPicking(StartPickingCommand command);
        OutboundActionPersistenceResult ReleaseAllocation(string allocationNo, OutboundActionRequest request);
        OutboundActionPersistenceResult VoidOrder(string sapDeliveryNo, OutboundActionRequest request);
        string GetPickBatchCustomerCode(string pickBatchNo);
        OutboundScanResultDto GetScanByRequestId(string requestId);
        OutboundScanPersistenceResult ExecuteScan(OutboundScanCommand command);
        OutboundActionPersistenceResult ShipOrder(string sapDeliveryNo, ShipOutboundRequest request);
        OutboundActionPersistenceResult ReturnPickDetail(long pickDetailId, ReturnPickDetailRequest request);
        OutboundActionPersistenceResult ReturnToStock(string huCode, ReturnToStockRequest request);
        StartPickingPersistenceResult ReserveReplacement(string pickBatchNo, ReplacementRequest request);
        OutboundPickingStateReadResult GetPickingState(string sapDeliveryNo);
    }
}
