using System;
using System.Collections.Generic;

namespace WMS_HU_Model.Models.Audit
{
    public class AuditQueryBase
    {
        public string DateFrom { get; set; }
        public string DateTo { get; set; }
        public DateTime? FromTime { get; set; }
        public DateTime? ToTimeExclusive { get; set; }
        public string HuCode { get; set; }
        public string RequestId { get; set; }
        public string Operator { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }

    public class OperationAuditQuery : AuditQueryBase
    {
        public string OperationType { get; set; }
    }

    public class MoveAuditQuery : AuditQueryBase
    {
        public string MoveType { get; set; }
    }

    public class HoldAuditQuery : AuditQueryBase
    {
        public string HoldReason { get; set; }
        public string Status { get; set; }
    }

    public class AuditPageResult<T>
    {
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public List<T> Items { get; set; } = new List<T>();
    }

    public class OperationAuditRowDto
    {
        public long Id { get; set; }
        public string OperationNo { get; set; }
        public string OperationType { get; set; }
        public string HuCode { get; set; }
        public string HuType { get; set; }
        public string RelatedHuCode { get; set; }
        public string BusinessNo { get; set; }
        public string RequestId { get; set; }
        public string DetailJson { get; set; }
        public string Operator { get; set; }
        public DateTime OperationTime { get; set; }
        public string Remark { get; set; }
    }

    public class MoveAuditRowDto
    {
        public long Id { get; set; }
        public string MoveNo { get; set; }
        public string HuCode { get; set; }
        public string HuType { get; set; }
        public string FromLocationCode { get; set; }
        public string ToLocationCode { get; set; }
        public string MoveType { get; set; }
        public string ScannedLocationCode { get; set; }
        public string BusinessNo { get; set; }
        public string TaskNo { get; set; }
        public string RequestId { get; set; }
        public string ReasonCode { get; set; }
        public string Operator { get; set; }
        public DateTime MoveTime { get; set; }
        public string Remark { get; set; }
    }

    public class HoldAuditRowDto
    {
        public long Id { get; set; }
        public string HoldNo { get; set; }
        public string HuCode { get; set; }
        public string HuType { get; set; }
        public string HoldType { get; set; }
        public string HoldReasonCode { get; set; }
        public string HoldReason { get; set; }
        public string SourceBizType { get; set; }
        public long? SourceBizId { get; set; }
        public string SourceBizNo { get; set; }
        public bool IsActive { get; set; }
        public DateTime HoldTime { get; set; }
        public string HoldUser { get; set; }
        public string HoldRequestId { get; set; }
        public DateTime? ReleaseTime { get; set; }
        public string ReleaseUser { get; set; }
        public string ReleaseReason { get; set; }
        public string ReleaseRequestId { get; set; }
    }

    public class AuditTraceEventDto
    {
        public string Source { get; set; }
        public string EventType { get; set; }
        public DateTime EventTime { get; set; }
        public string HuCode { get; set; }
        public string HuType { get; set; }
        public string Operator { get; set; }
        public string RequestId { get; set; }
        public string Title { get; set; }
        public string Summary { get; set; }
    }

    public class RequestAuditTraceDto
    {
        public string RequestId { get; set; }
        public List<OperationAuditRowDto> Operations { get; set; } = new List<OperationAuditRowDto>();
        public List<MoveAuditRowDto> Moves { get; set; } = new List<MoveAuditRowDto>();
        public List<HoldAuditRowDto> Holds { get; set; } = new List<HoldAuditRowDto>();
        public List<AuditTraceEventDto> Events { get; set; } = new List<AuditTraceEventDto>();
    }

    public class AuditCapabilityDto
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public bool Available { get; set; }
        public string Status { get; set; }
        public string Source { get; set; }
        public string Message { get; set; }
    }
}
