using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using WMS_HU_Model.Infrastructure.Database;
using WMS_HU_Model.Models.HU;

namespace WMS_HU_Model.Repositories
{
    public class HuIdentifierRepository : IHuIdentifierRepository
    {
        public List<HuIdentityDto> ResolveCandidates(string rawValue, string customerCode)
        {
            const string sql = @"
SELECT
    h.Id AS HUId,
    h.HUCode,
    h.HUType,
    h.PlantCode,
    CAST('HU_CODE' AS VARCHAR(30)) AS IdentifierType,
    h.HUCode AS IdentifierValue,
    CAST(NULL AS VARCHAR(50)) AS CustomerCode,
    CAST(1 AS BIT) AS MatchedHUCode
FROM dbo.WMS_HU h
WHERE h.HUCode = @RawValue

UNION ALL

SELECT
    h.Id AS HUId,
    h.HUCode,
    h.HUType,
    h.PlantCode,
    i.IdentifierType,
    i.IdentifierValue,
    i.CustomerCode,
    CAST(0 AS BIT) AS MatchedHUCode
FROM dbo.WMS_HU_Identifier i
INNER JOIN dbo.WMS_HU h ON h.Id = i.HUId
WHERE i.IdentifierValue = @RawValue
  AND i.IsActive = 1
  AND
  (
      i.CustomerCode IS NULL
      OR (@CustomerCode IS NOT NULL AND i.CustomerCode = @CustomerCode)
  )
ORDER BY MatchedHUCode DESC, HUId;
";

            List<HuIdentityDto> result = new List<HuIdentityDto>();

            using (SqlConnection conn = DbConnectionFactory.Create())
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.Add("@RawValue", SqlDbType.VarChar, 200).Value = rawValue;
                cmd.Parameters.Add("@CustomerCode", SqlDbType.VarChar, 50).Value = DbParameterValue.StringOrDbNull(customerCode);

                conn.Open();

                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(new HuIdentityDto
                        {
                            HuId = Convert.ToInt64(reader["HUId"]),
                            HuCode = Convert.ToString(reader["HUCode"]),
                            HuType = Convert.ToString(reader["HUType"]),
                            PlantCode = Convert.ToString(reader["PlantCode"]),
                            IdentifierType = Convert.ToString(reader["IdentifierType"]),
                            IdentifierValue = Convert.ToString(reader["IdentifierValue"]),
                            CustomerCode = reader["CustomerCode"] == DBNull.Value ? null : Convert.ToString(reader["CustomerCode"]),
                            MatchedHuCode = Convert.ToBoolean(reader["MatchedHUCode"])
                        });
                    }
                }
            }

            return result;
        }
    }
}
