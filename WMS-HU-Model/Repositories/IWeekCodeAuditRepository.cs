using System.Collections.Generic;
using WMS_HU_Model.Models.WeekCode;

namespace WMS_HU_Model.Repositories
{
    public interface IWeekCodeAuditRepository
    {
        List<WeekCodeAuditFact> QueryFacts(WeekCodeAuditQuery query, int maxRows);
    }
}
