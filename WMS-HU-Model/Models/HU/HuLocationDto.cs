using System;

namespace WMS_HU_Model.Models.HU
{
    public class HuLocationDto
    {
        public long HuId { get; set; }
        public string HuCode { get; set; }
        public string HuType { get; set; }
        public string PlantCode { get; set; }

        // -------------------------
        // 直接位置
        // -------------------------

        public long? DirectLocationId { get; set; }
        public string DirectLocationCode { get; set; }
        public string DirectWarehouseCode { get; set; }
        public string DirectZoneCode { get; set; }
        public DateTime? DirectLocationSince { get; set; }

        // -------------------------
        // 有效位置
        // -------------------------

        public long? EffectiveLocationId { get; set; }
        public string EffectiveLocationCode { get; set; }
        public string EffectiveWarehouseCode { get; set; }
        public string EffectiveZoneCode { get; set; }

        // -------------------------
        // 有效位置来源HU
        // -------------------------

        public long? LocationSourceHuId { get; set; }
        public string LocationSourceHuCode { get; set; }
        public bool IsInherited { get; set; }
        public long? LastMoveLogId { get; set; }
        public string LastMoveNo { get; set; }
        public string LastMoveType { get; set; }
    }
}
