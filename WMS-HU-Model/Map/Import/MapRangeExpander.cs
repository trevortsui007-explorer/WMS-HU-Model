using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace WMS_HU_Model.Map.Import
{
    public static class MapRangeExpander
    {
        private static readonly Regex CodePattern = new Regex(@"^(?<prefix>[A-Z]*)(?<number>\d+)(?<suffix>[A-Z]*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex RangePattern = new Regex(@"(?<![A-Z0-9])(?<start>(?:[A-Z]+\d+[A-Z]*|\d+[A-Z]+))\s*-\s*(?<end>(?:[A-Z]+\d+[A-Z]*|\d+[A-Z]+))(?![A-Z0-9])", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex TokenPattern = new Regex(@"(?<![A-Z0-9])(?:[A-Z]+\d+[A-Z]*|\d+[A-Z]+)(?![A-Z0-9])", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static string ExtractExpression(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            string normalized = Normalize(value);
            Match range = RangePattern.Match(normalized);
            if (range.Success) return range.Groups["start"].Value + "-" + range.Groups["end"].Value;
            MatchCollection tokens = TokenPattern.Matches(normalized);
            if (tokens.Count == 0) return null;
            List<string> values = new List<string>();
            foreach (Match token in tokens) values.Add(token.Value);
            return string.Join("/", values);
        }

        public static List<string> Expand(string value)
        {
            List<string> result = new List<string>();
            if (string.IsNullOrWhiteSpace(value)) return result;
            string normalized = Normalize(value);
            string[] slashParts = normalized.Split(new[] { '/', ',', '，', '、' }, StringSplitOptions.RemoveEmptyEntries);
            if (slashParts.Length > 1)
            {
                foreach (string part in slashParts) result.AddRange(Expand(part));
                return Distinct(result);
            }

            int separator = normalized.IndexOf('-');
            if (separator < 0)
            {
                if (CodePattern.IsMatch(normalized)) result.Add(normalized);
                return result;
            }

            Match start = CodePattern.Match(normalized.Substring(0, separator).Trim());
            Match end = CodePattern.Match(normalized.Substring(separator + 1).Trim());
            if (!start.Success || !end.Success) return result;
            if (!string.Equals(start.Groups["prefix"].Value, end.Groups["prefix"].Value, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(start.Groups["suffix"].Value, end.Groups["suffix"].Value, StringComparison.OrdinalIgnoreCase)) return result;

            int first = Convert.ToInt32(start.Groups["number"].Value);
            int last = Convert.ToInt32(end.Groups["number"].Value);
            if (last < first || last - first > 500) return result;
            int width = Math.Max(start.Groups["number"].Value.Length, end.Groups["number"].Value.Length);
            for (int number = first; number <= last; number++)
                result.Add(start.Groups["prefix"].Value + number.ToString(new string('0', width)) + start.Groups["suffix"].Value);
            return result;
        }

        public static List<string> ExtractAndExpand(string value)
        {
            string expression = ExtractExpression(value);
            return expression == null ? new List<string>() : Expand(expression);
        }

        private static string Normalize(string value)
        {
            return value.Trim().ToUpperInvariant().Replace("／", "/").Replace("—", "-").Replace("–", "-");
        }

        private static List<string> Distinct(IEnumerable<string> values)
        {
            List<string> result = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string value in values) if (seen.Add(value)) result.Add(value);
            return result;
        }
    }
}
