using Nancy;
using Nancy.ModelBinding;
using System;
using System.Collections.Generic;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.Outbound;
using WMS_HU_Model.Models.Outbound.Formal;
using WMS_HU_Model.Models.Outbound.Integration;
using WMS_HU_Model.Repositories;
using WMS_HU_Model.Services;
using WMS.Infrastructure.Sap;

namespace WMS.Modules
{
    public class OutboundModule : NancyModule
    {
        private readonly IOutboundService _service;
        private readonly IFormalOutboundService _formalService;
        private readonly ISapOutboundPostingService _sapPostingService;

        public OutboundModule()
            : base("/api/wms/outbound")
        {
            IOutboundRepository repository = new OutboundRepository();

            _service = new OutboundService(repository);
            _formalService = new FormalOutboundService(
                new FormalOutboundRepository(),
                new HuIdentifierService(new HuIdentifierRepository()));
            _sapPostingService = new SapOutboundPostingService(
                new SapOutboundPostingRepository(),
                new S0089SapOutboundPostingClient());

            Get("/orders", _ =>
            {
                OutboundOrderQuery query = this.Bind<OutboundOrderQuery>();
                OperationResult<List<OutboundOrderDto>> result = _formalService.GetOrders(query);
                return result.IsSuccess
                    ? Response.AsJson(ApiResult.Success(result.Data))
                    : Response.AsJson(ApiResult.Fail(result.Message));
            });

            Get("/orders/{vbeln}", args =>
            {
                object rawVbeln = args.vbeln;
                string vbeln = Convert.ToString(rawVbeln);
                OperationResult<OutboundOrderDto> result = _formalService.GetOrder(vbeln);
                return result.IsSuccess
                    ? Response.AsJson(ApiResult.Success(result.Data))
                    : Response.AsJson(ApiResult.Fail(result.Message));
            });

            Get("/orders/{vbeln}/picking-state", args =>
            {
                object rawVbeln = args.vbeln;
                string vbeln = Convert.ToString(rawVbeln);
                OperationResult<OutboundPickingStateDto> result = _formalService.GetPickingState(vbeln);
                return result.IsSuccess
                    ? Response.AsJson(ApiResult.Success(result.Data))
                    : Response.AsJson(ApiResult.Fail(result.Message));
            });

            Post("/orders/{vbeln}/preview", args =>
            {
                object rawVbeln = args.vbeln;
                string vbeln = Convert.ToString(rawVbeln);
                OperationResult<OutboundPreviewDto> result = _formalService.Preview(vbeln);
                return result.IsSuccess
                    ? Response.AsJson(ApiResult.Success(result.Data))
                    : Response.AsJson(ApiResult.Fail(result.Message));
            });

            Post("/orders/{vbeln}/start-picking", args =>
            {
                object rawVbeln = args.vbeln;
                string vbeln = Convert.ToString(rawVbeln);
                StartPickingRequest request = this.Bind<StartPickingRequest>();
                OperationResult<StartPickingResultDto> result = _formalService.StartPicking(vbeln, request);
                return result.IsSuccess
                    ? Response.AsJson(ApiResult.Success(result.Data, result.Message))
                    : Response.AsJson(ApiResult.Fail(result.Message));
            });

            Post("/orders/{vbeln}/void", args =>
            {
                object rawVbeln = args.vbeln;
                string vbeln = Convert.ToString(rawVbeln);
                OutboundActionRequest request = this.Bind<OutboundActionRequest>();
                OperationResult<OutboundActionResultDto> result = _formalService.VoidOrder(vbeln, request);
                return result.IsSuccess
                    ? Response.AsJson(ApiResult.Success(result.Data, result.Message))
                    : Response.AsJson(ApiResult.Fail(result.Message));
            });

            Post("/allocations/{allocationNo}/release", args =>
            {
                object rawAllocationNo = args.allocationNo;
                string allocationNo = Convert.ToString(rawAllocationNo);
                OutboundActionRequest request = this.Bind<OutboundActionRequest>();
                OperationResult<OutboundActionResultDto> result = _formalService.ReleaseAllocation(allocationNo, request);
                return result.IsSuccess
                    ? Response.AsJson(ApiResult.Success(result.Data, result.Message))
                    : Response.AsJson(ApiResult.Fail(result.Message));
            });

            Post("/pick-batches/{pickBatchNo}/scan", args =>
            {
                object rawPickBatchNo = args.pickBatchNo;
                string pickBatchNo = Convert.ToString(rawPickBatchNo);
                OutboundScanRequest request = this.Bind<OutboundScanRequest>();
                OperationResult<OutboundScanResultDto> result = _formalService.Scan(pickBatchNo, request);
                return result.IsSuccess
                    ? Response.AsJson(ApiResult.Success(result.Data, result.Message))
                    : Response.AsJson(ApiResult.Fail(result.Message));
            });

            Post("/orders/{vbeln}/ship", args =>
            {
                object rawVbeln = args.vbeln;
                string vbeln = Convert.ToString(rawVbeln);
                ShipOutboundRequest request = this.Bind<ShipOutboundRequest>();
                OperationResult<OutboundShippingStateDto> result = _sapPostingService.ConfirmShipment(
                    vbeln,
                    new SapOutboundPostingControlRequest
                    {
                        RequestId = request.RequestId,
                        Operator = request.Operator,
                        Remark = request.Remark
                    });
                return result.IsSuccess
                    ? Response.AsJson(ApiResult.Success(result.Data, result.Message))
                    : PostingFailure(result.Message);
            });

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

            Post("/pick-details/{detailId}/return", args =>
            {
                object rawDetailId = args.detailId;
                long detailId;
                if (!long.TryParse(Convert.ToString(rawDetailId), out detailId))
                    return Response.AsJson(ApiResult.Fail("detailId格式无效。"));

                ReturnPickDetailRequest request = this.Bind<ReturnPickDetailRequest>();
                OperationResult<OutboundActionResultDto> result = _formalService.ReturnPickDetail(detailId, request);
                return result.IsSuccess
                    ? Response.AsJson(ApiResult.Success(result.Data, result.Message))
                    : Response.AsJson(ApiResult.Fail(result.Message));
            });

            Post("/returned/{huCode}/putaway", args =>
            {
                object rawHuCode = args.huCode;
                string huCode = Convert.ToString(rawHuCode);
                ReturnToStockRequest request = this.Bind<ReturnToStockRequest>();
                OperationResult<OutboundActionResultDto> result = _formalService.ReturnToStock(huCode, request);
                return result.IsSuccess
                    ? Response.AsJson(ApiResult.Success(result.Data, result.Message))
                    : Response.AsJson(ApiResult.Fail(result.Message));
            });

            Post("/pick-batches/{pickBatchNo}/replace", args =>
            {
                object rawPickBatchNo = args.pickBatchNo;
                string pickBatchNo = Convert.ToString(rawPickBatchNo);
                ReplacementRequest request = this.Bind<ReplacementRequest>();
                OperationResult<StartPickingResultDto> result = _formalService.ReserveReplacement(
                    pickBatchNo,
                    request);
                return result.IsSuccess
                    ? Response.AsJson(ApiResult.Success(result.Data, result.Message))
                    : Response.AsJson(ApiResult.Fail(result.Message));
            });

            //----------------------------------------------------
            // 1. FIFO队列预览
            //----------------------------------------------------
            Get("/queue", _ =>
            {
                OutboundQueueQuery query = this.Bind<OutboundQueueQuery>();

                // 兜底默认的分页大小 (如果前端未传)
                if (query.Top <= 0)
                {
                    query.Top = 20;
                }

                OperationResult<List<OutboundQueueItemDto>> result = _service.GetQueue(query);

                if (!result.IsSuccess)
                {
                    return Response.AsJson(ApiResult.Fail(result.Message));
                }

                return Response.AsJson(ApiResult.Success(result.Data));
            });

            //----------------------------------------------------
            // 2. 拣FIFO队头
            //----------------------------------------------------
            Post("/pick-next", _ =>
            {
                return Response.AsJson(ApiResult.Fail(
                    "pick-next验证写入口已停用，请使用正式订单StartPicking与Scan流程。",
                    410));
            });

            //----------------------------------------------------
            // FIFO 多箱规划
            //----------------------------------------------------
            Post("/plan", _ =>
            {
                OutboundBatchPlanRequest request = this.Bind<OutboundBatchPlanRequest>();

                OperationResult<OutboundBatchPlanDto> result = _service.PlanBatch(request);

                if (!result.IsSuccess)
                {
                    return Response.AsJson(ApiResult.Fail(result.Message));
                }

                return Response.AsJson(ApiResult.Success(result.Data));
            });

            //----------------------------------------------------
            // FIFO 多箱实际拣货
            //----------------------------------------------------
            Post("/pick-batch", _ =>
            {
                return Response.AsJson(ApiResult.Fail(
                    "pick-batch验证写入口已停用，请使用正式订单StartPicking与Scan流程。",
                    410));
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
