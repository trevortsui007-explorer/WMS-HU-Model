using System;
using System.Collections.Generic;

namespace WMS_HU_Model.Models.Inbound
{
    public class InboundConfirmBoxRequest
    {
        public string PalletHuCode { get; set; }
        public string BoxHuCode { get; set; }
        public string ConfirmMethod { get; set; }
        public string RequestId { get; set; }
        public string Operator { get; set; }
    }

    public class InboundConfirmPalletDto
    {
        public long ReceiptId { get; set; }
        public string PalletHuCode { get; set; }
        public string PlantCode { get; set; }
        public string Status { get; set; }
        public bool CanConfirm { get; set; }
        public string BlockReason { get; set; }
        public bool IsDuplicate { get; set; }
        public int TotalBoxCount { get; set; }
        public int ConfirmedBoxCount { get; set; }
        public List<InboundConfirmBoxDto> Boxes { get; set; } = new List<InboundConfirmBoxDto>();
    }

    public class InboundConfirmBoxDto
    {
        public string BoxHuCode { get; set; }
        public string ConfirmMethod { get; set; }
        public string ConfirmedBy { get; set; }
        public DateTime? ConfirmedAt { get; set; }
    }
}
