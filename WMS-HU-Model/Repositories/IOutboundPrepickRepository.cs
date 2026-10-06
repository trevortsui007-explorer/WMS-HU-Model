using System.Collections.Generic;
using WMS_HU_Model.Models.Outbound.Formal;
namespace WMS_HU_Model.Repositories
{
    public interface IOutboundPrepickRepository
    {
        List<PrepickWarehouse> ReadWarehouses(string plantCode);
        bool HasWork(long orderId);
        List<PrepickStockRow> ReadStock(OutboundOrderDto order, long warehouseId);
    }
}
