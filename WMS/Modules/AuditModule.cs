using Learun.Application.WebApi;
using System;
using Nancy;
using WMS_HU_Model.Models.Audit;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Repositories;
using WMS_HU_Model.Services;

namespace WMS.Modules
{
    public class AuditModule : BaseApi
    {
        private readonly IAuditService _service;

        public AuditModule()
            : this(new AuditService(new AuditRepository()))
        {
        }

        internal AuditModule(IAuditService service)
            : base("/api/wms-fg/audit")
        {
            _service = service;

            Get["/operations"] = _ => Respond(_service.QueryOperations(new OperationAuditQuery
            {
                DateFrom = Read("dateFrom"), DateTo = Read("dateTo"), HuCode = Read("huCode"), RequestId = Read("requestId"),
                Operator = Read("operator"), OperationType = Read("operationType"), Page = ReadInt("page"), PageSize = ReadInt("pageSize")
            }));

            Get["/moves"] = _ => Respond(_service.QueryMoves(new MoveAuditQuery
            {
                DateFrom = Read("dateFrom"), DateTo = Read("dateTo"), HuCode = Read("huCode"), RequestId = Read("requestId"),
                Operator = Read("operator"), MoveType = Read("moveType"), Page = ReadInt("page"), PageSize = ReadInt("pageSize")
            }));

            Get["/holds"] = _ => Respond(_service.QueryHolds(new HoldAuditQuery
            {
                DateFrom = Read("dateFrom"), DateTo = Read("dateTo"), HuCode = Read("huCode"), RequestId = Read("requestId"),
                Operator = Read("operator"), HoldReason = Read("holdReason"), Status = Read("status"), Page = ReadInt("page"), PageSize = ReadInt("pageSize")
            }));

            Get["/request-trace"] = _ => Respond(_service.GetRequestTrace(Read("requestId")));

            Get["/capabilities"] = _ => Response.AsJson(ApiResult.Success(_service.GetCapabilities()));
        }

        private Response Respond<T>(OperationResult<T> result)
        {
            return result.IsSuccess
                ? Response.AsJson(ApiResult.Success(result.Data), HttpStatusCode.OK)
                : Response.AsJson(ApiResult.Fail(result.Message, 400), HttpStatusCode.BadRequest);
        }

        private string Read(string name)
        {
            object rawValue = Request.Query[name];
            return Convert.ToString(rawValue);
        }

        private int ReadInt(string name)
        {
            string text = Read(name);
            int value;
            return int.TryParse(text, out value) ? value : 0;
        }
    }
}
