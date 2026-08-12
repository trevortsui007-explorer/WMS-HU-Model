namespace WMS_HU_Model.Models.Outbound
{
    public class OutboundBatchPlanRequest
    {
        public string WarehouseCode { get; set; }

        public string PlantCode { get; set; }

        public string MaterialCode { get; set; }

        public string MaterialRevision { get; set; }

        public string BatchNo { get; set; }

        public string ProductionOrderNo { get; set; }

        public decimal RequiredQty { get; set; }

        public string Uom { get; set; }

        public bool AllowOverPick { get; set; }
    }
}