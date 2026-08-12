using System;

namespace WMS_HU_Model.Models.HU
{
    public class HuMoveResultDto
    {
        public long MoveLogId { get; set; }
        public string MoveNo { get; set; }
        public string HuCode { get; set; }
        public string MoveType { get; set; }
        public string FromLocationCode { get; set; }
        public string ToLocationCode { get; set; }
        public DateTime MoveTime { get; set; }
        public string Operator { get; set; }
    }
}
