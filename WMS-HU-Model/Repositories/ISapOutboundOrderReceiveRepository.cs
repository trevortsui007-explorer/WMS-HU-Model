using WMS_HU_Model.Models.Outbound.Integration;

namespace WMS_HU_Model.Repositories
{
    public interface ISapOutboundOrderReceiveRepository
    {
        SapOutboundOrderReceiveAttemptResult TryHandleExisting(string sapDeliveryNo, string payloadHash, string rawJson);
        SapOutboundOrderReceiveAttemptResult CreateFirst(SapOutboundOrderReceiveCommand command);
        void RecordRejected(string sapDeliveryNo, string payloadHash, string rawJson, string message);
    }
}
