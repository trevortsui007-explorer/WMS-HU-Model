using Nancy;
using Nancy.ModelBinding;
using System;
using System.Collections.Generic;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.Outbound.Formal;
using WMS_HU_Model.Models.Outbound.Integration;
using WMS_HU_Model.Repositories;
using WMS_HU_Model.Services;
using WMS.Infrastructure.Sap;

namespace WMS.Modules
{    public class OutboundSapPostingModule : NancyModule
    {
        private readonly ISapOutboundPostingService _sapPostingService;

        public OutboundSapPostingModule()
            : base("/api/wms/outbound")
        {
            _sapPostingService = new SapOutboundPostingService(new SapOutboundPostingRepository(), new S0089SapOutboundPostingClient());

            Post("/orders/{vbeln}/confirm-shipment", args =>
            {
                object rawVbeln = args.vbeln;
                string vbeln = Convert.ToString(rawVbeln);
                SapOutboundPostingControlRequest request = this.Bind<SapOutboundPostingControlRequest>();
                OperationResult<OutboundShippingStateDto> result = _sapPostingService.ConfirmShipment(vbeln, request);
                return result.IsSuccess
                    ? Response.AsJson(ApiResult.Success(result.Data, result.Message), HttpStatusCode.OK)
                    : PostingFailure(result.Message);
            });

            Get("/orders/{vbeln}/sap-posting-preview", args =>
            {
                object rawVbeln = args.vbeln;
                string vbeln = Convert.ToString(rawVbeln);
                OperationResult<SapOutboundPostingPreviewDto> result = _sapPostingService.Preview(vbeln);
                return result.IsSuccess
                    ? Response.AsJson(ApiResult.Success(result.Data), HttpStatusCode.OK)
                    : Response.AsJson(ApiResult.Fail(result.Message, 400), HttpStatusCode.BadRequest);
            });

            Get("/orders/{vbeln}/shipping-state", args =>
            {
                object rawVbeln = args.vbeln;
                string vbeln = Convert.ToString(rawVbeln);
                OperationResult<OutboundShippingStateDto> result = _sapPostingService.GetShippingState(vbeln);
                return result.IsSuccess
                    ? Response.AsJson(ApiResult.Success(result.Data), HttpStatusCode.OK)
                    : Response.AsJson(ApiResult.Fail(result.Message, 404), HttpStatusCode.NotFound);
            });

            Post("/orders/{vbeln}/sap-posting/retry", args =>
            {
                object rawVbeln = args.vbeln;
                string vbeln = Convert.ToString(rawVbeln);
                SapOutboundPostingControlRequest request = this.Bind<SapOutboundPostingControlRequest>();
                OperationResult<OutboundShippingStateDto> result = _sapPostingService.Retry(vbeln, request);
                return result.IsSuccess ? Response.AsJson(ApiResult.Success(result.Data, result.Message), HttpStatusCode.OK) : PostingFailure(result.Message);
            });

            Post("/orders/{vbeln}/sap-posting/manual-confirm-posted", args =>
            {
                object rawVbeln = args.vbeln;
                string vbeln = Convert.ToString(rawVbeln);
                SapOutboundManualConfirmRequest request = this.Bind<SapOutboundManualConfirmRequest>();
                OperationResult<OutboundShippingStateDto> result = _sapPostingService.ManualConfirmPosted(vbeln, request);
                return result.IsSuccess ? Response.AsJson(ApiResult.Success(result.Data, result.Message), HttpStatusCode.OK) : PostingFailure(result.Message);
            });

            Post("/orders/{vbeln}/sap-posting/manual-confirm-not-posted", args =>
            {
                object rawVbeln = args.vbeln;
                string vbeln = Convert.ToString(rawVbeln);
                SapOutboundManualConfirmRequest request = this.Bind<SapOutboundManualConfirmRequest>();
                OperationResult<OutboundShippingStateDto> result = _sapPostingService.ManualConfirmNotPosted(vbeln, request);
                return result.IsSuccess ? Response.AsJson(ApiResult.Success(result.Data, result.Message), HttpStatusCode.OK) : PostingFailure(result.Message);
            });

            Get("/shipping-orders", _ =>
            {
                object rawDelivery = Request.Query.sapDeliveryNo;
                object rawOutboundStatus = Request.Query.outboundStatus;
                object rawPostingStatus = Request.Query.postingStatus;
                object rawPage = Request.Query.page;
                object rawPageSize = Request.Query.pageSize;
                int page;
                int pageSize;
                int.TryParse(Convert.ToString(rawPage), out page);
                int.TryParse(Convert.ToString(rawPageSize), out pageSize);
                OperationResult<PagedResult<OutboundShippingStateDto>> result = _sapPostingService.QueryShippingOrders(
                    Convert.ToString(rawDelivery), Convert.ToString(rawOutboundStatus), Convert.ToString(rawPostingStatus), page, pageSize);
                return Response.AsJson(ApiResult.Success(result.Data), HttpStatusCode.OK);
            });
        }

        private object PostingFailure(string message)
        {
            if (message != null && message.StartsWith("SAP_OUTBOUND_CONFIGURATION_ERROR:", StringComparison.Ordinal))
                return Response.AsJson(ApiResult.Fail(message, 500), HttpStatusCode.InternalServerError);
            if (message != null && (message.IndexOf("并发", StringComparison.Ordinal) >= 0 || message.IndexOf("已经", StringComparison.Ordinal) >= 0 || message.IndexOf("再次发送", StringComparison.Ordinal) >= 0))
                return Response.AsJson(ApiResult.Fail(message, 409), HttpStatusCode.Conflict);
            return Response.AsJson(ApiResult.Fail(message, 400), HttpStatusCode.BadRequest);
        }
    }
}
