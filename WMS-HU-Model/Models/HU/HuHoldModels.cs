namespace WMS_HU_Model.Models.HU
{
    public class HoldHuRequest
    {
        public string RequestId { get; set; }
        public string Operator { get; set; }
        public string HoldType { get; set; }
        public string ReasonCode { get; set; }
        public string Reason { get; set; }
    }

    public class ReleaseHuRequest
    {
        public string RequestId { get; set; }
        public string Operator { get; set; }
        public string Reason { get; set; }
    }

    public class HuHoldResultDto
    {
        public string HuCode { get; set; }
        public string HoldNo { get; set; }
        public bool IsHold { get; set; }
        public bool IsDuplicate { get; set; }
    }
}
