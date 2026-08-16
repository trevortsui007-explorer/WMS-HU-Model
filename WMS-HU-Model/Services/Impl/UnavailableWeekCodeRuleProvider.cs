using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.WeekCode;

namespace WMS_HU_Model.Services
{
    public class UnavailableWeekCodeRuleProvider : IWeekCodeRuleProvider
    {
        public OperationResult<WeekCodeSnapshot> Resolve(string materialCode, string materialRevision, string dateCode)
        {
            return OperationResult<WeekCodeSnapshot>.Fail("MES周码规则Provider尚未配置，不能固化DateCode。");
        }
    }
}
