using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.Outbound.Integration;

namespace WMS_HU_Model.Services
{
    public interface ISapOutboundPostingService
    {
        OperationResult<SapOutboundPostingPreviewDto> Preview(string sapDeliveryNo);
        OperationResult<OutboundShippingStateDto> ConfirmShipment(string sapDeliveryNo, SapOutboundPostingControlRequest request);
        OperationResult<OutboundShippingStateDto> Retry(string sapDeliveryNo, SapOutboundPostingControlRequest request);
        OperationResult<OutboundShippingStateDto> GetShippingState(string sapDeliveryNo);
        OperationResult<OutboundShippingStateDto> ManualConfirmPosted(string sapDeliveryNo, SapOutboundManualConfirmRequest request);
        OperationResult<OutboundShippingStateDto> ManualConfirmNotPosted(string sapDeliveryNo, SapOutboundManualConfirmRequest request);
        OperationResult<PagedResult<OutboundShippingStateDto>> QueryShippingOrders(string sapDeliveryNo, string outboundStatus, string postingStatus, int page, int pageSize);
        OperationResult<ShipmentManagementResultDto> QueryShipmentManagement(ShipmentManagementQuery query);
        OperationResult<ShipmentManagementDetailDto> GetShipmentManagementDetail(string sapDeliveryNo);
        OperationResult<PagedResult<SapIntegrationLogDto>> QueryLogs(SapIntegrationLogQuery query);
        OperationResult<SapIntegrationLogDto> GetLog(long id);
    }
}
