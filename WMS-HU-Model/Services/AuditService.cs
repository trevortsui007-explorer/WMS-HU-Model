using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WMS_HU_Model.Models.Audit;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Repositories;

namespace WMS_HU_Model.Services
{
    public class AuditService : IAuditService
    {
        private const int DefaultPageSize = 50;
        private const int MaxPageSize = 200;
        private const int MaxRangeDays = 90;
        private const int TraceTopPerSource = 200;
        private readonly IAuditRepository _repository;

        public AuditService(IAuditRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public OperationResult<AuditPageResult<OperationAuditRowDto>> QueryOperations(OperationAuditQuery query)
        {
            string error = NormalizeBase(query);
            if (error != null) return OperationResult<AuditPageResult<OperationAuditRowDto>>.Fail(error);
            string normalized;
            error = NormalizeText(query.OperationType, "OperationType", 30, true, out normalized);
            query.OperationType = normalized;
            return error == null
                ? OperationResult<AuditPageResult<OperationAuditRowDto>>.Success(_repository.QueryOperations(query))
                : OperationResult<AuditPageResult<OperationAuditRowDto>>.Fail(error);
        }

        public OperationResult<AuditPageResult<MoveAuditRowDto>> QueryMoves(MoveAuditQuery query)
        {
            string error = NormalizeBase(query);
            if (error != null) return OperationResult<AuditPageResult<MoveAuditRowDto>>.Fail(error);
            string normalized;
            error = NormalizeText(query.MoveType, "MoveType", 30, true, out normalized);
            query.MoveType = normalized;
            return error == null
                ? OperationResult<AuditPageResult<MoveAuditRowDto>>.Success(_repository.QueryMoves(query))
                : OperationResult<AuditPageResult<MoveAuditRowDto>>.Fail(error);
        }

        public OperationResult<AuditPageResult<HoldAuditRowDto>> QueryHolds(HoldAuditQuery query)
        {
            string error = NormalizeBase(query);
            if (error != null) return OperationResult<AuditPageResult<HoldAuditRowDto>>.Fail(error);
            string normalized;
            error = NormalizeText(query.HoldReason, "HoldReason", 100, false, out normalized);
            query.HoldReason = normalized;
            if (error != null) return OperationResult<AuditPageResult<HoldAuditRowDto>>.Fail(error);
            error = NormalizeText(query.Status, "Status", 10, true, out normalized);
            query.Status = normalized;
            if (error != null) return OperationResult<AuditPageResult<HoldAuditRowDto>>.Fail(error);
            if (query.Status != null && query.Status != "ACTIVE" && query.Status != "RELEASED")
                return OperationResult<AuditPageResult<HoldAuditRowDto>>.Fail("Status只支持ACTIVE或RELEASED。");
            return OperationResult<AuditPageResult<HoldAuditRowDto>>.Success(_repository.QueryHolds(query));
        }

        public OperationResult<RequestAuditTraceDto> GetRequestTrace(string requestId)
        {
            string normalized = requestId;
            string error = NormalizeText(requestId, "RequestId", 100, false, out normalized);
            if (error != null) return OperationResult<RequestAuditTraceDto>.Fail(error);
            if (normalized == null) return OperationResult<RequestAuditTraceDto>.Fail("RequestId不能为空。");

            RequestAuditTraceDto result = new RequestAuditTraceDto
            {
                RequestId = normalized,
                Operations = _repository.GetOperationsByRequestId(normalized, TraceTopPerSource) ?? new List<OperationAuditRowDto>(),
                Moves = _repository.GetMovesByRequestId(normalized, TraceTopPerSource) ?? new List<MoveAuditRowDto>(),
                Holds = _repository.GetHoldsByRequestId(normalized, TraceTopPerSource) ?? new List<HoldAuditRowDto>()
            };
            result.Events.AddRange(result.Operations.Select(x => new AuditTraceEventDto
            {
                Source = "OPERATION", EventType = "OPERATION", EventTime = x.OperationTime, HuCode = x.HuCode, HuType = x.HuType,
                Operator = x.Operator, RequestId = x.RequestId, Title = x.OperationType, Summary = First(x.Remark, x.BusinessNo, x.OperationNo)
            }));
            result.Events.AddRange(result.Moves.Select(x => new AuditTraceEventDto
            {
                Source = "MOVE", EventType = "MOVE", EventTime = x.MoveTime, HuCode = x.HuCode, HuType = x.HuType,
                Operator = x.Operator, RequestId = x.RequestId, Title = x.MoveType, Summary = Text(x.FromLocationCode) + " → " + Text(x.ToLocationCode)
            }));
            foreach (HoldAuditRowDto hold in result.Holds)
            {
                if (string.Equals(hold.HoldRequestId, normalized, StringComparison.OrdinalIgnoreCase)) result.Events.Add(new AuditTraceEventDto
                {
                    Source = "HOLD", EventType = "HOLD", EventTime = hold.HoldTime, HuCode = hold.HuCode, HuType = hold.HuType,
                    Operator = hold.HoldUser, RequestId = hold.HoldRequestId, Title = hold.HoldReasonCode, Summary = First(hold.HoldReason, hold.HoldType, hold.HoldNo)
                });
                if (string.Equals(hold.ReleaseRequestId, normalized, StringComparison.OrdinalIgnoreCase) && hold.ReleaseTime.HasValue) result.Events.Add(new AuditTraceEventDto
                {
                    Source = "HOLD", EventType = "RELEASE", EventTime = hold.ReleaseTime.Value, HuCode = hold.HuCode, HuType = hold.HuType,
                    Operator = hold.ReleaseUser, RequestId = hold.ReleaseRequestId, Title = "RELEASE", Summary = First(hold.ReleaseReason, hold.HoldReasonCode, hold.HoldNo)
                });
            }
            result.Events = result.Events.OrderBy(x => x.EventTime).ThenBy(x => x.Source).ThenBy(x => x.HuCode).ToList();
            return OperationResult<RequestAuditTraceDto>.Success(result);
        }

        public List<AuditCapabilityDto> GetCapabilities()
        {
            return new List<AuditCapabilityDto>
            {
                Capability("HU_MOVE", "HU移动", true, "WMS_HU_MoveLog", "移动事实可审计。"),
                Capability("HU_OPERATION", "HU操作", true, "WMS_HU_OperationLog", "操作事实可审计。"),
                Capability("HOLD_RELEASE", "Hold / Release", true, "WMS_HU_HoldLog", "Hold与Release生命周期可审计。"),
                Capability("MASTER_DATA", "基础数据修改", false, null, "当前未建立完整持久化审计；查不到不代表没有发生。"),
                Capability("PRE_RECEIPT_INBOUND_FAILURE", "Receipt创建前接口失败", false, null, "当前未持久化完整失败事实；无法提供失败次数。")
            };
        }

        private static string NormalizeBase(AuditQueryBase query)
        {
            if (query == null) return "查询条件不能为空。";
            string error = NormalizeDates(query);
            if (error != null) return error;
            string normalized;
            error = NormalizeText(query.HuCode, "HUCode", 100, true, out normalized);
            query.HuCode = normalized;
            if (error != null) return error;
            error = NormalizeText(query.RequestId, "RequestId", 100, false, out normalized);
            query.RequestId = normalized;
            if (error != null) return error;
            error = NormalizeText(query.Operator, "Operator", 50, false, out normalized);
            query.Operator = normalized;
            if (error != null) return error;
            if (query.Page == 0) query.Page = 1;
            if (query.Page < 1) return "Page必须大于0。";
            if (query.PageSize == 0) query.PageSize = DefaultPageSize;
            if (query.PageSize < 1 || query.PageSize > MaxPageSize) return "PageSize必须在1到200之间。";
            return null;
        }

        private static string NormalizeDates(AuditQueryBase query)
        {
            if (string.IsNullOrWhiteSpace(query.DateFrom) && string.IsNullOrWhiteSpace(query.DateTo))
            {
                query.FromTime = DateTime.Now.Date.AddDays(-6);
                query.ToTimeExclusive = DateTime.Now.Date.AddDays(1);
                return null;
            }
            if (string.IsNullOrWhiteSpace(query.DateFrom) || string.IsNullOrWhiteSpace(query.DateTo)) return "开始和结束日期必须同时提供。";
            DateTime from;
            DateTime to;
            if (!DateTime.TryParseExact(query.DateFrom.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out from)
                || !DateTime.TryParseExact(query.DateTo.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out to))
                return "日期格式必须为yyyy-MM-dd。";
            DateTime toExclusive = to.AddDays(1);
            if (from >= toExclusive) return "开始日期不能晚于结束日期。";
            if ((toExclusive - from).TotalDays > MaxRangeDays) return "日期范围不能超过90天。";
            query.FromTime = from;
            query.ToTimeExclusive = toExclusive;
            return null;
        }

        private static string NormalizeText(string value, string name, int maxLength, bool upper, out string normalized)
        {
            normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            if (normalized != null && normalized.Length > maxLength) return name + "长度不能超过" + maxLength + "。";
            if (normalized != null && upper) normalized = normalized.ToUpperInvariant();
            return null;
        }

        private static AuditCapabilityDto Capability(string code, string name, bool available, string source, string message)
        {
            return new AuditCapabilityDto { Code = code, Name = name, Available = available, Status = available ? "AVAILABLE" : "NOT_OBSERVABLE", Source = source, Message = message };
        }

        private static string Text(string value) { return string.IsNullOrEmpty(value) ? "—" : value; }
        private static string First(params string[] values) { return values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)); }
    }
}
