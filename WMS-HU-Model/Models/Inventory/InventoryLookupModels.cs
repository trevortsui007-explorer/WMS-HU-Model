using System.Collections.Generic;

namespace WMS_HU_Model.Models.Inventory
{
    public abstract class InventoryLookupDto
    {
        public string PlantCode { get; set; }
        public string QueryCode { get; set; }
        public string ResultType { get; set; }
        public string WarehouseCode { get; set; }
        public string ZoneCode { get; set; }
        public string LocationCode { get; set; }
    }

    public class InventoryLocationLookupDto : InventoryLookupDto
    {
        public List<InventoryLocationItemDto> Items { get; set; }

        public InventoryLocationLookupDto()
        {
            Items = new List<InventoryLocationItemDto>();
        }
    }

    public class InventoryOuterBoxLookupDto : InventoryLookupDto
    {
        public string LocationSource { get; set; }
        public string BoxCode { get; set; }
        public string HuStatus { get; set; }
        public bool IsHold { get; set; }
        public string MaterialCode { get; set; }
        public string MaterialRevision { get; set; }
        public List<InventoryPackItemDto> Items { get; set; }

        public InventoryOuterBoxLookupDto()
        {
            Items = new List<InventoryPackItemDto>();
        }
    }

    public class InventoryInnerPackLookupDto : InventoryLookupDto
    {
        public string LocationSource { get; set; }
        public string PackCode { get; set; }
        public string ParentBoxCode { get; set; }
        public string HuStatus { get; set; }
        public bool IsHold { get; set; }
        public string MaterialCode { get; set; }
        public string MaterialRevision { get; set; }
        public string BatchNo { get; set; }
        public string WeekCode { get; set; }
        public decimal TotalQty { get; set; }
        public List<InventoryPackItemDto> Items { get; set; }

        public InventoryInnerPackLookupDto()
        {
            Items = new List<InventoryPackItemDto>();
        }
    }

    public class InventoryLocationItemDto
    {
        public string PlantCode { get; set; }
        public string Uom { get; set; }
        public string LocationCode { get; set; }
        public string WarehouseCode { get; set; }
        public string BoxCode { get; set; }
        public string MaterialCode { get; set; }
        public string MaterialRevision { get; set; }
        public string WeekDisplay { get; set; }
        public decimal Qty { get; set; }
        public int PackCount { get; set; }
        public string Status { get; set; }
        public bool IsHold { get; set; }
    }

    public class InventoryPackItemDto
    {
        public string PackCode { get; set; }
        public string BatchNo { get; set; }
        public string WeekCode { get; set; }
        public decimal Qty { get; set; }
    }

    public class InventoryLookupLocationRecord
    {
        public long Id { get; set; }
        public string WarehouseCode { get; set; }
        public string ZoneCode { get; set; }
        public string LocationCode { get; set; }
    }

    public class InventoryItemFact
    {
        public long BoxHuId { get; set; }
        public string BoxCode { get; set; }
        public string BoxStatus { get; set; }
        public bool BoxIsHold { get; set; }
        public long PackHuId { get; set; }
        public string PackCode { get; set; }
        public string MaterialCode { get; set; }
        public string MaterialRevision { get; set; }
        public string BatchNo { get; set; }
        public short? WeekYear { get; set; }
        public byte? WeekNo { get; set; }
        public int? WeekSortKey { get; set; }
        public decimal Qty { get; set; }
    }
}
