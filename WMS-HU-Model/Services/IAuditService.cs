using System.Collections.Generic;
using WMS_HU_Model.Models.Audit;
using WMS_HU_Model.Models.Common;

namespace WMS_HU_Model.Services
{
    public interface IAuditService
    {
        OperationResult<AuditPageResult<OperationAuditRowDto>> QueryOperations(OperationAuditQuery query);
        OperationResult<AuditPageResult<MoveAuditRowDto>> QueryMoves(MoveAuditQuery query);
        OperationResult<AuditPageResult<HoldAuditRowDto>> QueryHolds(HoldAuditQuery query);
        OperationResult<RequestAuditTraceDto> GetRequestTrace(string requestId);
        List<AuditCapabilityDto> GetCapabilities();
    }
}
