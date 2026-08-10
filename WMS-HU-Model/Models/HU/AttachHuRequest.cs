namespace WMS_HU_Model.Models.HU
{
    public class AttachHuRequest
    {
        public string ParentHuCode { get; set; }

        public string ChildHuCode { get; set; }

        public string Operator { get; set; }

        public string RequestId { get; set; }

        public string Remark { get; set; }
    }
}