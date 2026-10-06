using System.Collections.Generic;
using WMS_HU_Model.Models.HU;
using WMS_HU_Model.Models.Inventory;

namespace WMS_HU_Model.Repositories
{
    public interface IInventoryQueryRepository
    {
        List<string> GetMappedWarehouses(string plantCode);
        InventoryLookupLocationRecord GetLocation(string warehouseCode, string locationCode);
        InventoryLookupLocationRecord FindLocation(string locationCode);
        HuDto GetHu(long huId);
        List<InventoryItemFact> GetLocationInventoryFacts(IList<long> boxHuIds);
        List<InventoryItemFact> GetOuterBoxFacts(long boxHuId);
        List<InventoryItemFact> GetInnerPackFacts(long packHuId);
        string GetActiveParentBoxCode(long packHuId);
    }
}
