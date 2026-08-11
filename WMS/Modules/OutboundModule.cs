using Nancy;
using Nancy.ModelBinding;
using System;
using System.Collections.Generic;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.Outbound;
using WMS_HU_Model.Repositories;
using WMS_HU_Model.Services;

namespace WMS.Modules
{
    public class OutboundModule : NancyModule
    {
        private readonly IOutboundService _service;

        public OutboundModule()
            : base("/api/wms/outbound")
        {
            IOutboundRepository repository = new OutboundRepository();

            _service = new OutboundService(repository);

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
                // 绑定 Body 中的 JSON 请求参数
                PickNextRequest request = this.Bind<PickNextRequest>();

                OperationResult<PickResultDto> result = _service.PickNext(request);

                if (!result.IsSuccess)
                {
                    return Response.AsJson(ApiResult.Fail(result.Message));
                }

                return Response.AsJson(ApiResult.Success(result.Data, result.Message));
            });
        }
    }
}