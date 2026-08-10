using System.Collections.Generic;

namespace WMS_HU_Model.Models.Putaway
{
    public class PalletPutawayStatusDto
    {
        public long PalletHuId { get; set; }

        public string PalletHuCode { get; set; }

        public string PlantCode { get; set; }

        public string CurrentLocationCode { get; set; }

        public string CurrentZoneCode { get; set; }

        public int RemainingBoxCount { get; set; }

        public bool IsEmpty { get; set; }

        public List<string> RemainingBoxCodes { get; set; }


        public PalletPutawayStatusDto()
        {
            RemainingBoxCodes =
                new List<string>();
        }
    }
}