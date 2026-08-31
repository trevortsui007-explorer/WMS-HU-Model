using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.Outbound.Formal;

namespace WMS_HU_Model.Services
{
    public interface IOutboundExceptionService
    {
        OperationResult<OutboundActionResultDto> ReturnPickDetail(long pickDetailId, ReturnPickDetailRequest request);
        OperationResult<OutboundActionResultDto> ReturnToStock(string huCode, ReturnToStockRequest request);
        OperationResult<StartPickingResultDto> ReserveReplacement(string pickBatchNo, ReplacementRequest request);
    }
}
