using System.Collections.Generic;
using WMS_HU_Model.Models.Outbound.Formal;

namespace WMS_HU_Model.Repositories
{
    public interface IOutboundPickingRepository
    {
        List<OutboundAllocationCandidateDto> GetAllocationCandidates(OutboundOrderDto order, string materialCode, string uom);
        StartPickingResultDto GetStartPickingByRequestId(string requestId);
        StartPickingPersistenceResult ExecuteStartPicking(StartPickingCommand command);
        OutboundActionPersistenceResult ReleaseAllocation(string allocationNo, OutboundActionRequest request);
        string GetPickBatchCustomerCode(string pickBatchNo);
        OutboundScanResultDto GetScanByRequestId(string requestId);
        OutboundScanPersistenceResult ExecuteScan(OutboundScanCommand command);
    }
}
