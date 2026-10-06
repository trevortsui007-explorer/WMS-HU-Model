using System.Collections.Generic;
using WMS_HU_Model.Models.Outbound.Formal;
namespace WMS_HU_Model.Repositories
{
    public interface IOutboundOrderRepository
    {
        List<OutboundOrderDto> GetOrders(OutboundOrderQuery query);
        OutboundOrderDto GetOrder(string sapDeliveryNo);
        OutboundActionPersistenceResult VoidOrder(string sapDeliveryNo, OutboundActionRequest request);
        OutboundPickingStateReadResult GetPickingState(string sapDeliveryNo);
    }
}

