using System;

namespace WMS_HU_Model.Models.Repack
{
    public class UnpackInnerResultDto
    {
        public string OperationNo { get; set; }
        public long MoveLogId { get; set; }
        public string SourceBoxHuCode { get; set; }
        public string InnerHuCode { get; set; }
        public string FromLocationCode { get; set; }
        public string ToLocationCode { get; set; }
        public int SourceRemainingInnerCount { get; set; }
        public bool IsSourceEmpty { get; set; }
        public bool IsDuplicate { get; set; }
        public DateTime OperationTime { get; set; }
    }
}
