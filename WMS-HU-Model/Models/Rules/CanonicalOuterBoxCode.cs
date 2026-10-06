using System;
using System.Globalization;

namespace WMS_HU_Model.Rules
{
    public static class CanonicalOuterBoxCode
    {
        public static string BuildSequenceKey(DateTime businessDate, string plantCode)
        {
            GetPlantSuffix(plantCode);
            string normalizedPlantCode = plantCode.Trim().ToUpperInvariant();
            return "OUTER_BOX|" + normalizedPlantCode + "|" + businessDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        }

        public static string Build(DateTime businessDate, string plantCode, int sequenceNo)
        {
            if (sequenceNo < 1 || sequenceNo > 9999)
                throw new ArgumentOutOfRangeException("sequenceNo", "OUTER_BOX日序号必须在0001到9999之间。");

            string suffix = GetPlantSuffix(plantCode);
            return "B" + businessDate.ToString("yyMMdd", CultureInfo.InvariantCulture)
                + sequenceNo.ToString("0000", CultureInfo.InvariantCulture) + suffix;
        }

        public static string GetPlantSuffix(string plantCode)
        {
            string normalized = string.IsNullOrWhiteSpace(plantCode) ? null : plantCode.Trim().ToUpperInvariant();
            if (normalized == "1010") return "A";
            if (normalized == "1020") return "B";
            throw new ArgumentException("当前厂别不支持生成OUTER_BOX标准箱码。", "plantCode");
        }
    }
}
