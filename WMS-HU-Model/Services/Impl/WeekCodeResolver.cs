using System;
using System.Globalization;
using System.Linq;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.WeekCode;

namespace WMS_HU_Model.Services
{
    public class WeekCodeResolver : IWeekCodeResolver
    {
        private readonly IWeekCodeRuleProvider _provider;

        public WeekCodeResolver(IWeekCodeRuleProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public OperationResult<WeekCodeSnapshot> Resolve(WeekCodeResolveContext context)
        {
            if (context == null) return OperationResult<WeekCodeSnapshot>.Fail("WEEK_CONTEXT_REQUIRED: 周码上下文不能为空。");
            // MES instruction rules are authoritative; manual configuration is auxiliary only.
            string rule = context.RuleValue;
            if (!string.IsNullOrWhiteSpace(context.RuleFailureCode))
                return OperationResult<WeekCodeSnapshot>.Fail(context.RuleFailureCode + ": " + context.RuleFailureMessage);
            OperationResult<WeekCodeSnapshot> result = Resolve(context.DateCode, rule, context.PackCreatedTime);
            if (!result.IsSuccess) return result;
            WeekCodeSnapshot snapshot = result.Data;
            if (snapshot == null || snapshot.WeekYear < 2000 || snapshot.WeekYear > 2199 || snapshot.WeekNo < 1 || snapshot.WeekNo > 53
                || snapshot.WeekSortKey != snapshot.WeekYear * 100 + snapshot.WeekNo || string.IsNullOrWhiteSpace(snapshot.WeekFormatCode)
                || string.IsNullOrWhiteSpace(snapshot.WeekRuleValue))
                return OperationResult<WeekCodeSnapshot>.Fail("WEEKCODE_INVALID_RESULT: 周码解析结果不完整或无效。");
            snapshot.WeekRuleSource = string.IsNullOrWhiteSpace(context.RuleSource) ? null : context.RuleSource.Trim();
            snapshot.WeekRuleValue = rule == null ? null : rule.Trim();
            snapshot.WeekResolvedTime = DateTime.Now;
            return result;
        }

        public OperationResult<WeekCodeSnapshot> Resolve(string materialCode, string materialRevision, string dateCode)
        {
            if (string.IsNullOrWhiteSpace(dateCode))
                return OperationResult<WeekCodeSnapshot>.Success(null);

            OperationResult<WeekCodeSnapshot> result = _provider.Resolve(materialCode, materialRevision, dateCode);

            if (!result.IsSuccess || result.Data == null)
                return OperationResult<WeekCodeSnapshot>.Fail(result.Message ?? "周码解析失败。");

            WeekCodeSnapshot snapshot = result.Data;

            if (snapshot.WeekYear < 2000 || snapshot.WeekYear > 2199)
                return OperationResult<WeekCodeSnapshot>.Fail("WeekYear必须在2000到2199之间。");

            if (snapshot.WeekNo < 1 || snapshot.WeekNo > 53)
                return OperationResult<WeekCodeSnapshot>.Fail("WeekNo必须在1到53之间。");

            if (snapshot.WeekSortKey != snapshot.WeekYear * 100 + snapshot.WeekNo)
                return OperationResult<WeekCodeSnapshot>.Fail("WeekSortKey与WeekYear/WeekNo不一致。");

            if (string.IsNullOrWhiteSpace(snapshot.WeekFormatCode) || string.IsNullOrWhiteSpace(snapshot.WeekRuleSource))
                return OperationResult<WeekCodeSnapshot>.Fail("周码规则快照不完整。");

            if (snapshot.WeekResolvedTime == default(DateTime))
                snapshot.WeekResolvedTime = DateTime.Now;

            return OperationResult<WeekCodeSnapshot>.Success(snapshot);
        }

        public OperationResult<WeekCodeSnapshot> Resolve(string dateCode, string ruleValue, DateTime? anchorTime)
        {
            if (string.IsNullOrWhiteSpace(ruleValue))
                return OperationResult<WeekCodeSnapshot>.Fail("WEEK_RULE_MISSING: MES周期格式为空，请到MES指示库维护。");
            if (string.IsNullOrWhiteSpace(dateCode))
                return OperationResult<WeekCodeSnapshot>.Fail("MISSING_DATECODE: DateCode不能为空。");
            string declaredRule = ruleValue.Trim();
            string format = declaredRule;
            if (format == "YY-WW") format = "YYWW";
            if (format == "WW-YY" || format == "WW/YY" || format == "WW.YY") format = "WWYY";
            string value = dateCode.Trim();
            if (declaredRule == "YYWW-DS-CP" || declaredRule == "YYWW-P-LC")
            {
                string[] parts = value.Split('-');
                if (parts.Length != 1 && (parts.Length != 3 || parts.Any(string.IsNullOrWhiteSpace)))
                    return OperationResult<WeekCodeSnapshot>.Fail("INVALID_DATECODE: DateCode不符合" + declaredRule + "格式。");
                value = parts[0];
                format = "YYWW";
            }
            if (declaredRule == "YY-WW" || declaredRule == "WW-YY" || declaredRule == "WW/YY" || declaredRule == "WW.YY")
            {
                char separator = declaredRule[2];
                if (value.Length != 5 || value[2] != separator || !char.IsDigit(value[0]) || !char.IsDigit(value[1]) || !char.IsDigit(value[3]) || !char.IsDigit(value[4]))
                    return OperationResult<WeekCodeSnapshot>.Fail("INVALID_DATECODE: DateCode不符合" + declaredRule + "格式。");
                value = value.Substring(0, 2) + value.Substring(3, 2);
            }
            int year;
            int week;
            if (format == "YYWW" || format == "WWYY")
            {
                if (value.Length != 4 || !value.All(char.IsDigit))
                    return OperationResult<WeekCodeSnapshot>.Fail("INVALID_DATECODE: " + format + "必须为四位数字。");
                year = 2000 + int.Parse(format == "YYWW" ? value.Substring(0, 2) : value.Substring(2, 2));
                week = int.Parse(format == "YYWW" ? value.Substring(2, 2) : value.Substring(0, 2));
            }
            else if (format == "YYMMDD")
            {
                DateTime date;
                if (value.Length != 6 || !value.All(c => c >= '0' && c <= '9')
                    || !DateTime.TryParseExact("20" + value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                    return OperationResult<WeekCodeSnapshot>.Fail("INVALID_DATECODE: YYMMDD必须为六位数字且为有效日期。");
                // ISO weeks belong to the year of their Thursday, including year boundaries.
                int weekday = ((int)date.DayOfWeek + 6) % 7;
                DateTime thursday = date.AddDays(3 - weekday);
                year = thursday.Year;
                week = (thursday.DayOfYear - 1) / 7 + 1;
                if (year < 2000 || year > 2199)
                    return OperationResult<WeekCodeSnapshot>.Fail("INVALID_DATECODE: 日期所属周的年份必须在2000到2199之间。");
            }
            else if (format == "YWW")
            {
                if (value.Length != 3 || !value.All(char.IsDigit))
                    return OperationResult<WeekCodeSnapshot>.Fail("INVALID_DATECODE: YWW必须为三位数字。");
                if (!anchorTime.HasValue)
                    return OperationResult<WeekCodeSnapshot>.Fail("WEEK_ANCHOR_REQUIRED: YWW需要PackCreatedTime。");
                int suffix = value[0] - '0';
                int anchorYear = anchorTime.Value.Year;
                int first = 2000 + suffix;
                int candidate = first + ((anchorYear - first) / 10) * 10;
                int before = candidate > anchorYear ? candidate - 10 : candidate;
                int after = before + 10;
                int beforeDistance = Math.Abs(anchorYear - before);
                int afterDistance = Math.Abs(after - anchorYear);
                if (beforeDistance == afterDistance)
                    return OperationResult<WeekCodeSnapshot>.Fail("INVALID_DATECODE: YWW年份与锚点等距，无法解析。");
                year = beforeDistance < afterDistance ? before : after;
                week = int.Parse(value.Substring(1, 2));
            }
            else
            {
                return OperationResult<WeekCodeSnapshot>.Fail("UNSUPPORTED_WEEK_RULE: MES周期格式“" + ruleValue.Trim() + "”暂不支持解析，请核对MES指示库配置。");
            }
            if (week < 1 || week > 53)
                return OperationResult<WeekCodeSnapshot>.Fail("INVALID_WEEK: 周数必须在1到53之间。");
            return OperationResult<WeekCodeSnapshot>.Success(new WeekCodeSnapshot
            {
                WeekFormatCode = format,
                WeekYear = (short)year,
                WeekNo = (byte)week,
                WeekSortKey = year * 100 + week,
                WeekRuleSource = "SNAPSHOT",
                WeekRuleValue = ruleValue.Trim(),
                WeekResolvedTime = DateTime.Now
            });
        }
    }
}
