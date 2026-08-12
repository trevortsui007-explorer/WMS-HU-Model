using System.Collections.Generic;

namespace WMS_HU_Model.Models.Outbound
{
    public class OutboundBatchPlanDto
    {
        public string ResultType { get; set; }
        public decimal RequiredQty { get; set; }
        public decimal PlannedPickQty { get; set; }
        public decimal DifferenceQty { get; set; }
        public decimal RemainingQty { get; set; }
        public string Uom { get; set; }
        public bool AllowOverPick { get; set; }

        //--------------------------------------------------------
        // NEED_SPLIT时使用
        //--------------------------------------------------------
        public string SuggestedSplitBoxHuCode { get; set; }
        public decimal? SuggestedSplitBoxQty { get; set; }
        public decimal? SuggestedSplitQty { get; set; }

        public List<OutboundBatchPlanItemDto>
            SelectedBoxes
        { get; set; }

        public OutboundBatchPlanDto()
        {
            SelectedBoxes = new List<OutboundBatchPlanItemDto>();
        }
    }
}
