using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.Inbound;

namespace WMS_HU_Model.Services
{
    public interface IFinishedGoodsInboundReadService
    {
        OperationResult<FinishedGoodsInboundRecordResultDto> Query(FinishedGoodsInboundRecordQuery query);
        OperationResult<FinishedGoodsInboundDetailDto> GetDetail(long receiptId);
    }
}
