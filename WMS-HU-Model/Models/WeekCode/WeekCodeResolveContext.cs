using System;

namespace WMS_HU_Model.Models.WeekCode
{
    public class WeekCodeResolveContext
    {
        public string PlantCode { get; set; }
        public string MaterialCode { get; set; }
        public string MaterialRevision { get; set; }
        public string RuleSource { get; set; }
        public string RuleValue { get; set; }
        public string RuleFailureCode { get; set; }
        public string RuleFailureMessage { get; set; }
        public string DateCode { get; set; }
        public DateTime? PackCreatedTime { get; set; }
    }
}
