using WMS_HU_Model.Infrastructure.Database;
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
                string planStockId = null;
                try
                {
                    MesFinishedGoodsInboundTriggerRequest request = this.Bind<MesFinishedGoodsInboundTriggerRequest>();
                    planStockId = request == null ? null : request.PlanStockId;
                    OperationResult<MesFinishedGoodsInboundTriggerResult> result = _service.Trigger(planStockId);
                    return result.IsSuccess
                        ? Response.AsJson(ApiResult.Success(result.Data), HttpStatusCode.OK)
                        : Response.AsJson(ApiResult.Fail(BusinessLogText.MesError(result.Message), 400), HttpStatusCode.BadRequest);
                }
                catch (ModelBindingException)
                {
                    return Response.AsJson(ApiResult.Fail(BusinessLogText.MesError("请求JSON格式或字段类型无效。"), 400), HttpStatusCode.BadRequest);
                }
                catch (Exception ex)
                {
                    Logger.Error(string.Format(
                        "[{0:yyyy-MM-dd HH:mm:ss.fff}] [MES] POST /api/wms-fg/integration/finished-goods/mes-inbound planStockId={1}{2}{3}{2}",
                        DateTime.Now,
                        (planStockId ?? "<null>").Replace("\r", "\\r").Replace("\n", "\\n"),
                        Environment.NewLine,
                        ex.ToString()));
                    return Response.AsJson(ApiResult.Fail(BusinessLogText.MesError("成品入库处理异常"), 500), HttpStatusCode.InternalServerError);
                }
            };
        }
    }
}
