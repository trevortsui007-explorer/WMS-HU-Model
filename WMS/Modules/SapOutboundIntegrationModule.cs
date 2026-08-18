using System.IO;
using Nancy;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.Outbound.Integration;
using WMS_HU_Model.Repositories;
using WMS_HU_Model.Services;

namespace WMS.Modules
{
    public class SapOutboundIntegrationModule : NancyModule
    {
        private readonly ISapOutboundOrderReceiveService _service;

        public SapOutboundIntegrationModule()
            : base("/api/wms/integration/sap")
        {
            _service = new SapOutboundOrderReceiveService(new SapOutboundOrderReceiveRepository());

            Post("/outbound-orders", _ =>
            {
                string rawJson;
                using (StreamReader reader = new StreamReader(Request.Body))
                    rawJson = reader.ReadToEnd();
                OperationResult<SapOutboundOrderReceiveResultDto> result = _service.Receive(rawJson);
                return result.IsSuccess
                    ? Response.AsJson(ApiResult.Success(result.Data, result.Message))
                    : Response.AsJson(ApiResult.Fail(result.Message));
            });
        }
    }
}
