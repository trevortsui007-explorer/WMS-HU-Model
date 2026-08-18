using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.Outbound.Integration;

namespace WMS_HU_Model.Services
{
    public interface ISapOutboundOrderReceiveService
    {
        OperationResult<SapOutboundOrderReceiveResultDto> Receive(string rawJson);
    }
}
