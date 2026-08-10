using System.Collections.Generic;

namespace WMS_HU_Model.Models.HU
{
    public class HuTreeNodeDto
    {
        public long Id { get; set; }

        public string HuCode { get; set; }

        public string HuType { get; set; }

        public string PlantCode { get; set; }

        public string LifecycleStatus { get; set; }

        public bool IsHold { get; set; }

        public List<HuItemDto> Items { get; set; }

        public List<HuTreeNodeDto> Children { get; set; }


        public HuTreeNodeDto()
        {
            Items = new List<HuItemDto>();

            Children =
                new List<HuTreeNodeDto>();
        }
    }
}