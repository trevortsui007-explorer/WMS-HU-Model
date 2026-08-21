using System;
using Nancy;
using Nancy.ModelBinding;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.Inbound;
using WMS_HU_Model.Repositories;
using WMS_HU_Model.Services;

namespace WMS.Modules
{
    public class FinishedGoodsInboundIntegrationModule : NancyModule
    {
        private readonly IFinishedGoodsInboundService _service;

        public FinishedGoodsInboundIntegrationModule()
            : this(new FinishedGoodsInboundService(
                new FinishedGoodsInboundRepository(),
                new WeekCodeResolver(new UnavailableWeekCodeRuleProvider())))
        {
        }

        internal FinishedGoodsInboundIntegrationModule(IFinishedGoodsInboundService service)
            : base("/api/wms/integration/finished-goods")
        {
            _service = service;

            Post("/inbound", _ =>
            {
                try
                {
                    FinishedGoodsInboundReceiveRequest request = this.Bind<FinishedGoodsInboundReceiveRequest>();
                    OperationResult<FinishedGoodsInboundResultDto> result = _service.Receive(request);
                    if (!result.IsSuccess)
                        return Response.AsJson(ApiResult.Fail(result.Message, 400), HttpStatusCode.BadRequest);
                    return Response.AsJson(ApiResult.Success(result.Data, result.Message), HttpStatusCode.OK);
                }
                catch (ModelBindingException)
                {
                    return Response.AsJson(
                        ApiResult.Fail("请求JSON格式或字段类型无效。", 400),
                        HttpStatusCode.BadRequest);
                }
                catch (Exception)
                {
                    return Response.AsJson(
                        ApiResult.Fail("Finished goods inbound处理失败。", 500),
                        HttpStatusCode.InternalServerError);
                }
            });
        }
    }
}
