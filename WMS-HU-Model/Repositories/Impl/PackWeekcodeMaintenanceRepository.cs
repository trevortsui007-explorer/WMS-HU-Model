using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.WeekCode;

namespace WMS_HU_Model.Repositories
{
    public class PackWeekcodeMaintenanceRepository : IPackWeekcodeMaintenanceRepository
    {
        private static SqlConnection Open()
        {
            var setting = ConfigurationManager.ConnectionStrings["BaseDb"];
            if (setting == null) throw new InvalidOperationException("BaseDb未配置。");
            var connection = new SqlConnection(setting.ConnectionString);
            try { connection.Open(); return connection; }
            catch { connection.Dispose(); throw; }
        }

        private static void Scope(SqlCommand command, string plant, string prodNo, string prodRev)
        {
            command.Parameters.Add("@PlantCode", SqlDbType.VarChar, 20).Value = plant;
            command.Parameters.Add("@ProdNo", SqlDbType.VarChar, 50).Value = prodNo;
            command.Parameters.Add("@ProdRev", SqlDbType.VarChar, 30).Value = prodRev ?? "";
        }

        public PackWeekcodeConfig Get(string plantCode, string prodNo, string prodRev)
        {
            using (var connection = Open()) return Get(connection, null, plantCode, prodNo, prodRev);
        }

        private static PackWeekcodeConfig Get(SqlConnection connection, SqlTransaction transaction, string plant, string prodNo, string prodRev)
        {
            using (var command = new SqlCommand(@"SELECT PlantCode,ProdNo,ProdRev,WeekCode,MESWeekCode,ExampleValue,Remark,LastFailure,IsActive,RowVersion
FROM dbo.PK_PackWeekcode WHERE PlantCode=@PlantCode AND ProdNo=@ProdNo AND ProdRev=@ProdRev;", connection, transaction))
            {
                Scope(command, plant, prodNo, prodRev);
                using (var reader = command.ExecuteReader())
                {
                    if (!reader.Read()) return null;
                    return new PackWeekcodeConfig
                    {
                        PlantCode = reader.GetString(0), ProdNo = reader.GetString(1), ProdRev = reader.GetString(2), WeekCode = reader.GetString(3),
                        MESWeekCode = Convert.ToString(reader[4]), ExampleValue = Convert.ToString(reader[5]), Remark = Convert.ToString(reader[6]),
                        LastFailure = Convert.ToString(reader[7]), IsActive = reader.GetBoolean(8), RowVersion = Convert.ToBase64String((byte[])reader[9])
                    };
                }
            }
        }

        public List<PackWeekcodeFormat> GetSqlFormats()
        {
            var result = new List<PackWeekcodeFormat>();
            using (var connection = Open())
            using (var command = new SqlCommand("dbo.pr_PackWeekcode_Formats", connection))
            {
                command.CommandType = CommandType.StoredProcedure;
                using (var reader = command.ExecuteReader())
                    while (reader.Read()) result.Add(new PackWeekcodeFormat { Code = reader.GetString(0), Example = reader.GetString(1) });
            }
            return result;
        }

        public string ReadMESWeekCode(string plantCode, string prodNo, string prodRev)
        {
            using (var connection = Open())
                return ReadMESWeekCode(connection, null, plantCode, prodNo, prodRev);
        }

        private static string ReadMESWeekCode(SqlConnection connection, SqlTransaction transaction, string plantCode, string prodNo, string prodRev)
        {
            using (var command = new SqlCommand(@"SELECT DISTINCT LTRIM(RTRIM(v.attvalue)) AS RuleValue
FROM dbo.MD_Flow f
JOIN dbo.MD_FlowStation fs ON fs.flowid=f.md_flow_id
JOIN dbo.MD_StationAttValue v ON v.flowid=f.md_flow_id AND v.flowitemno=fs.flowitemno
WHERE f.plantcode=@PlantCode AND f.prodno=@ProdNo AND f.prodrev=@ProdRev
AND f.status='on' AND ISNULL(f.isdeleted,0)=0 AND ISNULL(v.isdeleted,0)=0
AND (v.itemname=N'周期格式' OR v.enname='DATE_CODE_FORMAT_')
AND NULLIF(LTRIM(RTRIM(v.attvalue)),'') IS NOT NULL ORDER BY RuleValue;", connection, transaction))
            {
                Scope(command, plantCode, prodNo, prodRev);
                var values = new List<string>();
                using (var reader = command.ExecuteReader()) while (reader.Read()) values.Add(reader.GetString(0));
                return Limit(string.Join(" | ", values), 500);
            }
        }

        public OperationResult<PackWeekcodeConfig> Save(PackWeekcodeConfig config, string actor)
        {
            using (var connection = Open())
            using (var transaction = connection.BeginTransaction(IsolationLevel.Serializable))
            {
                using (var command = new SqlCommand(@"DECLARE @Existing VARBINARY(8);
SELECT @Existing=RowVersion FROM dbo.PK_PackWeekcode WITH(UPDLOCK,HOLDLOCK)
WHERE PlantCode=@PlantCode AND ProdNo=@ProdNo AND ProdRev=@ProdRev;
IF (@Existing IS NULL AND @Expected IS NOT NULL) OR (@Existing IS NOT NULL AND (@Expected IS NULL OR @Existing<>@Expected))
    SELECT CAST(0 AS INT);
ELSE BEGIN
    IF @Existing IS NULL
        INSERT dbo.PK_PackWeekcode(PlantCode,ProdNo,ProdRev,WeekCode,MESWeekCode,ExampleValue,Remark,IsActive,CreateBy,CreateDt)
        VALUES(@PlantCode,@ProdNo,@ProdRev,@Rule,@MES,@Example,@Remark,@Active,@Actor,GETDATE());
    ELSE
        UPDATE dbo.PK_PackWeekcode SET WeekCode=@Rule,MESWeekCode=@MES,ExampleValue=@Example,Remark=@Remark,
            IsActive=@Active,LastEditBy=@Actor,LastEditDt=GETDATE()
        WHERE PlantCode=@PlantCode AND ProdNo=@ProdNo AND ProdRev=@ProdRev;
    SELECT CAST(1 AS INT);
END;", connection, transaction))
                {
                    Scope(command, config.PlantCode, config.ProdNo, config.ProdRev);
                    command.Parameters.Add("@Expected", SqlDbType.Binary, 8).Value = string.IsNullOrEmpty(config.RowVersion) ? (object)DBNull.Value : Convert.FromBase64String(config.RowVersion);
                    command.Parameters.Add("@Rule", SqlDbType.VarChar, 100).Value = config.WeekCode;
                    command.Parameters.Add("@MES", SqlDbType.NVarChar, 500).Value = config.MESWeekCode ?? "";
                    command.Parameters.Add("@Example", SqlDbType.NVarChar, 50).Value = config.ExampleValue;
                    command.Parameters.Add("@Remark", SqlDbType.NVarChar, 500).Value = (object)config.Remark ?? DBNull.Value;
                    command.Parameters.Add("@Active", SqlDbType.Bit).Value = config.IsActive;
                    command.Parameters.Add("@Actor", SqlDbType.NVarChar, 50).Value = actor;
                    if ((int)command.ExecuteScalar() == 0)
                    {
                        transaction.Rollback();
                        return OperationResult<PackWeekcodeConfig>.Fail("配置已被其他操作更新，请重新打开后再保存。");
                    }
                }
                var saved = Get(connection, transaction, config.PlantCode, config.ProdNo, config.ProdRev);
                transaction.Commit();
                return OperationResult<PackWeekcodeConfig>.Success(saved);
            }
        }

        // Called only within the physical receipt transaction. Never overwrite a maintained rule.
        public static void RegisterFailure(SqlConnection connection, SqlTransaction transaction, string plant, string prodNo, string prodRev,
            string mesWeekCode, string example, string reason, string actor)
        {
            // Direct snapshot inbound has no server-enriched MES evidence; read it here.
            if (mesWeekCode == null) mesWeekCode = ReadMESWeekCode(connection, transaction, plant, prodNo, prodRev);
            using (var command = new SqlCommand(@"IF NOT EXISTS(SELECT 1 FROM dbo.PK_PackWeekcode WITH(UPDLOCK,HOLDLOCK)
WHERE PlantCode=@PlantCode AND ProdNo=@ProdNo AND ProdRev=@ProdRev)
    INSERT dbo.PK_PackWeekcode(PlantCode,ProdNo,ProdRev,WeekCode,IsActive,MESWeekCode,ExampleValue,LastFailure,CreateBy,CreateDt)
    VALUES(@PlantCode,@ProdNo,@ProdRev,'',0,@MES,@Example,@Failure,@Actor,GETDATE());
ELSE
    UPDATE dbo.PK_PackWeekcode SET MESWeekCode=@MES,LastFailure=@Failure,
        ExampleValue=CASE WHEN NULLIF(ExampleValue,'') IS NULL THEN @Example ELSE ExampleValue END
    WHERE PlantCode=@PlantCode AND ProdNo=@ProdNo AND ProdRev=@ProdRev;", connection, transaction))
            {
                Scope(command, plant, prodNo, prodRev);
                command.Parameters.Add("@MES", SqlDbType.NVarChar, 500).Value = Limit(mesWeekCode, 500);
                command.Parameters.Add("@Example", SqlDbType.NVarChar, 50).Value = Limit(example, 50);
                command.Parameters.Add("@Failure", SqlDbType.NVarChar, 500).Value = Limit(reason, 500);
                command.Parameters.Add("@Actor", SqlDbType.NVarChar, 50).Value = actor;
                command.ExecuteNonQuery();
            }
        }

        private static string Limit(string value, int length) { return string.IsNullOrEmpty(value) ? "" : value.Length <= length ? value : value.Substring(0, length); }
    }
}
