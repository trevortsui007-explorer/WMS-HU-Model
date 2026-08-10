namespace WMS_HU_Model.Models.Putaway
{
    public class PutawayBoxRequest
    {
        public string PalletHuCode { get; set; }

        public string BoxHuCode { get; set; }

        public string WarehouseCode { get; set; }

        public string ToLocationCode { get; set; }

        public string ScannedLocationCode { get; set; }

        public string RequestId { get; set; }

        public string BusinessNo { get; set; }

        public string Operator { get; set; }

        public string Remark { get; set; }
    }
}