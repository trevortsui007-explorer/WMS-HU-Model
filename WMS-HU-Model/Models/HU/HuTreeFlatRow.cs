namespace WMS_HU_Model.Models.HU
{
    public class HuTreeFlatRow
    {
        public long HuId { get; set; }

        public long? ParentHuId { get; set; }

        public string HuCode { get; set; }

        public string HuType { get; set; }

        public string PlantCode { get; set; }

        public string LifecycleStatus { get; set; }

        public bool IsHold { get; set; }

        public int LevelNo { get; set; }
    }
}