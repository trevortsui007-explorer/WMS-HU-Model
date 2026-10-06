using Learun.Application.WebApi;
using Nancy;
using Nancy.ModelBinding;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.WeekCode;
using WMS_HU_Model.Repositories;
using WMS_HU_Model.Services;

namespace WMS.Modules
{
    public class WeekCodeRecalculateModule : BaseApi
    {
        public WeekCodeRecalculateModule()
            : base("/api/wms-fg/weekcode")
        {
            var service = new WeekCodeRecalculateService(
                new WeekCodeRecalculateRepository(),
                new MesWeekCodeRuleProvider(new MesWeekCodeRuleRepository()),
                new WeekCodeResolver(new UnavailableWeekCodeRuleProvider()));

            Post["/recalculate"] = _ =>
            {
                var request = this.Bind<WeekCodeRecalculateRequest>();
                var result = service.Recalculate(request);

                return result.IsSuccess
                    ? Response.AsJson(ApiResult.Success(result.Data))
                    : Response.AsJson(ApiResult.Fail(result.Message), HttpStatusCode.BadRequest);
            };
        }
    }
}
