using System;

namespace WMS_HU_Model.Models.Outbound
{
    public class PickBatchItemResultDto
    {
        public int SequenceNo { get; set; }

        public string BoxHuCode { get; set; }

        public string FromLocationCode { get; set; }

        public string ToLocationCode { get; set; }

        public decimal BoxQty { get; set; }

        public string Uom { get; set; }

        public DateTime FifoTime { get; set; }

        public long MoveLogId { get; set; }

        public string OperationNo { get; set; }
    }
}