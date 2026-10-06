using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.HU;

namespace WMS_HU_Model.Services
{
    public interface IHuHoldService
    {
        OperationResult<HuHoldResultDto> Hold(string huCode, HoldHuRequest request);
        OperationResult<HuHoldResultDto> Release(string huCode, ReleaseHuRequest request);
    }
}
