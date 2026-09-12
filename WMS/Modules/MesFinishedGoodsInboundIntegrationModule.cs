using Learun.Application.WebApi;
using System;
using Nancy;
using Nancy.ModelBinding;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.Inbound;
using WMS_HU_Model.Repositories;
using WMS_HU_Model.Services;

namespace WMS.Modules
{
    public class MesFinishedGoodsInboundIntegrationModule : BaseApi
    {
        private readonly IMesFinishedGoodsInboundOrchestrationService _service;

        public MesFinishedGoodsInboundIntegrationModule()
            : this(new MesFinishedGoodsInboundOrchestrationService(
                new MesFinishedGoodsInboundRepository(),
                new MesFinishedGoodsInboundSnapshotBuilder(),
                new MesWeekCodeRuleProvider(new MesWeekCodeRuleRepository()),
                new FinishedGoodsInboundService(
                    new FinishedGoodsInboundRepository(),
                    new WeekCodeResolver(new UnavailableWeekCodeRuleProvider()))))
        {
        }

        internal MesFinishedGoodsInboundIntegrationModule(IMesFinishedGoodsInboundOrchestrationService service)
            : base("/api/wms-fg/integration/finished-goods")
        {
            _service = service;
            Post["/mes-inbound"] = _ =>
            {
                try
                {
                    MesFinishedGoodsInboundTriggerRequest request = this.Bind<MesFinishedGoodsInboundTriggerRequest>();
                    OperationResult<MesFinishedGoodsInboundTriggerResult> result = _service.Trigger(request == null ? null : request.PlanStockId);
                    return result.IsSuccess
                        ? Response.AsJson(ApiResult.Success(result.Data), HttpStatusCode.OK)
                        : Response.AsJson(ApiResult.Fail(result.Message, 400), HttpStatusCode.BadRequest);
                }
                catch (ModelBindingException)
                {
                    return Response.AsJson(ApiResult.Fail("请求JSON格式或字段类型无效。", 400), HttpStatusCode.BadRequest);
                }
                catch (Exception)
                {
                    return Response.AsJson(ApiResult.Fail("MES finished goods inbound处理失败。", 500), HttpStatusCode.InternalServerError);
                }
            };
        }
    }
}
