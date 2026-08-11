using System;

namespace WMS_HU_Model.Models.Outbound
{
    public class PickResultDto
    {
        public string OperationNo { get; set; }

        public long MoveLogId { get; set; }


        public string BoxHuCode { get; set; }

        public string PlantCode { get; set; }

        public string MaterialCode { get; set; }


        public string FromLocationCode { get; set; }

        public string ToLocationCode { get; set; }


        public int InnerPackCount { get; set; }

        public decimal TotalQty { get; set; }

        public string Uom { get; set; }


        public DateTime FifoTime { get; set; }

        public DateTime PickTime { get; set; }


        public bool IsDuplicate { get; set; }
    }
}