using System;
using System.Collections.Generic;

namespace WMS_HU_Model.Models.WeekCode
{
    public class WeekCodeAuditQuery
    {
        public string PlantCode { get; set; }
        public string MaterialCode { get; set; }
        public string MaterialRevision { get; set; }
        public string AnomalyType { get; set; }
        public string RiskLevel { get; set; }
        public bool? IsHold { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }

    public class WeekCodeAuditItemDto
    {
        public string InboundRuleValue { get; set; }
        public string MesInstructionRule { get; set; }
        public long BoxHUId { get; set; }
        public string BoxCode { get; set; }
        public string PlantCode { get; set; }
        public string MaterialCode { get; set; }
        public string MaterialRevision { get; set; }
        public string DateCode { get; set; }
        public DateTime? PackCreatedTime { get; set; }
        public string WeekRuleSource { get; set; }
        public string WeekRuleValue { get; set; }
        public int? WeekYear { get; set; }
        public int? WeekNo { get; set; }
        public int? WeekSortKey { get; set; }
        public string CandidateRuleValue { get; set; }
        public int? CandidateWeekYear { get; set; }
        public int? CandidateWeekNo { get; set; }
        public int? CandidateWeekSortKey { get; set; }
        public string AnomalyType { get; set; }
        public string RiskLevel { get; set; }
        public string Reason { get; set; }
        public bool IsHold { get; set; }
        public int PackCount { get; set; }
        public decimal TotalQty { get; set; }
    }

    public class WeekCodeAuditResultDto
    {
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public List<WeekCodeAuditItemDto> Items { get; set; }
        public List<WeekCodeAuditMaterialDto> MaterialSummary { get; set; }
        public WeekCodeAuditResultDto() { Items = new List<WeekCodeAuditItemDto>(); }
    }

    public class WeekCodeAuditMaterialDto
    {
        public string MesInstructionRule { get; set; }
        public string MaterialCode { get; set; }
        public string MaterialRevision { get; set; }
        public int BoxCount { get; set; }
    }

    public class WeekCodeAuditFact
    {
        public string InboundRuleValue { get; set; }
        public string MesInstructionRule { get; set; }
        public long BoxHUId { get; set; }
        public string BoxCode { get; set; }
        public string PlantCode { get; set; }
        public string MaterialCode { get; set; }
        public string MaterialRevision { get; set; }
        public string DateCode { get; set; }
        public DateTime? PackCreatedTime { get; set; }
        public string WeekRuleSource { get; set; }
        public string WeekRuleValue { get; set; }
        public int? WeekYear { get; set; }
        public int? WeekNo { get; set; }
        public int? WeekSortKey { get; set; }
        public bool IsHold { get; set; }
        public string HoldReasonCode { get; set; }
        public string HoldReason { get; set; }
        public int PackCount { get; set; }
        public decimal TotalQty { get; set; }
        public List<WeekCodeAuditPackSource> PackSources { get; set; }
        public WeekCodeAuditFact() { PackSources = new List<WeekCodeAuditPackSource>(); }
    }

    public class WeekCodeAuditPackSource
    {
        public string SourceSystem { get; set; }
        public string SourceEntityId { get; set; }
    }
}
