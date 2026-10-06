namespace WMS_HU_Model.Models.Inbound
{
    public class MesFinishedGoodsInboundTriggerRequest
    {
        public string PlanStockId { get; set; }
    }

    public class MesFinishedGoodsInboundTriggerResult
    {
        public string PlanStockId { get; set; }
        public long ReceiptId { get; set; }
        public bool IsDuplicate { get; set; }
    }
}
