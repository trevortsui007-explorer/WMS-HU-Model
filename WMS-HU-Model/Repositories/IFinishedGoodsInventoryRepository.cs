using WMS_HU_Model.Models.Inventory;

namespace WMS_HU_Model.Repositories
{
    public interface IFinishedGoodsInventoryRepository
    {
        FinishedGoodsInventoryResultDto Query(FinishedGoodsInventoryQuery query);
        FinishedGoodsInventoryPreviewDto GetPreview(string boxCode);
    }
}
