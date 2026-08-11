namespace WMS_HU_Model.Models.Outbound
{
    public class OutboundQueueQuery
    {
        public string WarehouseCode { get; set; }

        public string PlantCode { get; set; }

        public string MaterialCode { get; set; }

        public string MaterialRevision { get; set; }

        public string BatchNo { get; set; }

        public string ProductionOrderNo { get; set; }

        public int Top { get; set; }
    }
}