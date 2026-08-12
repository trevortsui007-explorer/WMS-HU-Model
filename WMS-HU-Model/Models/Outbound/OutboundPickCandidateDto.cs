using System;

namespace WMS_HU_Model.Models.Outbound
{
    public class OutboundPickCandidateDto
    {
        public long BoxHuId { get; set; }
        public string BoxHuCode { get; set; }
        public string PlantCode { get; set; }
        public long LocationId { get; set; }
        public string LocationCode { get; set; }
        public DateTime FifoTime { get; set; }
        public int InnerPackCount { get; set; }
        public decimal Qty { get; set; }
        public string Uom { get; set; }
    }
}
