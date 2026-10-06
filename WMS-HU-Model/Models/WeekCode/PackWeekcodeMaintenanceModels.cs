using System.Collections.Generic;

namespace WMS_HU_Model.Models.WeekCode
{
    public class PackWeekcodeConfig
    {
        public string PlantCode { get; set; }
        public string ProdNo { get; set; }
        public string ProdRev { get; set; }
        public string WeekCode { get; set; }
        public string MESWeekCode { get; set; }
        public string ExampleValue { get; set; }
        public string Remark { get; set; }
        public string LastFailure { get; set; }
        public bool IsActive { get; set; }
        public string RowVersion { get; set; }
    }

    public class PackWeekcodeForm
    {
        public PackWeekcodeConfig Config { get; set; }
        public List<PackWeekcodeFormat> Formats { get; set; }
    }

    public class PackWeekcodeFormat
    {
        public string Code { get; set; }
        public string Example { get; set; }
    }

    public static class WeekCodeFormats
    {
        public static List<PackWeekcodeFormat> Common()
        {
            return new List<PackWeekcodeFormat>
            {
                new PackWeekcodeFormat { Code = "YYWW", Example = "2638" },
                new PackWeekcodeFormat { Code = "WWYY", Example = "3826" },
                new PackWeekcodeFormat { Code = "YY-WW", Example = "26-38" },
                new PackWeekcodeFormat { Code = "WW-YY", Example = "38-26" },
                new PackWeekcodeFormat { Code = "WW/YY", Example = "38/26" },
                new PackWeekcodeFormat { Code = "WW.YY", Example = "38.26" },
                new PackWeekcodeFormat { Code = "YWW", Example = "638（需要包装时间）" },
                new PackWeekcodeFormat { Code = "YYMMDD", Example = "260920" },
                new PackWeekcodeFormat { Code = "YYWW-DS-CP", Example = "2638-A-01" },
                new PackWeekcodeFormat { Code = "YYWW-P-LC", Example = "2638-P-LC" }
            };
        }
    }
}
