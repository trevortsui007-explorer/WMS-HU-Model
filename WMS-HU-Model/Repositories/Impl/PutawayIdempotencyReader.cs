using System;
using System.Data;
using System.Data.SqlClient;
using WMS_HU_Model.Infrastructure.Database;
using WMS_HU_Model.Models.Putaway;

namespace WMS_HU_Model.Repositories
{
    internal static class PutawayIdempotencyReader
    {
        public static PutawayResultDto Read(string requestId)
        {
            if (string.IsNullOrWhiteSpace(requestId)) return null;
            using (SqlConnection connection = DbConnectionFactory.Create())
            {
                connection.Open();
                return Read(connection, null, requestId, false);
            }
        }

        public static bool RequestIdExists(SqlConnection connection, SqlTransaction transaction, string requestId)
        {
            const string sql = @"SELECT COUNT(1) FROM dbo.WMS_FG_HU_OperationLog WITH (UPDLOCK,HOLDLOCK) WHERE RequestId=@RequestId;";
            using (SqlCommand command = new SqlCommand(sql, connection, transaction))
            {
                command.Parameters.Add("@RequestId", SqlDbType.VarChar, 100).Value = requestId;
                return Convert.ToInt32(command.ExecuteScalar()) > 0;
            }
        }

        public static PutawayResultDto Read(SqlConnection connection, SqlTransaction transaction, string requestId, bool lockRows)
        {
            string operationTable = lockRows
                ? "dbo.WMS_FG_HU_OperationLog op WITH (UPDLOCK,HOLDLOCK)"
                : "dbo.WMS_FG_HU_OperationLog op";
            string sql = @"
SELECT TOP 1
    op.OperationNo,
    moveLog.Id AS MoveLogId,
    CASE WHEN pallet.Id IS NULL THEN 'DIRECT_RECEIVE' ELSE 'PALLET_INHERITED' END AS SourceMode,
    pallet.HUCode AS PalletHUCode,
    pallet.LifecycleStatus AS PalletLifecycleStatus,
    box.HUCode AS BoxHUCode,
    fromLoc.LocationCode AS FromLocationCode,
    toLoc.LocationCode AS ToLocationCode,
    moveLog.MoveDt AS PutawayTime,
    CASE WHEN pallet.Id IS NULL THEN 0 ELSE
    (
        SELECT COUNT(1)
        FROM dbo.WMS_FG_HU_Relation remainRel
        INNER JOIN dbo.WMS_FG_HU remainHU ON remainHU.Id=remainRel.ChildHUId
        WHERE remainRel.ParentHUId=pallet.Id AND remainRel.IsActive=1
          AND remainHU.HUType='OUTER_BOX' AND remainHU.LifecycleStatus='ACTIVE'
    ) END AS RemainingBoxCount
FROM " + operationTable + @"
INNER JOIN dbo.WMS_FG_HU box ON box.Id=op.HUId
LEFT JOIN dbo.WMS_FG_HU pallet ON pallet.Id=op.RelatedHUId AND pallet.HUType='PALLET'
INNER JOIN dbo.WMS_FG_HU_MoveLog moveLog ON moveLog.MoveNo=op.OperationNo AND moveLog.HUId=box.Id
LEFT JOIN dbo.WMS_FG_Location fromLoc ON fromLoc.Id=moveLog.FromLocationId
INNER JOIN dbo.WMS_FG_Location toLoc ON toLoc.Id=moveLog.ToLocationId
WHERE op.OperationType='PUTAWAY' AND op.RequestId=@RequestId
ORDER BY op.Id;";
            using (SqlCommand command = new SqlCommand(sql, connection, transaction))
            {
                command.Parameters.Add("@RequestId", SqlDbType.VarChar, 100).Value = requestId;
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read()) return null;
                    string sourceMode = Convert.ToString(reader["SourceMode"]);
                    int remainingBoxCount = Convert.ToInt32(reader["RemainingBoxCount"]);
                    string palletLifecycleStatus = NullableString(reader, "PalletLifecycleStatus");
                    return new PutawayResultDto
                    {
                        OperationNo = Convert.ToString(reader["OperationNo"]),
                        MoveLogId = Convert.ToInt64(reader["MoveLogId"]),
                        SourceMode = sourceMode,
                        PalletHuCode = NullableString(reader, "PalletHUCode"),
                        BoxHuCode = Convert.ToString(reader["BoxHUCode"]),
                        FromLocationCode = NullableString(reader, "FromLocationCode"),
                        ToLocationCode = Convert.ToString(reader["ToLocationCode"]),
                        RemainingBoxCount = remainingBoxCount,
                        IsPalletEmpty = sourceMode == "PALLET_INHERITED" && remainingBoxCount == 0,
                        IsPalletClosed = sourceMode == "PALLET_INHERITED" && string.Equals(palletLifecycleStatus, "CLOSED", StringComparison.OrdinalIgnoreCase),
                        IsDuplicate = true,
                        PutawayTime = Convert.ToDateTime(reader["PutawayTime"])
                    };
                }
            }
        }

        private static string NullableString(SqlDataReader reader, string name)
        {
            int ordinal = reader.GetOrdinal(name);
            return reader.IsDBNull(ordinal) ? null : Convert.ToString(reader.GetValue(ordinal));
        }
    }
}
