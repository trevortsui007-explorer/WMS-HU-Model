using System;
using System.Configuration;
using System.Data.SqlClient;
using Dapper;
using WMS_HU_Model.Models.WeekCode;

namespace WMS_HU_Model.Repositories
{
    public class MesWeekCodeRuleRepository : IMesWeekCodeRuleRepository
    {
        public MesWeekCodeRuleReadResult Read(string plantCode, string prodNo, string prodRev)
        {
            ConnectionStringSettings settings = ConfigurationManager.ConnectionStrings["BaseDb"];
            if (settings == null || string.IsNullOrWhiteSpace(settings.ConnectionString)) throw new InvalidOperationException("连接配置BaseDb未找到。");
            const string sql = @"SELECT LTRIM(RTRIM(v.attvalue))
FROM dbo.MD_Flow f WITH (NOLOCK)
JOIN dbo.MD_FlowStation fs WITH (NOLOCK) ON fs.flowid=f.md_flow_id
JOIN dbo.MD_StationAttValue v WITH (NOLOCK) ON v.flowid=f.md_flow_id AND v.flowitemno=fs.flowitemno
WHERE f.plantcode=@PlantCode AND f.prodno=@ProdNo AND f.prodrev=@ProdRev
  AND f.status='on' AND ISNULL(f.isdeleted,0)=0 AND ISNULL(v.isdeleted,0)=0
  AND (v.itemname=N'周期格式' OR v.enname='DATE_CODE_FORMAT_')
  AND NULLIF(LTRIM(RTRIM(v.attvalue)),'') IS NOT NULL;";
            using (SqlConnection conn = new SqlConnection(settings.ConnectionString))
                return new MesWeekCodeRuleReadResult { RuleValues = conn.Query<string>(sql, new { PlantCode = plantCode.Trim(), ProdNo = prodNo.Trim(), ProdRev = prodRev.Trim() }).AsList() };
        }
    }
}
