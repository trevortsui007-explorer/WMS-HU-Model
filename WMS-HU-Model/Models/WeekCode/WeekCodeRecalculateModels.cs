using System;
using System.Collections.Generic;

namespace WMS_HU_Model.Models.WeekCode
{
    public class WeekCodeRecalculateRequest
    {
        public string PlantCode { get; set; }
        public string MaterialCode { get; set; }
        public string MaterialRevision { get; set; }
        public string Operator { get; set; }
        public string RequestId { get; set; }
    }

    public class WeekCodeRecalculateResultDto
    {
        public string PlantCode { get; set; }
        public string MaterialCode { get; set; }
        public string MaterialRevision { get; set; }
        public string RequestId { get; set; }
        public string RuleSource { get; set; }
        public string RuleValue { get; set; }
        public int BoxCount { get; set; }
        public int UpdatedBoxCount { get; set; }
        public int UnchangedBoxCount { get; set; }
        public int FailedBoxCount { get; set; }
        public int UpdatedItemCount { get; set; }
        public int ReleasedHoldCount { get; set; }
        public List<WeekCodeRecalculateBoxResultDto> Items { get; set; } = new List<WeekCodeRecalculateBoxResultDto>();
    }

    public class WeekCodeRecalculateBoxFact
    {
        public long BoxHUId { get; set; }
        public string BoxCode { get; set; }
        public bool IsHold { get; set; }
        public List<WeekCodeRecalculateItemFact> Items { get; set; } = new List<WeekCodeRecalculateItemFact>();
    }

    public class WeekCodeRecalculateItemFact
    {
        public long ItemId { get; set; }
        public DateTime? PackCreatedTime { get; set; }
        public string DateCode { get; set; }
        public string OldRuleSource { get; set; }
        public string OldRuleValue { get; set; }
        public string OldWeekFormatCode { get; set; }
        public short? OldWeekYear { get; set; }
        public byte? OldWeekNo { get; set; }
        public int? OldWeekSortKey { get; set; }
    }

    public class WeekCodeRecalculateItemPlan
    {
        public long ItemId { get; set; }
        public string DateCode { get; set; }
        public string OldRuleSource { get; set; }
        public string OldRuleValue { get; set; }
        public string OldWeekFormatCode { get; set; }
        public short? OldWeekYear { get; set; }
        public byte? OldWeekNo { get; set; }
        public int? OldWeekSortKey { get; set; }
        public WeekCodeSnapshot WeekCode { get; set; }
    }

    public class WeekCodeRecalculateBoxPlan
    {
        public long BoxHUId { get; set; }
        public string BoxCode { get; set; }
        public bool BeforeIsHold { get; set; }
        public List<WeekCodeRecalculateItemPlan> Items { get; set; } = new List<WeekCodeRecalculateItemPlan>();
    }

    public class WeekCodeRecalculateBoxResultDto
    {
        public long BoxHUId { get; set; }
        public string BoxCode { get; set; }
        public string Result { get; set; }
        public bool BeforeIsHold { get; set; }
        public bool? AfterIsHold { get; set; }
        public int UpdatedItemCount { get; set; }
        public bool ReleasedWeekCodeHold { get; set; }
        public string ErrorCode { get; set; }
        public string ErrorMessage { get; set; }
        public List<WeekCodeRecalculateItemResultDto> ItemResults { get; set; } = new List<WeekCodeRecalculateItemResultDto>();
    }

    public class WeekCodeRecalculateItemResultDto
    {
        public string DateCode { get; set; }
        public string OldWeekRuleSource { get; set; }
        public string OldWeekRuleValue { get; set; }
        public string OldWeekFormatCode { get; set; }
        public short? OldWeekYear { get; set; }
        public byte? OldWeekNo { get; set; }
        public int? OldWeekSortKey { get; set; }
        public string NewWeekRuleSource { get; set; }
        public string NewWeekRuleValue { get; set; }
        public string NewWeekFormatCode { get; set; }
        public short? NewWeekYear { get; set; }
        public byte? NewWeekNo { get; set; }
        public int? NewWeekSortKey { get; set; }
        public string Result { get; set; }
    }
}
