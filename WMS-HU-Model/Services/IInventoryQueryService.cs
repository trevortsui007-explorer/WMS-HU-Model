using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.Inventory;

namespace WMS_HU_Model.Services
{
    public interface IInventoryQueryService
    {
        OperationResult<InventoryLookupDto> Lookup(string warehouseCode, string code);
    }
}
