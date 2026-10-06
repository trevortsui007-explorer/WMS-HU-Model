using System.Collections.Generic;

namespace WMS_HU_Model.Models.Outbound.Formal
{
    public class OutboundCombinedRequest : StartPickingRequest
    {
        public List<string> DeliveryNos { get; set; }
        public string PreviewVersion { get; set; }
    }

    public class OutboundCombinedDto
    {
        public string AllocationNo { get; set; }
        public string AllocationMode { get; set; }
        public string AllocationStatus { get; set; }
        public string LockSource { get; set; }
        public string PreviewVersion { get; set; }
        public string ResultType { get; set; }
        public bool CanLock { get; set; }
        public bool CanRelease { get; set; }
        public bool IsDuplicate { get; set; }
        public decimal RequiredQty { get; set; }
        public decimal AvailableQty { get; set; }
        public OutboundPrepickGroup Recommendation { get; set; }
        public List<OutboundOrderDto> Orders { get; set; } = new List<OutboundOrderDto>();
        public List<OutboundCombinedBoxDto> Boxes { get; set; } = new List<OutboundCombinedBoxDto>();
    }

    public class OutboundCombinedBoxDto
    {
        public long BoxHuId { get; set; }
        public string BoxCode { get; set; }
        public string LocationCode { get; set; }
        public long LocationId { get; set; }
        public string PickBatchNo { get; set; }
        public string DetailStatus { get; set; }
        public string MaterialCode { get; set; }
        public string Uom { get; set; }
        public int WeekSortKey { get; set; }
        public decimal Qty { get; set; }
        public bool Selected { get; set; }
        public List<OutboundCombinedShareDto> Shares { get; set; } = new List<OutboundCombinedShareDto>();
        public List<OutboundCombinedItemDto> Items { get; set; } = new List<OutboundCombinedItemDto>();
    }

    public class OutboundCombinedItemDto
    {
        public long HuItemId { get; set; }
        public long InnerHuId { get; set; }
        public string MaterialCode { get; set; }
        public string MaterialRevision { get; set; }
        public string DateCode { get; set; }
        public int WeekSortKey { get; set; }
        public string BatchNo { get; set; }
        public string SAPBatchNo { get; set; }
        public string Uom { get; set; }
        public decimal Qty { get; set; }
    }

    public class OutboundCombinedShareDto : OutboundCombinedItemDto
    {
        public long LineId { get; set; }
        public string SAPDeliveryNo { get; set; }
        public string SAPItemNo { get; set; }
    }
}
