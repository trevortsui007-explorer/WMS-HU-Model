using System;

namespace WMS_HU_Model.Models.Outbound
{
    public class OutboundBatchPlanItemDto
    {
        public int SequenceNo { get; set; }
        public long BoxHuId { get; set; }
        public string BoxHuCode { get; set; }
        public string LocationCode { get; set; }
        public DateTime FifoTime { get; set; }
        public decimal BoxQty { get; set; }
        public decimal CumulativeQty { get; set; }
        public string Uom { get; set; }
    }
}
