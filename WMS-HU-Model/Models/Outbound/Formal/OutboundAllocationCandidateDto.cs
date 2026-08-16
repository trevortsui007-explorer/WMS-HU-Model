using System.Collections.Generic;

namespace WMS_HU_Model.Models.Outbound.Formal
{
    public class OutboundAllocationCandidateDto
    {
        public long BoxHuId { get; set; }
        public string BoxHuCode { get; set; }
        public long LocationId { get; set; }
        public string LocationCode { get; set; }
        public string MaterialCode { get; set; }
        public string Uom { get; set; }
        public decimal Qty { get; set; }
        public int MinWeekSortKey { get; set; }
        public int MaxWeekSortKey { get; set; }
        public int DistinctWeekCount { get; set; }
        public string WeekSummary { get; set; }
        public List<OutboundInnerPackCandidateDto> InnerPacks { get; set; }

        public OutboundAllocationCandidateDto()
        {
            InnerPacks = new List<OutboundInnerPackCandidateDto>();
        }
    }
}
