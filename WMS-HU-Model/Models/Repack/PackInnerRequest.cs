namespace WMS_HU_Model.Models.Repack
{
    public class PackInnerRequest
    {
        public string TargetBoxHuCode { get; set; }
        public string InnerHuCode { get; set; }
        public string RequestId { get; set; }
        public string BusinessNo { get; set; }
        public string Operator { get; set; }
        public string Remark { get; set; }
    }
}
