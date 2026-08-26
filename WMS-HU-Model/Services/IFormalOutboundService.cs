using System.Collections.Generic;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.Outbound.Formal;

namespace WMS_HU_Model.Services
{
    public interface IFormalOutboundService
    {
        OperationResult<List<OutboundOrderDto>> GetOrders(OutboundOrderQuery query);
        OperationResult<OutboundOrderDto> GetOrder(string sapDeliveryNo);
        OperationResult<OutboundPreviewDto> Preview(string sapDeliveryNo);
        OperationResult<StartPickingResultDto> StartPicking(string sapDeliveryNo, StartPickingRequest request);
        OperationResult<OutboundActionResultDto> ReleaseAllocation(string allocationNo, OutboundActionRequest request);
        OperationResult<OutboundActionResultDto> VoidOrder(string sapDeliveryNo, OutboundActionRequest request);
        OperationResult<OutboundScanResultDto> Scan(string pickBatchNo, OutboundScanRequest request);
        OperationResult<OutboundActionResultDto> ShipOrder(string sapDeliveryNo, ShipOutboundRequest request);
        OperationResult<OutboundActionResultDto> ReturnPickDetail(long pickDetailId, ReturnPickDetailRequest request);
        OperationResult<OutboundActionResultDto> ReturnToStock(string huCode, ReturnToStockRequest request);
        OperationResult<StartPickingResultDto> ReserveReplacement(string pickBatchNo, ReplacementRequest request);
        OperationResult<OutboundPickingStateDto> GetPickingState(string sapDeliveryNo);
    }
}
