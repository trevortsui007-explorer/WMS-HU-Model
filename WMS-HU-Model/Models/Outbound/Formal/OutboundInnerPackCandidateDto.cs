namespace WMS_HU_Model.Models.Outbound.Formal
{
    public class OutboundInnerPackCandidateDto
    {
        public long InnerHuId { get; set; }
        public string InnerHuCode { get; set; }
        public decimal Qty { get; set; }
        public int MinWeekSortKey { get; set; }
    }
}
