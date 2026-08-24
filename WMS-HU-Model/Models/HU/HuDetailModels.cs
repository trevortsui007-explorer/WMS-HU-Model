using System;
using System.Collections.Generic;

namespace WMS_HU_Model.Models.HU
{
    public class HuDetailDto
    {
        public string RootHuCode { get; set; }
        public HuDto Hu { get; set; }
        public HuDetailRelationDto Parent { get; set; }
        public List<HuDetailRelationDto> Children { get; set; }
        public List<HuTreeFlatRow> Tree { get; set; }
        public HuLocationDto Location { get; set; }
        public HuHoldLogDto CurrentHold { get; set; }
        public List<HuDetailContentDto> Contents { get; set; }

        public HuDetailDto()
        {
            Children = new List<HuDetailRelationDto>();
            Tree = new List<HuTreeFlatRow>();
            Contents = new List<HuDetailContentDto>();
        }
    }

    public class HuDetailRelationDto
    {
        public long HuId { get; set; }
        public string HuCode { get; set; }
        public string HuType { get; set; }
        public string LifecycleStatus { get; set; }
        public bool IsHold { get; set; }
    }

    public class HuDetailContentDto
    {
        public long HuId { get; set; }
        public string HuCode { get; set; }
        public string HuType { get; set; }
        public string LifecycleStatus { get; set; }
        public bool IsHold { get; set; }
        public long? ItemId { get; set; }
        public string MaterialCode { get; set; }
        public string MaterialRevision { get; set; }
        public string BatchNo { get; set; }
        public string ProductionOrderNo { get; set; }
        public string DateCode { get; set; }
        public string WeekFormatCode { get; set; }
        public short? WeekYear { get; set; }
        public byte? WeekNo { get; set; }
        public int? WeekSortKey { get; set; }
        public string WeekRuleSource { get; set; }
        public string WeekRuleValue { get; set; }
        public decimal Qty { get; set; }
        public string Uom { get; set; }
        public string StockStatus { get; set; }
        public DateTime? CreatedTime { get; set; }
    }

    public class HuOperationLogDto
    {
        public long Id { get; set; }
        public string OperationNo { get; set; }
        public string OperationType { get; set; }
        public string HuCode { get; set; }
        public string RelatedHuCode { get; set; }
        public string BusinessNo { get; set; }
        public string RequestId { get; set; }
        public string DetailJson { get; set; }
        public string Operator { get; set; }
        public DateTime OperationTime { get; set; }
        public string Remark { get; set; }
    }

    public class HuHoldLogDto
    {
        public long Id { get; set; }
        public string HoldNo { get; set; }
        public string HuCode { get; set; }
        public string HoldType { get; set; }
        public string HoldReasonCode { get; set; }
        public string HoldReason { get; set; }
        public string SourceBizType { get; set; }
        public long? SourceBizId { get; set; }
        public string SourceBizNo { get; set; }
        public bool IsActive { get; set; }
        public DateTime HoldTime { get; set; }
        public string HoldUser { get; set; }
        public string HoldRequestId { get; set; }
        public DateTime? ReleaseTime { get; set; }
        public string ReleaseUser { get; set; }
        public string ReleaseReason { get; set; }
        public string ReleaseRequestId { get; set; }
    }
}
