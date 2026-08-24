using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.WeekCode;

namespace WMS_HU_Model.Services
{
    public interface IWeekCodeAuditService
    {
        OperationResult<WeekCodeAuditResultDto> Query(WeekCodeAuditQuery query);
    }
}
