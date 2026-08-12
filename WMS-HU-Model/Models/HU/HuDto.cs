namespace WMS_HU_Model.Models.HU
{
    public class HuDto
    {
        public long Id { get; set; }
        public string HuCode { get; set; }
        public string HuType { get; set; }
        public string PlantCode { get; set; }
        public string PackageSpecCode { get; set; }
        public string LifecycleStatus { get; set; }
        public bool IsHold { get; set; }
        public string HoldReason { get; set; }
        public string SourceSystem { get; set; }
        public string SourceEntityId { get; set; }
        public string SourceBusinessNo { get; set; }
        public string Remark { get; set; }
    }
}
