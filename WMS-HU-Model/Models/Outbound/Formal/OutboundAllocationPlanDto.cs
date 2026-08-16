using System.Collections.Generic;

namespace WMS_HU_Model.Models.Outbound.Formal
{
    public class OutboundAllocationPlanDto
    {
        public string ResultType { get; set; }
        public decimal RequiredQty { get; set; }
        public decimal AvailableQty { get; set; }
        public decimal ShortageQty { get; set; }
        public List<OutboundAllocationCandidateDto> SelectedBoxes { get; set; }
        public List<OutboundInnerPackCandidateDto> SuggestedInnerPacks { get; set; }

        public OutboundAllocationPlanDto()
        {
            SelectedBoxes = new List<OutboundAllocationCandidateDto>();
            SuggestedInnerPacks = new List<OutboundInnerPackCandidateDto>();
        }
    }
}
