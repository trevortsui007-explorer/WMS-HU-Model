using System;

namespace WMS_HU_Model.Models.Putaway
{
    public class PutawayResultDto
    {
        public string OperationNo { get; set; }

        public long MoveLogId { get; set; }

        public string PalletHuCode { get; set; }

        public string BoxHuCode { get; set; }

        public string FromLocationCode { get; set; }

        public string ToLocationCode { get; set; }

        public int RemainingBoxCount { get; set; }

        public bool IsPalletEmpty { get; set; }

        public bool IsPalletClosed { get; set; }

        public bool IsDuplicate { get; set; }

        public DateTime PutawayTime { get; set; }
    }
}