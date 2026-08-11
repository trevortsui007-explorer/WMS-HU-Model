namespace WMS_HU_Model.Models.Repack
{
    public class UnpackInnerRequest
    {
        public string SourceBoxHuCode { get; set; }

        public string InnerHuCode { get; set; }

        public string WarehouseCode { get; set; }

        public string ToLocationCode { get; set; }

        public string ScannedLocationCode { get; set; }

        public string RequestId { get; set; }

        public string BusinessNo { get; set; }

        public string Operator { get; set; }

        public string Remark { get; set; }
    }
}