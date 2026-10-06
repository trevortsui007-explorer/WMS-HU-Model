namespace WMS_HU_Model.Models.HU
{
    public class HuIdentityDto
    {
        public long HuId { get; set; }
        public string HuCode { get; set; }
        public string HuType { get; set; }
        public string PlantCode { get; set; }
        public string IdentifierType { get; set; }
        public string IdentifierValue { get; set; }
        public string CustomerCode { get; set; }
        public bool MatchedHuCode { get; set; }
    }
}
