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
{    public class OutboundPickingModule : NancyModule
    {
        private readonly IOutboundPickingService _pickingService;

        public OutboundPickingModule()
            : base("/api/wms/outbound")
        {
            IOutboundOrderRepository orderRepository = new OutboundOrderRepository();
            _pickingService = new OutboundPickingService(new OutboundPickingRepository(), orderRepository, new HuIdentifierService(new HuIdentifierRepository()));

            Post("/orders/{vbeln}/preview", args =>
            {
                object rawVbeln = args.vbeln;
                string vbeln = Convert.ToString(rawVbeln);
                OperationResult<OutboundPreviewDto> result = _pickingService.Preview(vbeln);
                return result.IsSuccess
                    ? Response.AsJson(ApiResult.Success(result.Data))
                    : Response.AsJson(ApiResult.Fail(result.Message));
            });

            Post("/orders/{vbeln}/start-picking", args =>
            {
                object rawVbeln = args.vbeln;
                string vbeln = Convert.ToString(rawVbeln);
                StartPickingRequest request = this.Bind<StartPickingRequest>();
                OperationResult<StartPickingResultDto> result = _pickingService.StartPicking(vbeln, request);
                return result.IsSuccess
                    ? Response.AsJson(ApiResult.Success(result.Data, result.Message))
                    : Response.AsJson(ApiResult.Fail(result.Message));
            });

            Post("/allocations/{allocationNo}/release", args =>
            {
                object rawAllocationNo = args.allocationNo;
                string allocationNo = Convert.ToString(rawAllocationNo);
                OutboundActionRequest request = this.Bind<OutboundActionRequest>();
                OperationResult<OutboundActionResultDto> result = _pickingService.ReleaseAllocation(allocationNo, request);
                return result.IsSuccess
                    ? Response.AsJson(ApiResult.Success(result.Data, result.Message))
                    : Response.AsJson(ApiResult.Fail(result.Message));
            });

            Post("/pick-batches/{pickBatchNo}/scan", args =>
            {
                object rawPickBatchNo = args.pickBatchNo;
                string pickBatchNo = Convert.ToString(rawPickBatchNo);
                OutboundScanRequest request = this.Bind<OutboundScanRequest>();
                OperationResult<OutboundScanResultDto> result = _pickingService.Scan(pickBatchNo, request);
                return result.IsSuccess
                    ? Response.AsJson(ApiResult.Success(result.Data, result.Message))
                    : Response.AsJson(ApiResult.Fail(result.Message));
            });
        }
    }
}
