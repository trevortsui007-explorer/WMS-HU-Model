using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.Repack;

namespace WMS_HU_Model.Services
{
    public interface IRepackService
    {
        OperationResult<BoxRepackStatusDto>
            GetBoxStatus(
                string boxHuCode);

        OperationResult<TransferInnerResultDto>
            TransferInner(
                TransferInnerRequest request);

        OperationResult<UnpackInnerResultDto>
            UnpackInner(
                UnpackInnerRequest request);

        OperationResult<PackInnerResultDto>
            PackInner(
                PackInnerRequest request);

        OperationResult<UnpackBoxResultDto> UnpackBox(UnpackBoxRequest request);

        OperationResult<MergeBoxResultDto> MergeBox(MergeBoxRequest request);
    }
}
