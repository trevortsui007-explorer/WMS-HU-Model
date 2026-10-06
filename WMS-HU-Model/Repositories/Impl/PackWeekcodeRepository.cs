using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.WeekCode;

namespace WMS_HU_Model.Repositories
{
    public class PackWeekcodeRepository : IPackWeekcodeRepository
    {
        public OperationResult<PackWeekcodeConfig> GetConfig(WeekCodeResolveContext context)
        {
            try
            {
                return OperationResult<PackWeekcodeConfig>.Success(new PackWeekcodeMaintenanceRepository().Get(
                    (context.PlantCode ?? "").Trim(), (context.MaterialCode ?? "").Trim(), (context.MaterialRevision ?? "").Trim()));
            }
            catch (SqlException) { return OperationResult<PackWeekcodeConfig>.Fail("PACK_WEEKCODE_DB_ERROR: 人工周码配置读取失败，请检查数据库升级。"); }
            catch (InvalidOperationException) { return OperationResult<PackWeekcodeConfig>.Fail("PACK_WEEKCODE_DB_ERROR: 人工周码配置连接不可用。"); }
        }

        public OperationResult<WeekCodeSnapshot> Resolve(WeekCodeResolveContext context)
        {
            if (context.PlantCode.Trim().Length > 20 || context.MaterialCode.Trim().Length > 50 || context.MaterialRevision.Trim().Length > 30
                || (context.DateCode != null && context.DateCode.Length > 50))
                return OperationResult<WeekCodeSnapshot>.Fail("PACK_WEEKCODE_INVALID_DATECODE: 输入字段超出允许长度。");
            ConnectionStringSettings settings = ConfigurationManager.ConnectionStrings["BaseDb"];
            if (settings == null || string.IsNullOrWhiteSpace(settings.ConnectionString))
                return OperationResult<WeekCodeSnapshot>.Fail("PACK_WEEKCODE_DB_ERROR: 未配置BaseDb连接。");
            try
            {
                using (SqlConnection connection = new SqlConnection(settings.ConnectionString))
                using (SqlCommand command = new SqlCommand("dbo.pr_PackWeekcode_Resolve", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.Add("@PlantCode", SqlDbType.VarChar, 20).Value = context.PlantCode.Trim();
                    command.Parameters.Add("@ProdNo", SqlDbType.VarChar, 50).Value = context.MaterialCode.Trim();
                    command.Parameters.Add("@ProdRev", SqlDbType.VarChar, 30).Value = context.MaterialRevision.Trim();
                    command.Parameters.Add("@DateCode", SqlDbType.NVarChar, 50).Value = (object)context.DateCode ?? DBNull.Value;
                    command.Parameters.Add("@PackCreatedTime", SqlDbType.DateTime).Value = (object)context.PackCreatedTime ?? DBNull.Value;
                    connection.Open();
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (!reader.Read()) return InvalidResult();
                        OperationResult<WeekCodeSnapshot> result;
                        if (!reader.GetBoolean(reader.GetOrdinal("IsSuccess")))
                        {
                            string code = Convert.ToString(reader["ErrorCode"]);
                            result = OperationResult<WeekCodeSnapshot>.Fail((string.IsNullOrWhiteSpace(code) ? "PACK_WEEKCODE_INVALID_RESULT" : code) + ": " + Convert.ToString(reader["ErrorMessage"]));
                        }
                        else
                        {
                            if (reader["WeekYear"] == DBNull.Value || reader["WeekNo"] == DBNull.Value || reader["WeekSortKey"] == DBNull.Value) return InvalidResult();
                            result = OperationResult<WeekCodeSnapshot>.Success(new WeekCodeSnapshot
                            {
                                WeekYear = Convert.ToInt16(reader["WeekYear"]),
                                WeekNo = Convert.ToByte(reader["WeekNo"]),
                                WeekSortKey = Convert.ToInt32(reader["WeekSortKey"]),
                                WeekFormatCode = Convert.ToString(reader["WeekFormatCode"]),
                                WeekRuleValue = Convert.ToString(reader["WeekFormatCode"]),
                                WeekRuleSource = "PACK_WEEKCODE",
                                WeekResolvedTime = DateTime.Now
                            });
                        }
                        return reader.Read() || reader.NextResult() ? InvalidResult() : result;
                    }
                }
            }
            catch (SqlException)
            {
                return DatabaseError();
            }
            catch (ArgumentException) { return DatabaseError(); }
            catch (InvalidOperationException) { return DatabaseError(); }
            catch (System.Data.SqlTypes.SqlNullValueException) { return InvalidResult(); }
            catch (IndexOutOfRangeException) { return InvalidResult(); }
            catch (InvalidCastException) { return InvalidResult(); }
            catch (OverflowException) { return InvalidResult(); }
            catch (FormatException) { return InvalidResult(); }
        }

        private static OperationResult<WeekCodeSnapshot> InvalidResult()
        {
            return OperationResult<WeekCodeSnapshot>.Fail("PACK_WEEKCODE_INVALID_RESULT: 存储过程返回不符合周码契约。");
        }

        private static OperationResult<WeekCodeSnapshot> DatabaseError()
        {
            return OperationResult<WeekCodeSnapshot>.Fail("PACK_WEEKCODE_DB_ERROR: 特殊周码解析数据库调用失败，请检查连接和存储过程部署。");
        }
    }
}
