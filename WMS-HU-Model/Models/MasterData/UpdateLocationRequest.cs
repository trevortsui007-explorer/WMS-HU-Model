namespace WMS_HU_Model.Models.MasterData
{
    public class UpdateLocationRequest
    {
        public string ZoneCode { get; set; }
        public string LocationName { get; set; }
        public string ParentLocationCode { get; set; }
        public string LocationLevel { get; set; }
        public string LocationTypeCode { get; set; }
        public string ManageMode { get; set; }
        public bool IsScannable { get; set; }
        public string ScanCode { get; set; }
        public string CapacityMode { get; set; }
        public decimal? CapacityQty { get; set; }
        public decimal? MaxWeightKg { get; set; }
        public string PlantPolicy { get; set; }
        public string DedicatedPlantCode { get; set; }
        public bool AllowMixedMaterial { get; set; }
        public bool AllowMixedBatch { get; set; }
        public bool AllowMixedOrder { get; set; }
        public int PutawayPriority { get; set; }
        public int PickingPriority { get; set; }
        public int RouteSequence { get; set; }
        public string MasterStatus { get; set; }
        public string Remark { get; set; }
        public string Operator { get; set; }
    }
}
