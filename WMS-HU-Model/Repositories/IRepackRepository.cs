using WMS_HU_Model.Models.Repack;

namespace WMS_HU_Model.Repositories
{
    public interface IRepackRepository
    {
        BoxRepackStatusDto GetBoxStatus(
            long boxHuId);

        TransferInnerResultDto GetTransferByRequestId(
            string requestId);

        TransferInnerPersistenceResult ExecuteTransferInner(
            TransferInnerCommand command);

        UnpackInnerResultDto GetUnpackByRequestId(
            string requestId);

        UnpackInnerPersistenceResult ExecuteUnpackInner(
            UnpackInnerCommand command);

        PackInnerResultDto GetPackByRequestId(
            string requestId);

        PackInnerPersistenceResult ExecutePackInner(
            PackInnerCommand command);

        UnpackBoxResultDto GetUnpackBoxByRequestId(string requestId);

        UnpackBoxPersistenceResult ExecuteUnpackBox(UnpackBoxCommand command);
    }
}
