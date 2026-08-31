namespace WMS_HU_Model.Models.MasterData
{
    public class UpdateLocationRequest
    {
        public string ZoneCode { get; set; }
        public string LocationName { get; set; }
        [System.Obsolete("Ignored; removed from Final Schema v1.2.")]
        public string ParentLocationCode { get; set; }
        [System.Obsolete("Ignored; removed from Final Schema v1.2.")]
        public string LocationLevel { get; set; }
        public string LocationTypeCode { get; set; }
        [System.Obsolete("Ignored; removed from Final Schema v1.2.")]
        public string ManageMode { get; set; }
        [System.Obsolete("Ignored; LocationCode is the scan value in Final Schema v1.2.")]
        public bool IsScannable { get; set; }
        [System.Obsolete("Ignored; LocationCode is the scan value in Final Schema v1.2.")]
        public string ScanCode { get; set; }
        [System.Obsolete("Ignored; removed from Final Schema v1.2.")]
        public string CapacityMode { get; set; }
        [System.Obsolete("Ignored; removed from Final Schema v1.2.")]
        public decimal? CapacityQty { get; set; }
        [System.Obsolete("Ignored; removed from Final Schema v1.2.")]
        public decimal? MaxWeightKg { get; set; }
        public string PlantPolicy { get; set; }
        public string DedicatedPlantCode { get; set; }
        public bool AllowMixedMaterial { get; set; }
        public bool AllowMixedBatch { get; set; }
        public bool AllowMixedOrder { get; set; }
        public int PutawayPriority { get; set; }
        public int PickingPriority { get; set; }
        public int RouteSequence { get; set; }
        [System.Obsolete("Compatibility input mapped to IsActive.")]
        public string MasterStatus { get; set; }
        public string Remark { get; set; }
        public string Operator { get; set; }
    }
}
