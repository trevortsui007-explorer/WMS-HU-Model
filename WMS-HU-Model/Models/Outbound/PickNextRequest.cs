namespace WMS_HU_Model.Models.Outbound
{
    public class PickNextRequest
    {
        public string WarehouseCode { get; set; }

        public string PlantCode { get; set; }

        public string MaterialCode { get; set; }

        public string MaterialRevision { get; set; }

        public string BatchNo { get; set; }

        public string ProductionOrderNo { get; set; }


        public string RequestId { get; set; }

        public string BusinessNo { get; set; }

        public string Operator { get; set; }

        public string Remark { get; set; }
    }
}