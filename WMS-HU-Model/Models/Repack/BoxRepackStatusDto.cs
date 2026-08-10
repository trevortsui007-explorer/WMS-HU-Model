using System.Collections.Generic;

namespace WMS_HU_Model.Models.Repack
{
    public class BoxRepackStatusDto
    {
        public long BoxHuId { get; set; }

        public string BoxHuCode { get; set; }

        public string PlantCode { get; set; }

        public string LifecycleStatus { get; set; }

        public bool IsHold { get; set; }


        public string LocationCode { get; set; }

        public string WarehouseCode { get; set; }

        public string ZoneCode { get; set; }


        public int InnerPackCount { get; set; }

        public decimal TotalQty { get; set; }


        public List<string> InnerPackCodes { get; set; }


        public BoxRepackStatusDto()
        {
            InnerPackCodes =
                new List<string>();
        }
    }
}