using System.Collections.Generic;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.Outbound.Formal;
namespace WMS_HU_Model.Services
{
    public interface IOutboundOrderService
    {
        OperationResult<List<OutboundOrderDto>> GetOrders(OutboundOrderQuery query);
        OperationResult<OutboundOrderDto> GetOrder(string sapDeliveryNo);
        OperationResult<OutboundActionResultDto> VoidOrder(string sapDeliveryNo, OutboundActionRequest request);
        OperationResult<OutboundPickingStateDto> GetPickingState(string sapDeliveryNo);
    }
}

