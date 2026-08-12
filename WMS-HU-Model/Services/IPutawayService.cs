using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.Putaway;

namespace WMS_HU_Model.Services
{
    public interface IPutawayService
    {
        OperationResult<PalletPutawayStatusDto>
            GetPalletStatus(
                string palletHuCode);

        OperationResult<PutawayResultDto>
            PutawayBox(
                PutawayBoxRequest request);
    }
}
