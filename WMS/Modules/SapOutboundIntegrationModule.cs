using System.IO;
using Nancy;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.Outbound.Integration;
using WMS_HU_Model.Repositories;
using WMS_HU_Model.Services;
using WMS.Infrastructure.Sap;
using System;
using Newtonsoft.Json.Linq;
using WMS_HU_Model.Infrastructure.Sap;

namespace WMS.Modules
{
    public class SapOutboundIntegrationModule : NancyModule
    {
        private readonly ISapOutboundOrderReceiveService _service;
        private readonly ISapOutboundPostingService _postingService;
        private readonly ISapOutboundPostingRepository _postingRepository;

        public SapOutboundIntegrationModule()
            : base("/api/wms/integration/sap")
        {
            _service = new SapOutboundOrderReceiveService(new SapOutboundOrderReceiveRepository());
            _postingRepository = new SapOutboundPostingRepository();
            _postingService = new SapOutboundPostingService(_postingRepository, new S0089SapOutboundPostingClient());

            Post("/outbound-orders", _ =>
            {
                string rawJson;
                using (StreamReader reader = new StreamReader(Request.Body))
                    rawJson = reader.ReadToEnd();
                OperationResult<SapOutboundOrderReceiveResultDto> result;
                try
                {
                    result = _service.Receive(rawJson);
                    string businessKey = result.Data == null ? ExtractDeliveryNo(rawJson) : result.Data.SAPDeliveryNo;
                    _postingRepository.RecordReceiveCommunication(
                        businessKey,
                        rawJson,
                        result.IsSuccess ? (result.Data != null && result.Data.IsDuplicate ? "DUPLICATE" : "SUCCESS") : "REJECTED",
                        result.Message,
                        ExtractPayloadHash(rawJson));
                }
                catch (Exception ex)
                {
                    _postingRepository.RecordReceiveCommunication(ExtractDeliveryNo(rawJson), rawJson, "REJECTED", ex.Message, ExtractPayloadHash(rawJson));
                    throw;
                }
                return result.IsSuccess
                    ? Response.AsJson(ApiResult.Success(result.Data, result.Message))
                    : Response.AsJson(ApiResult.Fail(result.Message));
            });

            Get("/logs", _ =>
            {
                object rawBusinessKey = Request.Query.businessKey;
                object rawInterfaceCode = Request.Query.interfaceCode;
                object rawDirection = Request.Query.direction;
                object rawResultStatus = Request.Query.resultStatus;
                object rawPage = Request.Query.page;
                object rawPageSize = Request.Query.pageSize;
                int page;
                int pageSize;
                int.TryParse(Convert.ToString(rawPage), out page);
                int.TryParse(Convert.ToString(rawPageSize), out pageSize);
                OperationResult<PagedResult<SapIntegrationLogDto>> result = _postingService.QueryLogs(new SapIntegrationLogQuery
                {
                    BusinessKey = Convert.ToString(rawBusinessKey),
                    InterfaceCode = Convert.ToString(rawInterfaceCode),
                    Direction = Convert.ToString(rawDirection),
                    ResultStatus = Convert.ToString(rawResultStatus),
                    Page = page,
                    PageSize = pageSize
                });
                return Response.AsJson(ApiResult.Success(result.Data), HttpStatusCode.OK);
            });

            Get("/logs/{id}", args =>
            {
                object rawId = args.id;
                long id;
                if (!long.TryParse(Convert.ToString(rawId), out id))
                    return Response.AsJson(ApiResult.Fail("日志Id无效。", 400), HttpStatusCode.BadRequest);
                OperationResult<SapIntegrationLogDto> result = _postingService.GetLog(id);
                return result.IsSuccess
                    ? Response.AsJson(ApiResult.Success(result.Data), HttpStatusCode.OK)
                    : Response.AsJson(ApiResult.Fail(result.Message, 404), HttpStatusCode.NotFound);
            });
        }

        private static string ExtractDeliveryNo(string rawJson)
        {
            try
            {
                JObject payload = JObject.Parse(rawJson ?? string.Empty);
                return SapPayloadCanonicalizer.ExtractDeliveryNo(payload);
            }
            catch
            {
                return null;
            }
        }

        private static string ExtractPayloadHash(string rawJson)
        {
            JObject payload;
            string hash;
            string error;
            return SapPayloadCanonicalizer.TryCreate(rawJson, out payload, out hash, out error) ? hash : null;
        }
    }
}
