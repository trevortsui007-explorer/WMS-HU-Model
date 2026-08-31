using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.Outbound.Formal;

namespace WMS_HU_Model.Services
{
    public interface IOutboundPickingService
    {
        OperationResult<OutboundPreviewDto> Preview(string sapDeliveryNo);
        OperationResult<StartPickingResultDto> StartPicking(string sapDeliveryNo, StartPickingRequest request);
        OperationResult<OutboundActionResultDto> ReleaseAllocation(string allocationNo, OutboundActionRequest request);
        OperationResult<OutboundScanResultDto> Scan(string pickBatchNo, OutboundScanRequest request);
    }
}
