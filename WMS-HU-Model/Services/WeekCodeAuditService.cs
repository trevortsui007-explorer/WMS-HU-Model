using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.WeekCode;
using WMS_HU_Model.Repositories;

namespace WMS_HU_Model.Services
{
    public class WeekCodeAuditService : IWeekCodeAuditService
    {
        private const int MaxFacts = 5000;
        private readonly IWeekCodeAuditRepository _repository;
        private readonly IMesFinishedGoodsInboundRepository _mesRepository;

        public WeekCodeAuditService(IWeekCodeAuditRepository repository, IMesFinishedGoodsInboundRepository mesRepository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _mesRepository = mesRepository ?? throw new ArgumentNullException(nameof(mesRepository));
        }

        public OperationResult<WeekCodeAuditResultDto> Query(WeekCodeAuditQuery query)
        {
            string error = NormalizeAndValidate(query);
            if (error != null) return OperationResult<WeekCodeAuditResultDto>.Fail(error);
            List<WeekCodeAuditFact> facts = _repository.QueryFacts(query, MaxFacts + 1);
            if (facts.Count > MaxFacts) return OperationResult<WeekCodeAuditResultDto>.Fail("候选记录超过5000条，请增加厂别或物料筛选条件。");
            EnrichPackCreatedTimes(facts);
            IEnumerable<WeekCodeAuditItemDto> items = facts.Select(Evaluate).Where(x => x != null);
            if (!string.IsNullOrWhiteSpace(query.AnomalyType)) items = items.Where(x => string.Equals(x.AnomalyType, query.AnomalyType, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(query.RiskLevel)) items = items.Where(x => string.Equals(x.RiskLevel, query.RiskLevel, StringComparison.OrdinalIgnoreCase));
            if (query.IsHold.HasValue) items = items.Where(x => x.IsHold == query.IsHold.Value);
            List<WeekCodeAuditItemDto> filtered = items.OrderByDescending(x => RiskOrder(x.RiskLevel)).ThenBy(x => x.BoxHUId).ThenBy(x => x.DateCode).ToList();
            return OperationResult<WeekCodeAuditResultDto>.Success(new WeekCodeAuditResultDto
            {
                TotalCount = filtered.Count,
                MaterialSummary = filtered.GroupBy(x => new { x.MaterialCode, x.MaterialRevision })
                    .Select(group => new WeekCodeAuditMaterialDto
                    {
                        MaterialCode = group.Key.MaterialCode,
                        MaterialRevision = group.Key.MaterialRevision,
                        MesInstructionRule = string.Join("；", group.GroupBy(x => x.PlantCode)
                            .OrderBy(x => x.Key).Select(x => new { Plant = x.Key, Rule = x.First().MesInstructionRule })
                            .Where(x => !string.IsNullOrWhiteSpace(x.Rule))
                            .Select(x => group.Select(y => y.PlantCode).Distinct().Count() > 1 ? x.Plant + "：" + x.Rule : x.Rule)),
                        BoxCount = group.Select(x => x.BoxHUId).Distinct().Count()
                    }).OrderByDescending(x => x.BoxCount).ThenBy(x => x.MaterialCode).ThenBy(x => x.MaterialRevision).ToList(),
                Page = query.Page,
                PageSize = query.PageSize,
                Items = filtered.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList()
            });
        }

        private void EnrichPackCreatedTimes(List<WeekCodeAuditFact> facts)
        {
            List<string> ids = facts.SelectMany(x => x.PackSources)
                .Where(x => string.Equals(x.SourceSystem, "MES", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(x.SourceEntityId))
                .Select(x => x.SourceEntityId.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            Dictionary<string, DateTime?> createdTimes = _mesRepository.GetPackCreatedTimes(ids);
            foreach (WeekCodeAuditFact fact in facts)
            {
                List<DateTime> groupTimes = fact.PackSources
                    .Where(x => string.Equals(x.SourceSystem, "MES", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(x.SourceEntityId))
                    .Select(x => createdTimes.ContainsKey(x.SourceEntityId) ? createdTimes[x.SourceEntityId] : null)
                    .Where(x => x.HasValue)
                    .Select(x => x.Value)
                    .OrderBy(x => x)
                    .ToList();
                // A group can contain multiple packs. The earliest MES createdt is the deterministic risk-analysis baseline.
                if (groupTimes.Count > 0) fact.PackCreatedTime = groupTimes[0];
            }
        }

        private static string NormalizeAndValidate(WeekCodeAuditQuery query)
        {
            if (query == null) return "查询参数不能为空。";
            query.PlantCode = Trim(query.PlantCode); query.MaterialCode = Trim(query.MaterialCode); query.MaterialRevision = Trim(query.MaterialRevision);
            query.AnomalyType = Trim(query.AnomalyType); query.RiskLevel = Trim(query.RiskLevel);
            if (query.Page == 0) query.Page = 1;
            if (query.PageSize == 0) query.PageSize = 50;
            if (query.Page < 1) return "page必须大于等于1。";
            if (query.PageSize < 1 || query.PageSize > 200) return "pageSize必须在1到200之间。";
            return null;
        }

        private static WeekCodeAuditItemDto Evaluate(WeekCodeAuditFact fact)
        {
            WeekCodeAuditItemDto item = Copy(fact);
            ApplyRecommendation(item, fact);
            string holdReason = (fact.HoldReason ?? string.Empty).ToUpperInvariant();
            if (fact.IsHold && string.Equals(fact.HoldReasonCode, "WEEKCODE_UNRESOLVED", StringComparison.OrdinalIgnoreCase))
            {
                if (holdReason.Contains("PACK_WEEKCODE_"))
                {
                    item.AnomalyType = holdReason.Contains("INVALID_DATECODE") ? "INVALID_DATECODE"
                        : holdReason.Contains("NOT_FOUND") || holdReason.Contains("DISABLED") ? "RULE_MISSING" : "RULE_UNSUPPORTED";
                    item.RiskLevel = "HIGH";
                    item.Reason = fact.HoldReason;
                }
                else if (holdReason.Contains("WEEK_RULE_NOT_FOUND") || holdReason.Contains("WEEK_RULE_MISSING"))
                {
                    item.AnomalyType = "RULE_MISSING"; item.RiskLevel = "HIGH";
                    item.Reason = "MES周码规则缺失，请到MES指示库维护；当前箱已按WEEKCODE_UNRESOLVED Hold。";
                }
                else if (holdReason.Contains("INVALID_DATECODE") || holdReason.Contains("INVALID_WEEK") || holdReason.Contains("MISSING_DATECODE"))
                {
                    item.AnomalyType = "INVALID_DATECODE"; item.RiskLevel = "HIGH";
                    item.Reason = "DateCode无法按当前规则解析：" + Display(fact.DateCode) + "。";
                }
                else if (holdReason.Contains("WEEK_RULE_MANUAL_REQUIRED"))
                {
                    item.AnomalyType = "RULE_UNSUPPORTED"; item.RiskLevel = "HIGH";
                    item.Reason = "MES周期格式为“见备注”，请到MES指示库维护明确的周期格式。";
                }
                else if (holdReason.Contains("WEEK_RULE_INVALID_CONFIG"))
                {
                    item.AnomalyType = "RULE_UNSUPPORTED"; item.RiskLevel = "HIGH";
                    item.Reason = "MES周期格式配置不是可解析规则：" + Display(string.IsNullOrWhiteSpace(fact.WeekRuleValue) ? FailureDetail(fact.HoldReason) : fact.WeekRuleValue) + "。请到MES指示库维护。";
                }
                else
                {
                    item.AnomalyType = "RULE_UNSUPPORTED"; item.RiskLevel = "HIGH";
                    item.Reason = "当前周码规则暂不支持解析：" + Display(string.IsNullOrWhiteSpace(fact.WeekRuleValue) ? FailureDetail(fact.HoldReason) : fact.WeekRuleValue) + "。";
                }
                return item;
            }

            Candidate candidate;
            if (TryCandidate(fact, out candidate) && fact.PackCreatedTime.HasValue && ValidWeek(fact.WeekYear, fact.WeekNo))
            {
                DateTime currentStart;
                DateTime candidateStart;
                if (TryIsoWeekStart(fact.WeekYear.Value, fact.WeekNo.Value, out currentStart)
                    && TryIsoWeekStart(candidate.Year, candidate.Week, out candidateStart))
                {
                    double currentDistance = Math.Abs((currentStart - fact.PackCreatedTime.Value.Date).TotalDays);
                    double candidateDistance = Math.Abs((candidateStart - fact.PackCreatedTime.Value.Date).TotalDays);
                    if (currentDistance > 180 && candidateDistance <= 90)
                    {
                        item.AnomalyType = "SUSPICIOUS_RULE_DIRECTION"; item.RiskLevel = "HIGH";
                        item.Reason = "当前规则" + fact.WeekRuleValue + "解析为" + Week(fact.WeekYear.Value, fact.WeekNo.Value) + "；候选规则" + candidate.Rule + "可解析为" + Week(candidate.Year, candidate.Week) + "，且与包装时间" + fact.PackCreatedTime.Value.ToString("yyyy-MM-dd") + "明显更接近。";
                        return item;
                    }
                }
            }
            if (fact.PackCreatedTime.HasValue && fact.WeekYear.HasValue)
            {
                int difference = fact.WeekYear.Value - fact.PackCreatedTime.Value.Year;
                if (Math.Abs(difference) >= 2)
                {
                    item.AnomalyType = "SUSPICIOUS_YEAR"; item.RiskLevel = "HIGH";
                    item.Reason = "周码解析结果为" + Week(fact.WeekYear.Value, fact.WeekNo) + "，但包装时间为" + fact.PackCreatedTime.Value.ToString("yyyy-MM-dd") + "，年份偏差明显。";
                    return item;
                }
                if (difference == 1 && !(fact.PackCreatedTime.Value.Month == 12 && fact.WeekNo.HasValue && fact.WeekNo.Value <= 2))
                {
                    item.AnomalyType = "SUSPICIOUS_YEAR"; item.RiskLevel = "MEDIUM";
                    item.Reason = "周码解析结果为" + Week(fact.WeekYear.Value, fact.WeekNo) + "，比包装时间" + fact.PackCreatedTime.Value.ToString("yyyy-MM-dd") + "晚一年，需要人工确认。";
                    return item;
                }
            }
            return null;
        }

        private static WeekCodeAuditItemDto Copy(WeekCodeAuditFact fact)
        {
            return new WeekCodeAuditItemDto
            {
                InboundRuleValue = fact.InboundRuleValue,
                MesInstructionRule = fact.MesInstructionRule,
                BoxHUId = fact.BoxHUId, BoxCode = fact.BoxCode, PlantCode = fact.PlantCode, MaterialCode = fact.MaterialCode, MaterialRevision = fact.MaterialRevision,
                DateCode = fact.DateCode, PackCreatedTime = fact.PackCreatedTime, WeekRuleSource = fact.WeekRuleSource, WeekRuleValue = fact.WeekRuleValue,
                WeekYear = fact.WeekYear, WeekNo = fact.WeekNo, WeekSortKey = fact.WeekSortKey, IsHold = fact.IsHold, PackCount = fact.PackCount, TotalQty = fact.TotalQty
            };
        }

        private static void ApplyRecommendation(WeekCodeAuditItemDto item, WeekCodeAuditFact fact)
        {
            string code = Trim(fact.DateCode);
            if (code == null || code.Length != 4 || !code.All(x => x >= '0' && x <= '9') || !fact.PackCreatedTime.HasValue) return;
            int first = int.Parse(code.Substring(0, 2), CultureInfo.InvariantCulture);
            int second = int.Parse(code.Substring(2, 2), CultureInfo.InvariantCulture);
            var candidates = new[]
            {
                new Candidate { Rule = "YYWW", Year = 2000 + first, Week = second },
                new Candidate { Rule = "WWYY", Year = 2000 + second, Week = first }
            };
            var plausible = candidates.Where(x =>
            {
                DateTime start;
                return TryIsoWeekStart(x.Year, x.Week, out start)
                    && System.Math.Abs((start - fact.PackCreatedTime.Value.Date).TotalDays) <= 90;
            }).ToList();
            if (plausible.Count == 1) ApplyCandidate(item, plausible[0]);
        }

        private static bool TryCandidate(WeekCodeAuditFact fact, out Candidate candidate)
        {
            candidate = null;
            string current = NormalizeRule(fact.WeekRuleValue);
            if (current != "YYWW" && current != "WWYY") return false;
            string dateCode = Trim(fact.DateCode);
            int first;
            int second;
            if (dateCode == null || dateCode.Length != 4 || !int.TryParse(dateCode.Substring(0, 2), NumberStyles.None, CultureInfo.InvariantCulture, out first) || !int.TryParse(dateCode.Substring(2, 2), NumberStyles.None, CultureInfo.InvariantCulture, out second)) return false;
            int year = 2000 + (current == "YYWW" ? second : first);
            int week = current == "YYWW" ? first : second;
            if (week < 1 || week > 53) return false;
            candidate = new Candidate { Rule = current == "YYWW" ? "WWYY" : "YYWW", Year = year, Week = week };
            return true;
        }

        private static bool TryIsoWeekStart(int year, int week, out DateTime value)
        {
            value = default(DateTime);
            if (year < 2000 || year > 2199 || week < 1 || week > 53) return false;
            try
            {
                DateTime januaryFourth = new DateTime(year, 1, 4);
                int offset = ((int)januaryFourth.DayOfWeek + 6) % 7;
                value = januaryFourth.AddDays(-offset + ((week - 1) * 7));
                return true;
            }
            catch (ArgumentOutOfRangeException) { return false; }
        }

        private static bool ValidWeek(int? year, int? week) { return year.HasValue && week.HasValue && year.Value >= 2000 && year.Value <= 2199 && week.Value >= 1 && week.Value <= 53; }
        private static void ApplyCandidate(WeekCodeAuditItemDto item, Candidate candidate) { item.CandidateRuleValue = candidate.Rule; item.CandidateWeekYear = candidate.Year; item.CandidateWeekNo = candidate.Week; item.CandidateWeekSortKey = candidate.Year * 100 + candidate.Week; }
        private static string NormalizeRule(string value) { return Trim(value) == null ? null : Trim(value).ToUpperInvariant().Replace("-", string.Empty).Replace("/", string.Empty).Replace(".", string.Empty); }
        private static string Trim(string value) { return string.IsNullOrWhiteSpace(value) ? null : value.Trim(); }
        private static string Display(string value) { return string.IsNullOrWhiteSpace(value) ? "（空）" : value.Trim(); }
        private static string FailureDetail(string value)
        {
            string detail = value ?? string.Empty;
            foreach (string code in new[] { "WEEK_RULE_UNSUPPORTED:", "UNSUPPORTED_WEEK_RULE:", "WEEK_RULE_MANUAL_REQUIRED:", "WEEK_RULE_INVALID_CONFIG:", "WEEK_RULE_NOT_FOUND:", "WEEK_RULE_MISSING:", "WEEKCODE_UNRESOLVED:" })
                while (detail.TrimStart().StartsWith(code, StringComparison.OrdinalIgnoreCase)) detail = detail.TrimStart().Substring(code.Length);
            return detail.Trim();
        }
        private static string Week(int year, int? week) { return week.HasValue ? year + "-W" + week.Value.ToString("D2") : year.ToString(); }
        private static int RiskOrder(string risk) { return risk == "HIGH" ? 3 : risk == "MEDIUM" ? 2 : 1; }

        private class Candidate { public string Rule { get; set; } public int Year { get; set; } public int Week { get; set; } }
    }
}
