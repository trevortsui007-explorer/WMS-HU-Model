using System;

namespace WMS_HU_Model.Models.WeekCode
{
    public class WeekCodeSnapshot
    {
        public string WeekFormatCode { get; set; }
        public short WeekYear { get; set; }
        public byte WeekNo { get; set; }
        public int WeekSortKey { get; set; }
        public string WeekRuleSource { get; set; }
        public string WeekRuleValue { get; set; }
        public DateTime WeekResolvedTime { get; set; }
    }
}
