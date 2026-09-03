using WMS_HU_Model.Models.Outbound.Integration;

namespace WMS_HU_Model.Repositories
{
    public interface ISapOutboundPostingRepository
    {
        SapPostingSnapshotBuildResult BuildPreview(string sapDeliveryNo);
        SapPostingClaimResult ClaimInitial(string sapDeliveryNo, SapOutboundPostingControlRequest request, SapOutboundPostingPreviewDto preview);
        SapPostingClaimResult ClaimRetry(string sapDeliveryNo, SapOutboundPostingControlRequest request);
        OutboundShippingStateDto FinalizeAttempt(long postingId, int attemptNo, string requestId, SapOutboundTransportResult transport, string operatorValue, string remark);
        OutboundShippingStateDto GetShippingState(string sapDeliveryNo);
        OutboundShippingStateDto ManualConfirmPosted(string sapDeliveryNo, SapOutboundManualConfirmRequest request);
        OutboundShippingStateDto ManualConfirmNotPosted(string sapDeliveryNo, SapOutboundManualConfirmRequest request);
        PagedResult<OutboundShippingStateDto> QueryShippingOrders(string sapDeliveryNo, string outboundStatus, string postingStatus, int page, int pageSize);
        ShipmentManagementResultDto QueryShipmentManagement(ShipmentManagementQuery query);
        ShipmentManagementDetailDto GetShipmentManagementDetail(string sapDeliveryNo);
        PagedResult<SapIntegrationLogDto> QueryLogs(SapIntegrationLogQuery query);
        SapIntegrationLogDto GetLog(long id);
        void RecordReceiveCommunication(string businessKey, string requestJson, string resultStatus, string externalMessage, string payloadHash);
    }
}
