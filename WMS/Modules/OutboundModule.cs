using Nancy;
using Nancy.ModelBinding;
using System;
using System.Collections.Generic;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.Outbound;
using WMS_HU_Model.Models.Outbound.Formal;
using WMS_HU_Model.Repositories;
using WMS_HU_Model.Services;

namespace WMS.Modules
{
    public class OutboundModule : NancyModule
    {
        private readonly IOutboundService _service;
        private readonly IFormalOutboundService _formalService;

        public OutboundModule()
            : base("/api/wms/outbound")
        {
            IOutboundRepository repository = new OutboundRepository();

            _service = new OutboundService(repository);
            _formalService = new FormalOutboundService(
                new FormalOutboundRepository(),
                new HuIdentifierService(new HuIdentifierRepository()));

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
                OperationResult<OutboundActionResultDto> result = _formalService.ShipOrder(vbeln, request);
                return result.IsSuccess
                    ? Response.AsJson(ApiResult.Success(result.Data, result.Message))
                    : Response.AsJson(ApiResult.Fail(result.Message));
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
    }
}
