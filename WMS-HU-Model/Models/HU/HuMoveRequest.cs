namespace WMS_HU_Model.Models.HU
{
    public class HuMoveRequest
    {
        public string WarehouseCode { get; set; }
        public string ToLocationCode { get; set; }
        public string ExpectedFromLocationCode { get; set; }
        public string MoveType { get; set; }
        public string ScannedLocationCode { get; set; }
        public string BusinessNo { get; set; }
        public string TaskNo { get; set; }
        public string RequestId { get; set; }
        public string ReasonCode { get; set; }
        public string Operator { get; set; }
        public string Remark { get; set; }
    }
}
