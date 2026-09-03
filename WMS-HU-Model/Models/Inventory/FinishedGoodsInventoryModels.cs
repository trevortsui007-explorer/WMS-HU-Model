using System;
using System.Collections.Generic;

namespace WMS_HU_Model.Models.Inventory
{
    public class FinishedGoodsInventoryQuery
    {
        public string PlantCode { get; set; }
        public string MaterialCode { get; set; }
        public string MaterialRevision { get; set; }
        public string BoxCode { get; set; }
        public string InnerPackCode { get; set; }
        public string LocationCode { get; set; }
        public string WeekCode { get; set; }
        public string StockStatus { get; set; }
        public string InboundState { get; set; }
        public bool? IsHold { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }

    public class FinishedGoodsInventoryResultDto
    {
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public List<FinishedGoodsInventoryRowDto> Items { get; set; }

        public FinishedGoodsInventoryResultDto()
        {
            Items = new List<FinishedGoodsInventoryRowDto>();
        }
    }

    public class FinishedGoodsInventoryRowDto
    {
        public long BoxHuId { get; set; }
        public string BoxCode { get; set; }
        public string PlantCode { get; set; }
        public string MaterialCode { get; set; }
        public string MaterialRevision { get; set; }
        public decimal TotalQty { get; set; }
        public string Uom { get; set; }
        public string WeekCodeSummary { get; set; }
        public int WeekCodeCount { get; set; }
        public string EffectiveLocationCode { get; set; }
        public string WarehouseCode { get; set; }
        public string ZoneCode { get; set; }
        public string LocationTypeCode { get; set; }
        public bool IsLocationInherited { get; set; }
        public string StockStatus { get; set; }
        public string LifecycleStatus { get; set; }
        public bool IsHold { get; set; }
        public string HoldReasonCode { get; set; }
        public string HoldReason { get; set; }
        public string ParentPalletCode { get; set; }
        public int InnerPackCount { get; set; }
        public DateTime CreatedTime { get; set; }
    }

    public class FinishedGoodsInventoryPreviewDto
    {
        public FinishedGoodsInventoryRowDto Box { get; set; }
        public List<FinishedGoodsInventoryWeekDto> WeekCodes { get; set; }
        public List<FinishedGoodsInventoryPackDto> InnerPacks { get; set; }

        public FinishedGoodsInventoryPreviewDto()
        {
            WeekCodes = new List<FinishedGoodsInventoryWeekDto>();
            InnerPacks = new List<FinishedGoodsInventoryPackDto>();
        }
    }

    public class FinishedGoodsInventoryWeekDto
    {
        public string WeekCode { get; set; }
        public int? WeekSortKey { get; set; }
        public int PackCount { get; set; }
        public decimal Qty { get; set; }
    }

    public class FinishedGoodsInventoryPackDto
    {
        public long PackHuId { get; set; }
        public string PackCode { get; set; }
        public decimal Qty { get; set; }
        public string Uom { get; set; }
        public string DateCode { get; set; }
        public string WeekCode { get; set; }
        public int? WeekSortKey { get; set; }
        public string StockStatus { get; set; }
        public bool IsHold { get; set; }
    }
}
