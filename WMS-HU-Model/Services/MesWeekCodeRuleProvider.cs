using System;
using System.Collections.Generic;
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
            string sourceRevision = prodRev.Trim();
            if (!read.RuleValues.Any(x => !string.IsNullOrWhiteSpace(x)))
            {
                List<MesWeekCodeVersionRule> candidates = _repository.ReadOtherVersions(plantCode, prodNo, prodRev)
                    .Where(x => !string.IsNullOrWhiteSpace(x.MaterialRevision) && !string.IsNullOrWhiteSpace(x.RuleValue)
                        && !string.Equals(x.MaterialRevision.Trim(), sourceRevision, StringComparison.OrdinalIgnoreCase)).ToList();
                if (candidates.Count > 0)
                {
                    sourceRevision = candidates.Select(x => x.MaterialRevision.Trim())
                        .OrderByDescending(x => x, new RevisionComparer()).ThenByDescending(x => x, StringComparer.Ordinal).First();
                    read = new MesWeekCodeRuleReadResult
                    {
                        RuleValues = candidates.Where(x => string.Equals(x.MaterialRevision.Trim(), sourceRevision, StringComparison.OrdinalIgnoreCase))
                            .Select(x => x.RuleValue).ToList()
                    };
                }
            }
            string[] distinct = read.RuleValues.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.Ordinal).ToArray();
            string mesWeekCode = string.Join(" | ", distinct.OrderBy(x => x, StringComparer.Ordinal));
            if (distinct.Length == 0) return new MesWeekCodeRuleLookupResult { MESWeekCode = null, SourceMaterialRevision = sourceRevision, RawRowCount = read.RuleValues.Count, DistinctRuleCount = 0, RuleSource = "MES_FLOW", RuleValue = null, ErrorCode = "WEEK_RULE_NOT_FOUND", Message = "MES未维护周期格式。" };
            if (distinct.Length > 1) return new MesWeekCodeRuleLookupResult { MESWeekCode = mesWeekCode, SourceMaterialRevision = sourceRevision, RawRowCount = read.RuleValues.Count, DistinctRuleCount = distinct.Length, RuleSource = "MES_FLOW", RuleValue = mesWeekCode, ErrorCode = "WEEK_RULE_AMBIGUOUS", Message = "MES存在多个不同周期格式。" };
            return new MesWeekCodeRuleLookupResult { MESWeekCode = mesWeekCode, SourceMaterialRevision = sourceRevision, RawRowCount = read.RuleValues.Count, DistinctRuleCount = 1, RuleSource = "MES_FLOW", RuleValue = distinct[0] };
        }

        // Compare digit runs by magnitude without integer conversion or overflow.
        private class RevisionComparer : IComparer<string>
        {
            public int Compare(string left, string right)
            {
                int i = 0, j = 0;
                while (i < left.Length && j < right.Length)
                {
                    if (IsDigit(left[i]) && IsDigit(right[j]))
                    {
                        int leftEnd = i, rightEnd = j;
                        while (leftEnd < left.Length && IsDigit(left[leftEnd])) leftEnd++;
                        while (rightEnd < right.Length && IsDigit(right[rightEnd])) rightEnd++;
                        while (i < leftEnd && left[i] == '0') i++;
                        while (j < rightEnd && right[j] == '0') j++;
                        int comparison = (leftEnd - i).CompareTo(rightEnd - j);
                        if (comparison == 0) comparison = string.Compare(left, i, right, j, leftEnd - i, StringComparison.Ordinal);
                        if (comparison != 0) return comparison;
                        i = leftEnd; j = rightEnd;
                    }
                    else
                    {
                        int comparison = char.ToUpperInvariant(left[i]).CompareTo(char.ToUpperInvariant(right[j]));
                        if (comparison != 0) return comparison;
                        i++; j++;
                    }
                }
                return (left.Length - i).CompareTo(right.Length - j);
            }

            private static bool IsDigit(char value) { return value >= '0' && value <= '9'; }
        }
    }
}
