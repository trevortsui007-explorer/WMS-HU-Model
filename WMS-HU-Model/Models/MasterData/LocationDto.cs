namespace WMS_HU_Model.Models.MasterData
{
    public class LocationDto
    {
        public long Id { get; set; }
        public string WarehouseCode { get; set; }
        public string WarehouseName { get; set; }
        public string ZoneCode { get; set; }
        public string ZoneName { get; set; }
        public string LocationCode { get; set; }
        public string LocationName { get; set; }
        [System.Obsolete("API compatibility only; removed from Final Schema v1.2.")]
        public string ParentLocationCode { get; set; }
        [System.Obsolete("API compatibility only; removed from Final Schema v1.2.")]
        public string LocationLevel { get; set; }
        public string LocationTypeCode { get; set; }
        [System.Obsolete("API compatibility only; removed from Final Schema v1.2.")]
        public string ManageMode { get; set; }
        public bool IsVirtual { get; set; }
        public bool IsActive { get; set; }
        [System.Obsolete("API compatibility only; removed from Final Schema v1.2.")]
        public bool IsScannable { get; set; }
        [System.Obsolete("Compatibility alias derived from LocationCode.")]
        public string ScanCode { get; set; }
        [System.Obsolete("API compatibility only; removed from Final Schema v1.2.")]
        public string CapacityMode { get; set; }
        [System.Obsolete("API compatibility only; removed from Final Schema v1.2.")]
        public decimal? CapacityQty { get; set; }
        [System.Obsolete("API compatibility only; removed from Final Schema v1.2.")]
        public decimal? MaxWeightKg { get; set; }
        public string PlantPolicy { get; set; }
        public string DedicatedPlantCode { get; set; }
        public bool AllowMixedMaterial { get; set; }
        public bool AllowMixedBatch { get; set; }
        public bool AllowMixedOrder { get; set; }
        public int PutawayPriority { get; set; }
        public int PickingPriority { get; set; }
        public int RouteSequence { get; set; }
        public string PhysicalRackGroupCode { get; set; }
        public string PhysicalRowCode { get; set; }
        public int? PositionSequence { get; set; }
        [System.Obsolete("Compatibility alias derived from IsActive.")]
        public string MasterStatus { get; set; }
        public string Remark { get; set; }
        public int CurrentHuCount { get; set; }
        public bool IsSystemProtected { get; set; }
    }
}
