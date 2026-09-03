using System;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.Outbound.Integration;
using WMS_HU_Model.Repositories;

namespace WMS_HU_Model.Services
{
    public class SapOutboundPostingService : ISapOutboundPostingService
    {
        private readonly ISapOutboundPostingRepository _repository;
        private readonly ISapOutboundPostingClient _client;

        public SapOutboundPostingService(ISapOutboundPostingRepository repository, ISapOutboundPostingClient client)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public OperationResult<SapOutboundPostingPreviewDto> Preview(string sapDeliveryNo)
        {
            string deliveryNo = Normalize(sapDeliveryNo);
            if (deliveryNo == null)
                return OperationResult<SapOutboundPostingPreviewDto>.Fail("VBELN不能为空。");
            SapPostingSnapshotBuildResult result = _repository.BuildPreview(deliveryNo);
            return result.IsSuccess
                ? OperationResult<SapOutboundPostingPreviewDto>.Success(result.Preview)
                : OperationResult<SapOutboundPostingPreviewDto>.Fail(result.Message);
        }

        public OperationResult<OutboundShippingStateDto> ConfirmShipment(string sapDeliveryNo, SapOutboundPostingControlRequest request)
        {
            string deliveryNo = Normalize(sapDeliveryNo);
            string error = ValidateControl(deliveryNo, request, false);
            if (error != null)
                return OperationResult<OutboundShippingStateDto>.Fail(error);

            SapOutboundTransportConfigurationResult configuration = _client.ValidateConfiguration();
            if (!configuration.IsValid)
                return OperationResult<OutboundShippingStateDto>.Fail("SAP_OUTBOUND_CONFIGURATION_ERROR: " + configuration.Message);

            SapPostingSnapshotBuildResult snapshot = _repository.BuildPreview(deliveryNo);
            if (!snapshot.IsSuccess)
                return OperationResult<OutboundShippingStateDto>.Fail(snapshot.Message);

            SapPostingClaimResult claim = _repository.ClaimInitial(deliveryNo, request, snapshot.Preview);
            return ExecuteClaim(claim, configuration.Configuration, request);
        }

        public OperationResult<OutboundShippingStateDto> Retry(string sapDeliveryNo, SapOutboundPostingControlRequest request)
        {
            string deliveryNo = Normalize(sapDeliveryNo);
            string error = ValidateControl(deliveryNo, request, false);
            if (error != null)
                return OperationResult<OutboundShippingStateDto>.Fail(error);

            SapOutboundTransportConfigurationResult configuration = _client.ValidateConfiguration();
            if (!configuration.IsValid)
                return OperationResult<OutboundShippingStateDto>.Fail("SAP_OUTBOUND_CONFIGURATION_ERROR: " + configuration.Message);

            SapPostingClaimResult claim = _repository.ClaimRetry(deliveryNo, request);
            return ExecuteClaim(claim, configuration.Configuration, request);
        }

        public OperationResult<OutboundShippingStateDto> GetShippingState(string sapDeliveryNo)
        {
            string deliveryNo = Normalize(sapDeliveryNo);
            if (deliveryNo == null)
                return OperationResult<OutboundShippingStateDto>.Fail("VBELN不能为空。");
            OutboundShippingStateDto state = _repository.GetShippingState(deliveryNo);
            return state == null
                ? OperationResult<OutboundShippingStateDto>.Fail("出库订单不存在。")
                : OperationResult<OutboundShippingStateDto>.Success(state);
        }

        public OperationResult<OutboundShippingStateDto> ManualConfirmPosted(string sapDeliveryNo, SapOutboundManualConfirmRequest request)
        {
            string deliveryNo = Normalize(sapDeliveryNo);
            string error = ValidateControl(deliveryNo, request, true);
            if (error != null)
                return OperationResult<OutboundShippingStateDto>.Fail(error);
            try
            {
                return OperationResult<OutboundShippingStateDto>.Success(_repository.ManualConfirmPosted(deliveryNo, request));
            }
            catch (InvalidOperationException ex)
            {
                return OperationResult<OutboundShippingStateDto>.Fail(ex.Message);
            }
        }

        public OperationResult<OutboundShippingStateDto> ManualConfirmNotPosted(string sapDeliveryNo, SapOutboundManualConfirmRequest request)
        {
            string deliveryNo = Normalize(sapDeliveryNo);
            string error = ValidateControl(deliveryNo, request, true);
            if (error != null)
                return OperationResult<OutboundShippingStateDto>.Fail(error);
            try
            {
                return OperationResult<OutboundShippingStateDto>.Success(_repository.ManualConfirmNotPosted(deliveryNo, request));
            }
            catch (InvalidOperationException ex)
            {
                return OperationResult<OutboundShippingStateDto>.Fail(ex.Message);
            }
        }

        public OperationResult<PagedResult<OutboundShippingStateDto>> QueryShippingOrders(string sapDeliveryNo, string outboundStatus, string postingStatus, int page, int pageSize)
        {
            page = page <= 0 ? 1 : page;
            pageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, 100);
            return OperationResult<PagedResult<OutboundShippingStateDto>>.Success(
                _repository.QueryShippingOrders(Normalize(sapDeliveryNo), NormalizeUpper(outboundStatus), NormalizeUpper(postingStatus), page, pageSize));
        }

        public OperationResult<ShipmentManagementResultDto> QueryShipmentManagement(ShipmentManagementQuery query)
        {
            query = query ?? new ShipmentManagementQuery();
            query.SapDeliveryNo = Normalize(query.SapDeliveryNo);
            query.CustomerCode = Normalize(query.CustomerCode);
            query.MaterialCode = Normalize(query.MaterialCode);
            query.PlantCode = NormalizeUpper(query.PlantCode);
            query.WarehouseCode = NormalizeUpper(query.WarehouseCode);
            query.BusinessStatus = NormalizeUpper(query.BusinessStatus);
            query.SapPostingStatus = NormalizeUpper(query.SapPostingStatus);
            query.Page = query.Page <= 0 ? 1 : query.Page;
            query.PageSize = query.PageSize <= 0 ? 20 : Math.Min(query.PageSize, 100);
            ShipmentManagementResultDto result = _repository.QueryShipmentManagement(query);
            foreach (ShipmentManagementRowDto row in result.Items)
                ApplyBusinessPresentation(row);
            return OperationResult<ShipmentManagementResultDto>.Success(result);
        }

        public OperationResult<ShipmentManagementDetailDto> GetShipmentManagementDetail(string sapDeliveryNo)
        {
            string deliveryNo = Normalize(sapDeliveryNo);
            if (deliveryNo == null)
                return OperationResult<ShipmentManagementDetailDto>.Fail("VBELN不能为空。");
            ShipmentManagementDetailDto detail = _repository.GetShipmentManagementDetail(deliveryNo);
            if (detail == null)
                return OperationResult<ShipmentManagementDetailDto>.Fail("出库订单不存在。");
            detail.Posting = detail.Posting ?? new ShipmentPostingBusinessDto();
            detail.Posting.PostingStatusText = PostingStatusText(detail.Posting.PostingStatus);
            detail.Posting.RetryAllowedOnShipmentPage = false;
            return OperationResult<ShipmentManagementDetailDto>.Success(detail);
        }

        private static void ApplyBusinessPresentation(ShipmentManagementRowDto row)
        {
            row.PickingStatusText = PickingStatusText(row.OutboundStatus);
            row.PostingStatusText = PostingStatusText(row.SapPostingStatus);
            if (row.OutboundStatus == "SHIPPED" || row.SapPostingStatus == SapOutboundPostingStatuses.Success)
            {
                row.BusinessStatusCode = "SHIPPED"; row.BusinessStatusText = "已出货";
            }
            else if (row.SapPostingStatus == SapOutboundPostingStatuses.Failed || row.SapPostingStatus == SapOutboundPostingStatuses.Unknown)
            {
                row.BusinessStatusCode = "SAP_EXCEPTION"; row.BusinessStatusText = "SAP异常";
            }
            else if (row.OutboundStatus == "PICKED" && row.SapPostingStatus == SapOutboundPostingStatuses.Sending)
            {
                row.BusinessStatusCode = "SAP_PROCESSING"; row.BusinessStatusText = "SAP过账中";
            }
            else if (row.OutboundStatus == "PICKED")
            {
                row.BusinessStatusCode = "WAIT_CONFIRM"; row.BusinessStatusText = "待确认出货";
            }
            else if (row.OutboundStatus == "WAIT_PICK")
            {
                row.BusinessStatusCode = "WAIT_PICK"; row.BusinessStatusText = "待拣货";
            }
            else
            {
                row.BusinessStatusCode = "PICKING"; row.BusinessStatusText = row.OutboundStatus == "EXCEPTION" ? "拣货异常" : "拣货中";
            }
            row.CanConfirmShipment = row.OutboundStatus == "PICKED"
                && (string.IsNullOrEmpty(row.SapPostingStatus) || row.SapPostingStatus == SapOutboundPostingStatuses.Ready);
            row.RequiresITReview = row.SapPostingStatus == SapOutboundPostingStatuses.Failed || row.SapPostingStatus == SapOutboundPostingStatuses.Unknown;
        }

        private static string PickingStatusText(string status)
        {
            switch (status)
            {
                case "WAIT_PICK": return "待拣货"; case "ALLOCATED": return "已分配"; case "PICKING": return "拣货中";
                case "PICKED": return "已拣货"; case "EXCEPTION": return "拣货异常"; case "SHIPPED": return "已出货"; case "VOID": return "已作废";
                default: return status ?? "—";
            }
        }

        private static string PostingStatusText(string status)
        {
            switch (status)
            {
                case "READY": return "待SAP过账"; case "SENDING": return "SAP过账中"; case "SUCCESS": return "已完成SAP过账";
                case "FAILED": return "SAP过账失败"; case "UNKNOWN": return "SAP状态待确认"; default: return status ?? "待提交";
            }
        }

        public OperationResult<PagedResult<SapIntegrationLogDto>> QueryLogs(SapIntegrationLogQuery query)
        {
            query = query ?? new SapIntegrationLogQuery();
            query.BusinessKey = Normalize(query.BusinessKey);
            query.InterfaceCode = NormalizeUpper(query.InterfaceCode);
            query.Direction = NormalizeUpper(query.Direction);
            query.ResultStatus = NormalizeUpper(query.ResultStatus);
            query.Page = query.Page <= 0 ? 1 : query.Page;
            query.PageSize = query.PageSize <= 0 ? 20 : Math.Min(query.PageSize, 100);
            return OperationResult<PagedResult<SapIntegrationLogDto>>.Success(_repository.QueryLogs(query));
        }

        public OperationResult<SapIntegrationLogDto> GetLog(long id)
        {
            if (id <= 0)
                return OperationResult<SapIntegrationLogDto>.Fail("日志Id无效。");
            SapIntegrationLogDto log = _repository.GetLog(id);
            return log == null
                ? OperationResult<SapIntegrationLogDto>.Fail("SAP接口日志不存在。")
                : OperationResult<SapIntegrationLogDto>.Success(log);
        }

        private OperationResult<OutboundShippingStateDto> ExecuteClaim(
            SapPostingClaimResult claim,
            SapOutboundTransportConfiguration configuration,
            SapOutboundPostingControlRequest request)
        {
            if (!claim.IsSuccess)
                return OperationResult<OutboundShippingStateDto>.Fail(claim.Message);
            if (!claim.ShouldInvokeSap)
                return OperationResult<OutboundShippingStateDto>.Success(claim.State, claim.Message);

            SapOutboundTransportResult transport = _client.Post(claim.PayloadJson, configuration);
            OutboundShippingStateDto state = _repository.FinalizeAttempt(
                claim.PostingId, claim.AttemptNo, request.RequestId, transport, request.Operator, request.Remark);
            return OperationResult<OutboundShippingStateDto>.Success(state, transport.SapMessage ?? transport.ResultStatus);
        }

        private static string ValidateControl(string deliveryNo, SapOutboundPostingControlRequest request, bool requireRemark)
        {
            if (deliveryNo == null)
                return "VBELN不能为空。";
            if (deliveryNo.Length > 30)
                return "VBELN长度不能超过30。";
            if (request == null)
                return "请求不能为空。";
            request.RequestId = Normalize(request.RequestId);
            request.Operator = Normalize(request.Operator);
            request.Remark = Normalize(request.Remark);
            if (request.RequestId == null || request.Operator == null)
                return "RequestId和Operator不能为空。";
            if (request.RequestId.Length > 100 || request.Operator.Length > 50)
                return "RequestId或Operator长度超出限制。";
            if (request.Remark != null && request.Remark.Length > 500)
                return "Remark长度不能超过500。";
            if (requireRemark && request.Remark == null)
                return "ManualConfirmRemark不能为空。";
            return null;
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static string NormalizeUpper(string value)
        {
            string normalized = Normalize(value);
            return normalized == null ? null : normalized.ToUpperInvariant();
        }
    }
}
