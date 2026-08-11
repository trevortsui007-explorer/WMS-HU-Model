namespace WMS_HU_Model.Models.Repack
{
    public class PackInnerCommand
    {
        public long TargetBoxHuId { get; set; }

        public string TargetBoxHuCode { get; set; }

        public long InnerHuId { get; set; }

        public string InnerHuCode { get; set; }

        public string PlantCode { get; set; }

        public long FromLocationId { get; set; }

        public string FromLocationCode { get; set; }

        public long ToLocationId { get; set; }

        public string ToLocationCode { get; set; }

        public string OperationNo { get; set; }

        public string RequestId { get; set; }

        public string BusinessNo { get; set; }

        public string Operator { get; set; }

        public string Remark { get; set; }
    }
}