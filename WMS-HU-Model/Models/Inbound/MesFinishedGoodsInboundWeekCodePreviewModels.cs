using System;
using System.Collections.Generic;

namespace WMS_HU_Model.Models.Inbound
{
    public class MesFinishedGoodsInboundWeekCodePreviewResult
    {
        public FinishedGoodsInboundReceiveRequest Request { get; set; }
        public List<MesFinishedGoodsInboundWeekCodePackPreview> Packs { get; set; }
        public int ResolvedPackCount { get; set; }
        public int UnresolvedPackCount { get; set; }
        public int RuleNotFoundCount { get; set; }
        public int RuleAmbiguousCount { get; set; }
        public int UnsupportedRuleCount { get; set; }
        public int InvalidDateCodeCount { get; set; }
        public int InvalidWeekCount { get; set; }
        public int AnchorRequiredCount { get; set; }
        public MesFinishedGoodsInboundWeekCodePreviewResult() { Packs = new List<MesFinishedGoodsInboundWeekCodePackPreview>(); }
    }

    public class MesFinishedGoodsInboundWeekCodePackPreview
    {
        public string BoxSourceEntityId { get; set; }
        public string PackSourceEntityId { get; set; }
        public string BoxCode { get; set; }
        public string PackCode { get; set; }
        public string MaterialCode { get; set; }
        public string MaterialRevision { get; set; }
        public string DateCode { get; set; }
        public string RuleSource { get; set; }
        public string RuleValue { get; set; }
        public short? WeekYear { get; set; }
        public byte? WeekNo { get; set; }
        public int? WeekSortKey { get; set; }
        public string Status { get; set; }
        public string ErrorCode { get; set; }
        public string ErrorMessage { get; set; }
    }
}
