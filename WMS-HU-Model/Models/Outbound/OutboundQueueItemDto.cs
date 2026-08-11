using System;

namespace WMS_HU_Model.Models.Outbound
{
    public class OutboundQueueItemDto
    {
        public int QueueSequence { get; set; }

        public long BoxHuId { get; set; }

        public string BoxHuCode { get; set; }

        public string PlantCode { get; set; }

        public string WarehouseCode { get; set; }

        public string LocationCode { get; set; }

        public string MaterialCode { get; set; }

        public string MaterialRevision { get; set; }

        public int InnerPackCount { get; set; }

        public decimal TotalQty { get; set; }

        public string Uom { get; set; }

        public DateTime FifoTime { get; set; }
    }
}