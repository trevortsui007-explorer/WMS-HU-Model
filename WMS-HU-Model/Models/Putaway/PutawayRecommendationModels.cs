using System.Collections.Generic;

namespace WMS_HU_Model.Models.Putaway
{
    public class PutawayRecommendationResultDto
    {
        public string BoxHuCode { get; set; }
        public string WarehouseCode { get; set; }
        public string MaterialCode { get; set; }
        public string MaterialRevision { get; set; }
        public string RecommendationVersion { get; set; }
        public bool ITMode { get; set; }
        public string RecommendedLocationCode { get; set; }
        public List<PutawayLocationRecommendationDto> Recommendations { get; set; }

        public PutawayRecommendationResultDto()
        {
            Recommendations = new List<PutawayLocationRecommendationDto>();
        }
    }

    public class PutawayLocationRecommendationDto
    {
        public int Rank { get; set; }
        public string LocationCode { get; set; }
        public string RecommendationLevel { get; set; }
        public string Reason { get; set; }
        public string ITReason { get; set; }
        public int CurrentBoxCount { get; set; }
        public int? ClusterMinBoxCount { get; set; }
        public int? ClusterMaxBoxCount { get; set; }
        public int? ClusterFillGap { get; set; }
        public int? ClusterLocationCount { get; set; }
        public string ClusterInferenceStatus { get; set; }
        public string PhysicalRackGroupCode { get; set; }
        public string PhysicalRowCode { get; set; }
        public int? PositionSequence { get; set; }
        public string WeekAffinity { get; set; }
    }

    public class PutawayRecommendationSnapshot
    {
        public PutawayRecommendationBoxSnapshot Box { get; set; }
        public bool WarehouseAllowsPlant { get; set; }
        public List<PutawayRecommendationItemSnapshot> IncomingItems { get; set; }
        public List<PutawayRecommendationLocationSnapshot> Locations { get; set; }
        public List<PutawayRecommendationInventorySnapshot> Inventory { get; set; }

        public PutawayRecommendationSnapshot()
        {
            IncomingItems = new List<PutawayRecommendationItemSnapshot>();
            Locations = new List<PutawayRecommendationLocationSnapshot>();
            Inventory = new List<PutawayRecommendationInventorySnapshot>();
        }
    }

    public class PutawayRecommendationBoxSnapshot
    {
        public long HuId { get; set; }
        public string HuCode { get; set; }
        public string HuType { get; set; }
        public string PlantCode { get; set; }
        public string LifecycleStatus { get; set; }
        public bool IsHold { get; set; }
        public long? EffectiveLocationId { get; set; }
        public string EffectiveLocationCode { get; set; }
        public string EffectiveWarehouseCode { get; set; }
        public string EffectiveZoneCode { get; set; }
        public string LocationSource { get; set; }
    }

    public class PutawayRecommendationItemSnapshot
    {
        public string MaterialCode { get; set; }
        public string MaterialRevision { get; set; }
        public short? WeekYear { get; set; }
        public byte? WeekNo { get; set; }
        public int? WeekSortKey { get; set; }
    }

    public class PutawayRecommendationLocationSnapshot
    {
        public long LocationId { get; set; }
        public string LocationCode { get; set; }
        public string PlantPolicy { get; set; }
        public string DedicatedPlantCode { get; set; }
        public bool AllowMixedMaterial { get; set; }
        public int PutawayPriority { get; set; }
        public string PhysicalRackGroupCode { get; set; }
        public string PhysicalRowCode { get; set; }
        public int? PositionSequence { get; set; }
    }

    public class PutawayRecommendationInventorySnapshot
    {
        public long BoxHuId { get; set; }
        public string BoxHuCode { get; set; }
        public string PlantCode { get; set; }
        public long LocationId { get; set; }
        public string MaterialCode { get; set; }
        public string MaterialRevision { get; set; }
        public short? WeekYear { get; set; }
        public byte? WeekNo { get; set; }
        public int? WeekSortKey { get; set; }
    }
}
