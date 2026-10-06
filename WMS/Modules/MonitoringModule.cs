using Learun.Application.WebApi;
using System;
using Nancy;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Repositories;
using WMS_HU_Model.Services;

namespace WMS.Modules
{
    public class MonitoringModule : BaseApi
    {
        private readonly IMonitoringService _service;

        public MonitoringModule()
            : this(new MonitoringService(
                new MonitoringRepository(),
                new FinishedGoodsInboundReadService(new FinishedGoodsInboundReadRepository(), new EffectiveLocationResolver()),
                new WeekCodeAuditService(new WeekCodeAuditRepository(), new MesFinishedGoodsInboundRepository())))
        {
        }

        internal MonitoringModule(IMonitoringService service)
            : base("/api/wms-fg/monitoring")
        {
            _service = service;
            Get["/overview"] = _ => Response.AsJson(ApiResult.Success(_service.GetOverview()));
            Get["/activity"] = _ =>
            {
                object rawDays = Request.Query.days;
                string daysText = Convert.ToString(rawDays);
                int days;
                if (!int.TryParse(daysText, out days)) days = 7;
                return Response.AsJson(ApiResult.Success(_service.GetActivity(days)));
            };
        }
    }
}
