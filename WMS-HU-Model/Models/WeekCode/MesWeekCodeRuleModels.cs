using System.Collections.Generic;

namespace WMS_HU_Model.Models.WeekCode
{
    public class MesWeekCodeVersionRule
    {
        public string MaterialRevision { get; set; }
        public string RuleValue { get; set; }
    }

    public class MesWeekCodeRuleReadResult
    {
        public List<string> RuleValues { get; set; }
        public MesWeekCodeRuleReadResult() { RuleValues = new List<string>(); }
    }

    public class MesWeekCodeRuleLookupResult
    {
        public string MESWeekCode { get; set; }
        public string SourceMaterialRevision { get; set; }
        public int RawRowCount { get; set; }
        public int DistinctRuleCount { get; set; }
        public string RuleSource { get; set; }
        public string RuleValue { get; set; }
        public string ErrorCode { get; set; }
        public string Message { get; set; }
        public bool IsSuccess { get { return ErrorCode == null; } }
    }
}
