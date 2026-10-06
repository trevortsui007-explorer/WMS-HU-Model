using System.Collections.Generic;
using WMS_HU_Model.Models.Audit;

namespace WMS_HU_Model.Repositories
{
    public interface IAuditRepository
    {
        AuditPageResult<OperationAuditRowDto> QueryOperations(OperationAuditQuery query);
        AuditPageResult<MoveAuditRowDto> QueryMoves(MoveAuditQuery query);
        AuditPageResult<HoldAuditRowDto> QueryHolds(HoldAuditQuery query);
        List<OperationAuditRowDto> GetOperationsByRequestId(string requestId, int top);
        List<MoveAuditRowDto> GetMovesByRequestId(string requestId, int top);
        List<HoldAuditRowDto> GetHoldsByRequestId(string requestId, int top);
    }
}
