namespace WMS_HU_Model.Models.MasterData
{
    public class CreateLocationRequest
    {
        public string WarehouseCode { get; set; }
        public string ZoneCode { get; set; }
        public string LocationCode { get; set; }
        public string LocationName { get; set; }
        public string ParentLocationCode { get; set; }
        public string LocationLevel { get; set; } = "BASE";

        public string LocationTypeCode { get; set; }
        public string ManageMode { get; set; } = "CAPACITY_LOCATION";

        public bool IsVirtual { get; set; } = false;

        public bool IsScannable { get; set; } = true;

        public string ScanCode { get; set; }
        public string CapacityMode { get; set; } = "STACK_POSITION";

        public decimal? CapacityQty { get; set; }
        public decimal? MaxWeightKg { get; set; }
        public string PlantPolicy { get; set; } = "SAME_PLANT";

        public string DedicatedPlantCode { get; set; }
        public bool AllowMixedMaterial { get; set; } = false;

        public bool AllowMixedBatch { get; set; } = false;

        public bool AllowMixedOrder { get; set; } = false;

        public int PutawayPriority { get; set; } = 100;

        public int PickingPriority { get; set; } = 100;

        public int RouteSequence { get; set; }
        public string Remark { get; set; }
        public string Operator { get; set; }
    }
}
