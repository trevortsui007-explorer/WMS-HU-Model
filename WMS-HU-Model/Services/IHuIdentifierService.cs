using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.HU;

namespace WMS_HU_Model.Services
{
    public interface IHuIdentifierService
    {
        OperationResult<HuIdentityDto> Resolve(string rawValue, string customerCode);
    }
}
