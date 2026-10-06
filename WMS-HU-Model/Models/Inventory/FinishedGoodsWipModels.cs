using System.Collections.Generic;

namespace WMS_HU_Model.Models.Inventory
{
    public class FinishedGoodsWipDto
    {
        public long BoxHUId { get; set; }
        public string BoxCode { get; set; }
        public string PlantCode { get; set; }
        public List<FinishedGoodsWipGroup> Groups { get; set; } = new List<FinishedGoodsWipGroup>();
    }

    public class FinishedGoodsWipGroup
    {
        public string MaterialCode { get; set; }
        public string MaterialRevision { get; set; }
        public string FlowId { get; set; }
        public string Message { get; set; }
        public List<FinishedGoodsWipStation> Stations { get; set; } = new List<FinishedGoodsWipStation>();
    }

    public class FinishedGoodsWipStation
    {
        public string StationId { get; set; }
        public string FlowItemNo { get; set; }
        public string StationCode { get; set; }
        public string StationName { get; set; }
        public string Sort { get; set; }
        public decimal? WipQty { get; set; }
        public string Unit { get; set; }
        public decimal? SetQty { get; set; }
        public decimal? PcsQty { get; set; }
    }
}
