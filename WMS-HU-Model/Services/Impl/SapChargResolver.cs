using System;

namespace WMS_HU_Model.Services
{
    public class SapChargResolver : ISapChargResolver
    {
        public string Resolve(string plantCode, string packId, bool isSpecialOrderType)
        {
            if (string.IsNullOrWhiteSpace(plantCode))
                throw new ArgumentException("PlantCode不能为空。", nameof(plantCode));
            if (string.IsNullOrWhiteSpace(packId))
                throw new ArgumentException("PackID不能为空。", nameof(packId));

            string normalizedPackId = packId.Trim();
            for (int index = 0; index < normalizedPackId.Length; index++)
            {
                if (normalizedPackId[index] < '0' || normalizedPackId[index] > '9')
                    throw new ArgumentException("PackID必须为数字。", nameof(packId));
            }

            bool plant1010 = string.Equals(plantCode.Trim(), "1010", StringComparison.Ordinal);
            string prefix = isSpecialOrderType ? (plant1010 ? "AY" : "BY") : (plant1010 ? "A" : "B");
            int width = isSpecialOrderType ? 8 : 9;
            string padded = "00000000" + normalizedPackId;
            return prefix + padded.Substring(padded.Length - Math.Min(width, padded.Length));
        }
    }
}
