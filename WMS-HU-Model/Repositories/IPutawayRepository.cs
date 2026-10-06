using WMS_HU_Model.Models.Putaway;

namespace WMS_HU_Model.Repositories
{
    public interface IPutawayRepository
    {
        PalletPutawayStatusDto GetPalletStatus(
            long palletHuId);

        PutawayResultDto GetByRequestId(
            string requestId);

        PutawayPersistenceResult ExecutePutaway(
            PutawayCommand command);

        PutawayPersistenceResult ExecuteDirectReceivePutaway(DirectReceivePutawayCommand command);

        WholePalletPutawayResultDto GetWholePalletPutawayByRequestId(string requestId);

        WholePalletPutawayPersistenceResult ExecuteWholePalletPutaway(WholePalletPutawayCommand command);
    }
}
