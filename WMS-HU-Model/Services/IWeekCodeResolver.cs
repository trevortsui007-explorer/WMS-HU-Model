using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.WeekCode;

namespace WMS_HU_Model.Services
{
    public interface IWeekCodeResolver
    {
        OperationResult<WeekCodeSnapshot> Resolve(string materialCode, string materialRevision, string dateCode);
    }
}
