using System;

namespace WMS_HU_Model.Models.HU
{
    public class HuMoveLogDto
    {
        public long Id { get; set; }

        public string MoveNo { get; set; }

        public string HuCode { get; set; }

        public string FromLocationCode { get; set; }

        public string ToLocationCode { get; set; }

        public string MoveType { get; set; }

        public string ScannedLocationCode { get; set; }

        public string BusinessNo { get; set; }

        public string TaskNo { get; set; }

        public string RequestId { get; set; }

        public string ReasonCode { get; set; }

        public string Operator { get; set; }

        public DateTime MoveTime { get; set; }

        public string Remark { get; set; }
    }
}