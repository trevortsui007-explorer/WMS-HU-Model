using System;
using System.Collections.Generic;

namespace WMS_HU_Model.Models.Inbound
{
    public class FinishedGoodsInboundRecordQuery
    {
        public string DateFrom { get; set; }
        public string DateTo { get; set; }
        public DateTime? FromTime { get; set; }
        public DateTime? ToTimeExclusive { get; set; }
        public string PlantCode { get; set; }
        public string SourceSystem { get; set; }
        public string SourceType { get; set; }
        public string SourceEntityId { get; set; }
        public string PalletCode { get; set; }
        public string BoxCode { get; set; }
        public string MaterialCode { get; set; }
        public string RequestId { get; set; }
        public string ExceptionCategory { get; set; }
        public bool ExceptionOnly { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }

    public class FinishedGoodsInboundRecordResultDto
    {
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public List<FinishedGoodsInboundRecordDto> Items { get; set; }

        public FinishedGoodsInboundRecordResultDto() { Items = new List<FinishedGoodsInboundRecordDto>(); }
    }

    public class FinishedGoodsInboundRecordDto
    {
        public long ReceiptId { get; set; }
        public DateTime ReceiveTime { get; set; }
        public string SourceSystem { get; set; }
        public string SourceType { get; set; }
        public string SourceEntityId { get; set; }
        public string PlantCode { get; set; }
        public string PalletSummary { get; set; }
        public int PalletCount { get; set; }
        public int BoxCount { get; set; }
        public int InnerPackCount { get; set; }
        public decimal TotalQty { get; set; }
        public string MaterialSummary { get; set; }
        public int MaterialCount { get; set; }
        public int HoldBoxCount { get; set; }
        public int WeekCodeExceptionBoxCount { get; set; }
        public int ExceptionBoxCount { get; set; }
        public string RequestId { get; set; }
        public string Operator { get; set; }
        public string Remark { get; set; }
        public string DisplayStatus { get; set; }
    }

    public class FinishedGoodsInboundDetailDto
    {
        public FinishedGoodsInboundRecordDto Record { get; set; }
        public List<FinishedGoodsInboundPalletDto> Pallets { get; set; }
        public List<FinishedGoodsInboundBoxDto> Boxes { get; set; }
        public int BoxLimit { get; set; }
        public bool HasMoreBoxes { get; set; }

        public FinishedGoodsInboundDetailDto()
        {
            Pallets = new List<FinishedGoodsInboundPalletDto>();
            Boxes = new List<FinishedGoodsInboundBoxDto>();
        }
    }

    public class FinishedGoodsInboundPalletDto
    {
        public long HuId { get; set; }
        public string PalletCode { get; set; }
        public string LifecycleStatus { get; set; }
        public string DirectLocationCode { get; set; }
        public string EffectiveLocationCode { get; set; }
        public bool IsLocationInherited { get; set; }
        public string LocationSourceHuCode { get; set; }
    }

    public class FinishedGoodsInboundBoxDto
    {
        public long HuId { get; set; }
        public string BoxCode { get; set; }
        public string LifecycleStatus { get; set; }
        public string MaterialCode { get; set; }
        public string MaterialRevision { get; set; }
        public decimal TotalQty { get; set; }
        public string Uom { get; set; }
        public int InnerPackCount { get; set; }
        public string WeekCodeSummary { get; set; }
        public bool IsHold { get; set; }
        public string HoldReasonCode { get; set; }
        public string HoldReason { get; set; }
        public bool HasWeekCodeException { get; set; }
        public string DirectLocationCode { get; set; }
        public string EffectiveLocationCode { get; set; }
        public bool IsLocationInherited { get; set; }
        public string LocationSourceHuCode { get; set; }
    }
}
