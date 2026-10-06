using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using WMS_HU_Model.Infrastructure.Database;
using WMS_HU_Model.Models.Audit;

namespace WMS_HU_Model.Repositories
{
    public class AuditRepository : IAuditRepository
    {
        private const int CommandTimeoutSeconds = 15;

        public const string OperationFromWhereSql = @"
FROM dbo.WMS_FG_HU_OperationLog log
INNER JOIN dbo.WMS_FG_HU hu ON hu.Id = log.HUId
LEFT JOIN dbo.WMS_FG_HU related ON related.Id = log.RelatedHUId
WHERE log.OperationDt >= @FromTime AND log.OperationDt < @ToTimeExclusive
  AND (@HUCode IS NULL OR hu.HUCode = @HUCode)
  AND (@RequestId IS NULL OR log.RequestId = @RequestId)
  AND (@Operator IS NULL OR log.OperationBy LIKE '%' + @Operator + '%')
  AND (@OperationType IS NULL OR log.OperationType = @OperationType)";

        public const string OperationRowsSql = @"
SELECT
    log.Id, log.OperationNo, log.OperationType,
    hu.HUCode, hu.HUType, related.HUCode AS RelatedHuCode,
    log.BusinessNo, log.RequestId, log.DetailJson, log.OperationBy AS Operator,
    log.OperationDt AS OperationTime, log.Remark
" + OperationFromWhereSql + @"
ORDER BY log.OperationDt DESC, log.Id DESC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";

        public const string MoveFromWhereSql = @"
FROM dbo.WMS_FG_HU_MoveLog log
INNER JOIN dbo.WMS_FG_HU hu ON hu.Id = log.HUId
LEFT JOIN dbo.WMS_FG_Location fromLocation ON fromLocation.Id = log.FromLocationId
INNER JOIN dbo.WMS_FG_Location toLocation ON toLocation.Id = log.ToLocationId
WHERE log.MoveDt >= @FromTime AND log.MoveDt < @ToTimeExclusive
  AND (@HUCode IS NULL OR hu.HUCode = @HUCode)
  AND (@RequestId IS NULL OR log.RequestId = @RequestId)
  AND (@Operator IS NULL OR log.MoveBy LIKE '%' + @Operator + '%')
  AND (@MoveType IS NULL OR log.MoveType = @MoveType)";

        public const string MoveRowsSql = @"
SELECT
    log.Id, log.MoveNo, hu.HUCode, hu.HUType,
    fromLocation.LocationCode AS FromLocationCode,
    toLocation.LocationCode AS ToLocationCode,
    log.MoveType, toLocation.LocationCode AS ScannedLocationCode, log.BusinessNo, CAST(NULL AS VARCHAR(100)) AS TaskNo,
    log.RequestId, log.ReasonCode, log.MoveBy AS Operator, log.MoveDt AS MoveTime, log.Remark
" + MoveFromWhereSql + @"
ORDER BY log.MoveDt DESC, log.Id DESC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";

        public const string HoldFromWhereSql = @"
FROM dbo.WMS_FG_HU_HoldLog log
INNER JOIN dbo.WMS_FG_HU hu ON hu.Id = log.HUId
WHERE log.HoldDt >= @FromTime AND log.HoldDt < @ToTimeExclusive
  AND (@HUCode IS NULL OR hu.HUCode = @HUCode)
  AND (@RequestId IS NULL OR log.HoldRequestId = @RequestId OR log.ReleaseRequestId = @RequestId)
  AND (@Operator IS NULL OR log.HoldBy LIKE '%' + @Operator + '%' OR log.ReleaseBy LIKE '%' + @Operator + '%')
  AND (@HoldReason IS NULL OR log.HoldReasonCode LIKE '%' + @HoldReason + '%' OR log.HoldReason LIKE '%' + @HoldReason + '%')
  AND (@Status IS NULL OR (@Status = 'ACTIVE' AND log.IsActive = 1) OR (@Status = 'RELEASED' AND log.IsActive = 0))";

        public const string HoldRowsSql = @"
SELECT
    log.Id, log.HoldNo, hu.HUCode, hu.HUType,
    log.HoldType, log.HoldReasonCode, log.HoldReason,
    log.SourceBizType, log.SourceBizId, log.SourceBizNo,
    log.IsActive, log.HoldDt AS HoldTime, log.HoldBy AS HoldUser, log.HoldRequestId,
    log.ReleaseDt AS ReleaseTime, log.ReleaseBy AS ReleaseUser, log.ReleaseReason, log.ReleaseRequestId
" + HoldFromWhereSql + @"
ORDER BY log.HoldDt DESC, log.Id DESC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";

        public AuditPageResult<OperationAuditRowDto> QueryOperations(OperationAuditQuery query)
        {
            AuditPageResult<OperationAuditRowDto> result = Page<OperationAuditRowDto>(query);
            using (SqlConnection connection = DbConnectionFactory.Create())
            {
                connection.Open();
                using (SqlCommand count = Command(connection, "SELECT COUNT(1) " + OperationFromWhereSql + ";"))
                {
                    AddBase(count, query); AddText(count, "@OperationType", query.OperationType, 30);
                    result.TotalCount = Convert.ToInt32(count.ExecuteScalar());
                }
                using (SqlCommand rows = Command(connection, OperationRowsSql))
                {
                    AddBase(rows, query); AddText(rows, "@OperationType", query.OperationType, 30); AddPage(rows, query);
                    using (SqlDataReader reader = rows.ExecuteReader()) while (reader.Read()) result.Items.Add(ReadOperation(reader));
                }
            }
            return result;
        }

        public AuditPageResult<MoveAuditRowDto> QueryMoves(MoveAuditQuery query)
        {
            AuditPageResult<MoveAuditRowDto> result = Page<MoveAuditRowDto>(query);
            using (SqlConnection connection = DbConnectionFactory.Create())
            {
                connection.Open();
                using (SqlCommand count = Command(connection, "SELECT COUNT(1) " + MoveFromWhereSql + ";"))
                {
                    AddBase(count, query); AddText(count, "@MoveType", query.MoveType, 30);
                    result.TotalCount = Convert.ToInt32(count.ExecuteScalar());
                }
                using (SqlCommand rows = Command(connection, MoveRowsSql))
                {
                    AddBase(rows, query); AddText(rows, "@MoveType", query.MoveType, 30); AddPage(rows, query);
                    using (SqlDataReader reader = rows.ExecuteReader()) while (reader.Read()) result.Items.Add(ReadMove(reader));
                }
            }
            return result;
        }

        public AuditPageResult<HoldAuditRowDto> QueryHolds(HoldAuditQuery query)
        {
            AuditPageResult<HoldAuditRowDto> result = Page<HoldAuditRowDto>(query);
            using (SqlConnection connection = DbConnectionFactory.Create())
            {
                connection.Open();
                using (SqlCommand count = Command(connection, "SELECT COUNT(1) " + HoldFromWhereSql + ";"))
                {
                    AddBase(count, query); AddText(count, "@HoldReason", query.HoldReason, 100); AddText(count, "@Status", query.Status, 10);
                    result.TotalCount = Convert.ToInt32(count.ExecuteScalar());
                }
                using (SqlCommand rows = Command(connection, HoldRowsSql))
                {
                    AddBase(rows, query); AddText(rows, "@HoldReason", query.HoldReason, 100); AddText(rows, "@Status", query.Status, 10); AddPage(rows, query);
                    using (SqlDataReader reader = rows.ExecuteReader()) while (reader.Read()) result.Items.Add(ReadHold(reader));
                }
            }
            return result;
        }

        public List<OperationAuditRowDto> GetOperationsByRequestId(string requestId, int top)
        {
            const string sql = @"
SELECT TOP (@Top)
    log.Id, log.OperationNo, log.OperationType,
    hu.HUCode, hu.HUType, related.HUCode AS RelatedHuCode,
    log.BusinessNo, log.RequestId, log.DetailJson, log.OperationBy AS Operator,
    log.OperationDt AS OperationTime, log.Remark
FROM dbo.WMS_FG_HU_OperationLog log
INNER JOIN dbo.WMS_FG_HU hu ON hu.Id = log.HUId
LEFT JOIN dbo.WMS_FG_HU related ON related.Id = log.RelatedHUId
WHERE log.RequestId = @RequestId
ORDER BY log.OperationDt, log.Id;";
            return ReadByRequest(sql, requestId, top, ReadOperation);
        }

        public List<MoveAuditRowDto> GetMovesByRequestId(string requestId, int top)
        {
            const string sql = @"
SELECT TOP (@Top)
    log.Id, log.MoveNo, hu.HUCode, hu.HUType,
    fromLocation.LocationCode AS FromLocationCode,
    toLocation.LocationCode AS ToLocationCode,
    log.MoveType, toLocation.LocationCode AS ScannedLocationCode, log.BusinessNo, CAST(NULL AS VARCHAR(100)) AS TaskNo,
    log.RequestId, log.ReasonCode, log.MoveBy AS Operator, log.MoveDt AS MoveTime, log.Remark
FROM dbo.WMS_FG_HU_MoveLog log
INNER JOIN dbo.WMS_FG_HU hu ON hu.Id = log.HUId
LEFT JOIN dbo.WMS_FG_Location fromLocation ON fromLocation.Id = log.FromLocationId
INNER JOIN dbo.WMS_FG_Location toLocation ON toLocation.Id = log.ToLocationId
WHERE log.RequestId = @RequestId
ORDER BY log.MoveDt, log.Id;";
            return ReadByRequest(sql, requestId, top, ReadMove);
        }

        public List<HoldAuditRowDto> GetHoldsByRequestId(string requestId, int top)
        {
            const string sql = @"
SELECT TOP (@Top)
    log.Id, log.HoldNo, hu.HUCode, hu.HUType,
    log.HoldType, log.HoldReasonCode, log.HoldReason,
    log.SourceBizType, log.SourceBizId, log.SourceBizNo,
    log.IsActive, log.HoldDt AS HoldTime, log.HoldBy AS HoldUser, log.HoldRequestId,
    log.ReleaseDt AS ReleaseTime, log.ReleaseBy AS ReleaseUser, log.ReleaseReason, log.ReleaseRequestId
FROM dbo.WMS_FG_HU_HoldLog log
INNER JOIN dbo.WMS_FG_HU hu ON hu.Id = log.HUId
WHERE log.HoldRequestId = @RequestId OR log.ReleaseRequestId = @RequestId
ORDER BY log.HoldDt, log.Id;";
            return ReadByRequest(sql, requestId, top, ReadHold);
        }

        private static List<T> ReadByRequest<T>(string sql, string requestId, int top, Func<SqlDataReader, T> read)
        {
            List<T> result = new List<T>();
            using (SqlConnection connection = DbConnectionFactory.Create())
            using (SqlCommand command = Command(connection, sql))
            {
                command.Parameters.Add("@Top", SqlDbType.Int).Value = top;
                AddText(command, "@RequestId", requestId, 100);
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader()) while (reader.Read()) result.Add(read(reader));
            }
            return result;
        }

        private static AuditPageResult<T> Page<T>(AuditQueryBase query)
        {
            return new AuditPageResult<T> { Page = query.Page, PageSize = query.PageSize };
        }

        private static SqlCommand Command(SqlConnection connection, string sql)
        {
            return new SqlCommand(sql, connection) { CommandTimeout = CommandTimeoutSeconds };
        }

        private static void AddBase(SqlCommand command, AuditQueryBase query)
        {
            command.Parameters.Add("@FromTime", SqlDbType.DateTime2).Value = query.FromTime.Value;
            command.Parameters.Add("@ToTimeExclusive", SqlDbType.DateTime2).Value = query.ToTimeExclusive.Value;
            AddText(command, "@HUCode", query.HuCode, 100);
            AddText(command, "@RequestId", query.RequestId, 100);
            AddText(command, "@Operator", query.Operator, 50);
        }

        private static void AddPage(SqlCommand command, AuditQueryBase query)
        {
            command.Parameters.Add("@Offset", SqlDbType.Int).Value = (query.Page - 1) * query.PageSize;
            command.Parameters.Add("@PageSize", SqlDbType.Int).Value = query.PageSize;
        }

        private static void AddText(SqlCommand command, string name, string value, int size)
        {
            command.Parameters.Add(name, SqlDbType.VarChar, size).Value = string.IsNullOrEmpty(value) ? (object)DBNull.Value : value;
        }

        private static OperationAuditRowDto ReadOperation(SqlDataReader reader)
        {
            return new OperationAuditRowDto
            {
                Id = Long(reader, "Id"), OperationNo = Text(reader, "OperationNo"), OperationType = Text(reader, "OperationType"),
                HuCode = Text(reader, "HUCode"), HuType = Text(reader, "HUType"), RelatedHuCode = Text(reader, "RelatedHuCode"),
                BusinessNo = Text(reader, "BusinessNo"), RequestId = Text(reader, "RequestId"), DetailJson = Text(reader, "DetailJson"),
                Operator = Text(reader, "Operator"), OperationTime = Date(reader, "OperationTime"), Remark = Text(reader, "Remark")
            };
        }

        private static MoveAuditRowDto ReadMove(SqlDataReader reader)
        {
            return new MoveAuditRowDto
            {
                Id = Long(reader, "Id"), MoveNo = Text(reader, "MoveNo"), HuCode = Text(reader, "HUCode"), HuType = Text(reader, "HUType"),
                FromLocationCode = Text(reader, "FromLocationCode"), ToLocationCode = Text(reader, "ToLocationCode"), MoveType = Text(reader, "MoveType"),
                ScannedLocationCode = Text(reader, "ScannedLocationCode"), BusinessNo = Text(reader, "BusinessNo"), TaskNo = Text(reader, "TaskNo"),
                RequestId = Text(reader, "RequestId"), ReasonCode = Text(reader, "ReasonCode"), Operator = Text(reader, "Operator"),
                MoveTime = Date(reader, "MoveTime"), Remark = Text(reader, "Remark")
            };
        }

        private static HoldAuditRowDto ReadHold(SqlDataReader reader)
        {
            return new HoldAuditRowDto
            {
                Id = Long(reader, "Id"), HoldNo = Text(reader, "HoldNo"), HuCode = Text(reader, "HUCode"), HuType = Text(reader, "HUType"),
                HoldType = Text(reader, "HoldType"), HoldReasonCode = Text(reader, "HoldReasonCode"), HoldReason = Text(reader, "HoldReason"),
                SourceBizType = Text(reader, "SourceBizType"), SourceBizId = NullableLong(reader, "SourceBizId"), SourceBizNo = Text(reader, "SourceBizNo"),
                IsActive = Bool(reader, "IsActive"), HoldTime = Date(reader, "HoldTime"), HoldUser = Text(reader, "HoldUser"), HoldRequestId = Text(reader, "HoldRequestId"),
                ReleaseTime = NullableDate(reader, "ReleaseTime"), ReleaseUser = Text(reader, "ReleaseUser"), ReleaseReason = Text(reader, "ReleaseReason"), ReleaseRequestId = Text(reader, "ReleaseRequestId")
            };
        }

        private static string Text(SqlDataReader reader, string name) { int index = reader.GetOrdinal(name); return reader.IsDBNull(index) ? null : Convert.ToString(reader.GetValue(index)); }
        private static long Long(SqlDataReader reader, string name) { return Convert.ToInt64(reader[name]); }
        private static long? NullableLong(SqlDataReader reader, string name) { int index = reader.GetOrdinal(name); return reader.IsDBNull(index) ? (long?)null : Convert.ToInt64(reader.GetValue(index)); }
        private static bool Bool(SqlDataReader reader, string name) { return Convert.ToBoolean(reader[name]); }
        private static DateTime Date(SqlDataReader reader, string name) { return Convert.ToDateTime(reader[name]); }
        private static DateTime? NullableDate(SqlDataReader reader, string name) { int index = reader.GetOrdinal(name); return reader.IsDBNull(index) ? (DateTime?)null : Convert.ToDateTime(reader.GetValue(index)); }
    }
}
