using System.Collections.Generic;

namespace WMS_HU_Model.Models.Outbound.Formal
{
    public class OutboundPrepickDto
    {
        public string SapDeliveryNo { get; set; }
        public string PlantCode { get; set; }
        public string WarehouseCode { get; set; }
        public string Mode { get; set; }
        public string Message { get; set; }
        public OutboundPickingStateDto PickingState { get; set; }
        public List<OutboundPrepickGroup> Groups { get; set; } = new List<OutboundPrepickGroup>();
    }
    public class OutboundPrepickGroup
    {
        public decimal? PcsPerSetQty { get; set; }
        public string MaterialCode { get; set; }
        public string Uom { get; set; }
        public decimal RequiredQty { get; set; }
        public decimal EligibleQty { get; set; }
        public decimal ShortageQty { get; set; }
        public string PlanCode { get; set; }
        public List<OutboundPrepickBox> Boxes { get; set; } = new List<OutboundPrepickBox>();
    }
    public class OutboundPrepickBox
    {
        public long BoxHuId { get; set; }
        public string BoxCode { get; set; }
        public string LocationCode { get; set; }
        public string MaterialCode { get; set; }
        public string Uom { get; set; }
        public string MaterialRevision { get; set; }
        public string DateCode { get; set; }
        public int WeekSortKey { get; set; }
        public decimal Qty { get; set; }
        public decimal SuggestedQty { get; set; }
        public bool Eligible { get; set; }
        public bool OwnReservation { get; set; }
        public string Recommendation { get; set; }
        public List<string> Reasons { get; set; } = new List<string>();
        public List<OutboundPrepickPack> InnerPacks { get; set; } = new List<OutboundPrepickPack>();
    }
    public class OutboundPrepickPack
    {
        public long InnerHuId { get; set; }
        public string InnerCode { get; set; }
        public string MaterialRevision { get; set; }
        public string DateCode { get; set; }
        public int WeekSortKey { get; set; }
        public decimal Qty { get; set; }
        public decimal SuggestedQty { get; set; }
        public decimal RemainingQty { get { return Qty - SuggestedQty; } }
    }
    public class PrepickWarehouse
    {
        public long WarehouseId { get; set; }
        public string WarehouseCode { get; set; }
        public string SAPStorageLocation { get; set; }
    }
    public class PrepickPackMaster
    {
        public string MaterialCode { get; set; }
        public string MaterialRevision { get; set; }
        public decimal? PcsPerSetQty { get; set; }
    }
    // Raw read model; never returned directly by the API.
    public class PrepickStockRow
    {
        public long BoxHuId { get; set; }
        public string BoxCode { get; set; }
        public string LocationCode { get; set; }
        public long? InnerHuId { get; set; }
        public string InnerCode { get; set; }
        public string MaterialCode { get; set; }
        public string Uom { get; set; }
        public string MaterialRevision { get; set; }
        public string DateCode { get; set; }
        public decimal Qty { get; set; }
        public int? WeekSortKey { get; set; }
        public bool UnresolvedWeek { get; set; }
        public bool Held { get; set; }
        public bool NotShelved { get; set; }
        public bool BadStock { get; set; }
        public bool BadPackaging { get; set; }
        public bool OtherReservation { get; set; }
        public bool OwnReservation { get; set; }
    }
}
