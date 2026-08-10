namespace WMS_HU_Model.Models.HU
{
    public class HuItemDto
    {
        public long Id { get; set; }

        public string MaterialCode { get; set; }

        public string MaterialRevision { get; set; }

        public string BatchNo { get; set; }

        public string SapBatchNo { get; set; }

        public string ProductionOrderNo { get; set; }

        public string DateCode { get; set; }

        public decimal Qty { get; set; }

        public string Uom { get; set; }

        public string StockStatus { get; set; }
    }
}