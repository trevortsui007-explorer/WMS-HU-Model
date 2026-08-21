using System;
using System.Linq;
using WMS_HU_Model.Models.WeekCode;
using WMS_HU_Model.Repositories;

namespace WMS_HU_Model.Services
{
    public class MesWeekCodeRuleProvider
    {
        private readonly IMesWeekCodeRuleRepository _repository;
        public MesWeekCodeRuleProvider(IMesWeekCodeRuleRepository repository) { _repository = repository ?? throw new ArgumentNullException(nameof(repository)); }
        public MesWeekCodeRuleLookupResult Resolve(string plantCode, string prodNo, string prodRev)
        {
            MesWeekCodeRuleReadResult read = _repository.Read(plantCode, prodNo, prodRev);
            string[] distinct = read.RuleValues.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.Ordinal).ToArray();
            if (distinct.Length == 0) return new MesWeekCodeRuleLookupResult { RawRowCount = read.RuleValues.Count, DistinctRuleCount = 0, ErrorCode = "WEEK_RULE_NOT_FOUND", Message = "WEEK_RULE_NOT_FOUND" };
            if (distinct.Length > 1) return new MesWeekCodeRuleLookupResult { RawRowCount = read.RuleValues.Count, DistinctRuleCount = distinct.Length, ErrorCode = "WEEK_RULE_AMBIGUOUS", Message = "WEEK_RULE_AMBIGUOUS" };
            return new MesWeekCodeRuleLookupResult { RawRowCount = read.RuleValues.Count, DistinctRuleCount = 1, RuleSource = "MES_FLOW", RuleValue = distinct[0] };
        }
    }
}
