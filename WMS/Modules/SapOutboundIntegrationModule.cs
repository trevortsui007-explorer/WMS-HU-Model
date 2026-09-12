using Learun.Application.WebApi;
using System.IO;
using Nancy;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.Outbound.Integration;
using WMS_HU_Model.Repositories;
using WMS_HU_Model.Services;
using WMS.Infrastructure.Sap;
using System;
using System.Globalization;

namespace WMS.Modules
{
    public class SapOutboundIntegrationModule : BaseApi
    {
        private readonly ISapOutboundOrderReceiveService _service;
        private readonly ISapOutboundPostingService _postingService;
        private readonly ISapOutboundPostingRepository _postingRepository;

        public SapOutboundIntegrationModule()
            : base("/api/wms-fg/integration/sap")
        {
            _service = new SapOutboundOrderReceiveService(new SapOutboundOrderReceiveRepository());
            _postingRepository = new SapOutboundPostingRepository();
            _postingService = new SapOutboundPostingService(_postingRepository, new S0089SapOutboundPostingClient());

            Post["/outbound-orders"] = _ =>
            {
                string rawJson;
                using (StreamReader reader = new StreamReader(Request.Body))
                    rawJson = reader.ReadToEnd();
                OperationResult<SapOutboundOrderReceiveResultDto> result = _service.Receive(rawJson);
                return result.IsSuccess
                    ? Response.AsJson(ApiResult.Success(result.Data, result.Message))
                    : Response.AsJson(ApiResult.Fail(result.Message));
            };

            Get["/logs"] = _ =>
            {
                object rawBusinessKey = Request.Query.businessKey;
                object rawInterfaceCode = Request.Query.interfaceCode;
                object rawDirection = Request.Query.direction;
                object rawResultStatus = Request.Query.resultStatus;
                object rawDateFrom = Request.Query.dateFrom;
                object rawDateTo = Request.Query.dateTo;
                object rawPage = Request.Query.page;
                object rawPageSize = Request.Query.pageSize;
                int page;
                int pageSize;
                DateTime dateFrom;
                DateTime dateTo;
                DateTime? parsedDateFrom = null;
                DateTime? parsedDateTo = null;
                int.TryParse(Convert.ToString(rawPage), out page);
                int.TryParse(Convert.ToString(rawPageSize), out pageSize);
                string dateFromValue = Convert.ToString(rawDateFrom);
                string dateToValue = Convert.ToString(rawDateTo);
                if (!string.IsNullOrWhiteSpace(dateFromValue))
                {
                    if (!DateTime.TryParseExact(dateFromValue, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out dateFrom))
                        return Response.AsJson(ApiResult.Fail("开始日期格式必须为yyyy-MM-dd。", 400), HttpStatusCode.BadRequest);
                    parsedDateFrom = dateFrom;
                }
                if (!string.IsNullOrWhiteSpace(dateToValue))
                {
                    if (!DateTime.TryParseExact(dateToValue, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTo))
                        return Response.AsJson(ApiResult.Fail("结束日期格式必须为yyyy-MM-dd。", 400), HttpStatusCode.BadRequest);
                    parsedDateTo = dateTo;
                }
                OperationResult<PagedResult<SapIntegrationLogDto>> result = _postingService.QueryLogs(new SapIntegrationLogQuery
                {
                    BusinessKey = Convert.ToString(rawBusinessKey),
                    InterfaceCode = Convert.ToString(rawInterfaceCode),
                    Direction = Convert.ToString(rawDirection),
                    ResultStatus = Convert.ToString(rawResultStatus),
                    DateFrom = parsedDateFrom,
                    DateTo = parsedDateTo,
                    Page = page,
                    PageSize = pageSize
                });
                return Response.AsJson(ApiResult.Success(result.Data), HttpStatusCode.OK);
            };

            Get["/logs/{direction}/{id}"] = args =>
            {
                object rawDirection = args.direction;
                object rawId = args.id;
                long id;
                if (!long.TryParse(Convert.ToString(rawId), out id))
                    return Response.AsJson(ApiResult.Fail("日志Id无效。", 400), HttpStatusCode.BadRequest);
                OperationResult<SapIntegrationLogDto> result = _postingService.GetLog(Convert.ToString(rawDirection), id);
                return result.IsSuccess
                    ? Response.AsJson(ApiResult.Success(result.Data), HttpStatusCode.OK)
                    : Response.AsJson(ApiResult.Fail(result.Message, 404), HttpStatusCode.NotFound);
            };

            Get["/logs/{id}"] = args =>
            {
                object rawId = args.id;
                long id;
                if (!long.TryParse(Convert.ToString(rawId), out id))
                    return Response.AsJson(ApiResult.Fail("日志Id无效。", 400), HttpStatusCode.BadRequest);
                OperationResult<SapIntegrationLogDto> result = _postingService.GetLog(id);
                return result.IsSuccess
                    ? Response.AsJson(ApiResult.Success(result.Data), HttpStatusCode.OK)
                    : Response.AsJson(ApiResult.Fail(result.Message, 404), HttpStatusCode.NotFound);
            };
        }

    }
}
