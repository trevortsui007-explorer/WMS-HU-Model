using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.Outbound.Formal;
namespace WMS_HU_Model.Services
{
    public interface IOutboundPrepickService
    {
        OperationResult<OutboundPrepickDto> Preview(string sapDeliveryNo);
    }
}
