using WMS_HU_Model.Models.Inbound;

namespace WMS_HU_Model.Repositories
{
    public interface IFinishedGoodsInboundReadRepository
    {
        FinishedGoodsInboundRecordResultDto Query(FinishedGoodsInboundRecordQuery query);
        FinishedGoodsInboundDetailDto GetDetail(long receiptId, int boxLimit);
    }
}
