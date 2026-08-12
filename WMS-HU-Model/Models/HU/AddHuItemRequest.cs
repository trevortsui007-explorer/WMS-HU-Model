namespace WMS_HU_Model.Models.HU
{
    public class AddHuItemRequest
    {
        public string MaterialCode { get; set; }
        public string MaterialRevision { get; set; }
        public string BatchNo { get; set; }
        public string SapBatchNo { get; set; }
        public string ProductionOrderNo { get; set; }
        public string CustomerCode { get; set; }
        public string SalesOrderNo { get; set; }
        public string SalesOrderItem { get; set; }
        public string DateCode { get; set; }
        public decimal Qty { get; set; }
        public string Uom { get; set; }
        public string StockStatus { get; set; }
        public string Operator { get; set; }
        public string RequestId { get; set; }
        public string Remark { get; set; }
    }
}
