namespace WMS_HU_Model.Models.Putaway
{
    public class PutawayCommand
    {
        public long PalletHuId { get; set; }
        public string PalletHuCode { get; set; }
        public long BoxHuId { get; set; }
        public string BoxHuCode { get; set; }
        public string PlantCode { get; set; }
        public long FromLocationId { get; set; }
        public string FromLocationCode { get; set; }
        public long ToLocationId { get; set; }
        public string ToLocationCode { get; set; }
        public string WarehouseCode { get; set; }
        public string OperationNo { get; set; }
        public string RequestId { get; set; }
        public string ScannedLocationCode { get; set; }
        public string BusinessNo { get; set; }
        public string Operator { get; set; }
        public string Remark { get; set; }
    }
}
