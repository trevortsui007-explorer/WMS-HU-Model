using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.Inventory;

namespace WMS_HU_Model.Services
{
    public interface IFinishedGoodsInventoryService
    {
        OperationResult<FinishedGoodsInventoryResultDto> Query(FinishedGoodsInventoryQuery query);
        OperationResult<FinishedGoodsInventoryPreviewDto> GetPreview(string boxCode);
    }
}
