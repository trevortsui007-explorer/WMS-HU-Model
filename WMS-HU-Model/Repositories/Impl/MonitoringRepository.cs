using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using WMS_HU_Model.Infrastructure.Database;
using WMS_HU_Model.Models.Monitoring;

namespace WMS_HU_Model.Repositories
{
    public class MonitoringRepository : IMonitoringRepository
    {
        private const int HealthTimeoutSeconds = 3;

        public const string SummarySql = @"
;WITH TodayReceipts AS
(
    SELECT Id
    FROM dbo.WMS_FG_FinishedGoodsInboundReceipt
    WHERE ReceiveDt >= @FromTime AND ReceiveDt < @ToTimeExclusive
)
SELECT
    TodayReceiptCount = (SELECT COUNT(1) FROM TodayReceipts),
    TodayBoxCount = (SELECT COUNT(1) FROM TodayReceipts receipt INNER JOIN dbo.WMS_FG_HU hu ON hu.SourceBusinessNo = 'FGI-' + CAST(receipt.Id AS VARCHAR(20)) AND hu.HUType = 'OUTER_BOX'),
    TodayPackCount = (SELECT COUNT(1) FROM TodayReceipts receipt INNER JOIN dbo.WMS_FG_HU hu ON hu.SourceBusinessNo = 'FGI-' + CAST(receipt.Id AS VARCHAR(20)) AND hu.HUType = 'INNER_PACK'),
    TodayQty = (SELECT ISNULL(SUM(item.Qty), 0) FROM TodayReceipts receipt INNER JOIN dbo.WMS_FG_HU pack ON pack.SourceBusinessNo = 'FGI-' + CAST(receipt.Id AS VARCHAR(20)) AND pack.HUType = 'INNER_PACK' INNER JOIN dbo.WMS_FG_HU_Item item ON item.HUId = pack.Id),
    ActiveHoldBoxCount = (SELECT COUNT(1) FROM dbo.WMS_FG_HU box WHERE box.HUType = 'OUTER_BOX' AND box.LifecycleStatus = 'ACTIVE' AND box.IsHold = 1),
    InboundExceptionReceiptCount =
    (
        SELECT COUNT(1)
        FROM dbo.WMS_FG_FinishedGoodsInboundReceipt receipt
        WHERE EXISTS
        (
            SELECT 1
            FROM dbo.WMS_FG_HU box
            WHERE box.SourceBusinessNo = 'FGI-' + CAST(receipt.Id AS VARCHAR(20))
              AND box.HUType = 'OUTER_BOX'
              AND (box.IsHold = 1 OR EXISTS
                  (SELECT 1 FROM dbo.WMS_FG_HU_HoldLog hold WHERE hold.HUId = box.Id AND hold.HoldReasonCode = 'WEEKCODE_UNRESOLVED' AND hold.IsActive = 1))
        )
    );";

        public const string TrendSql = @"
SELECT
    [Date] = CAST(receipt.ReceiveDt AS DATE),
    ReceiptCount = COUNT(1),
    BoxCount = SUM(ISNULL(huStats.BoxCount, 0)),
    PackCount = SUM(ISNULL(huStats.PackCount, 0)),
    Qty = SUM(ISNULL(itemStats.Qty, 0))
FROM dbo.WMS_FG_FinishedGoodsInboundReceipt receipt
OUTER APPLY
(
    SELECT
        BoxCount = SUM(CASE WHEN hu.HUType = 'OUTER_BOX' THEN 1 ELSE 0 END),
        PackCount = SUM(CASE WHEN hu.HUType = 'INNER_PACK' THEN 1 ELSE 0 END)
    FROM dbo.WMS_FG_HU hu
    WHERE hu.SourceBusinessNo = 'FGI-' + CAST(receipt.Id AS VARCHAR(20))
) huStats
OUTER APPLY
(
    SELECT Qty = SUM(item.Qty)
    FROM dbo.WMS_FG_HU pack
    INNER JOIN dbo.WMS_FG_HU_Item item ON item.HUId = pack.Id
    WHERE pack.SourceBusinessNo = 'FGI-' + CAST(receipt.Id AS VARCHAR(20)) AND pack.HUType = 'INNER_PACK'
) itemStats
WHERE receipt.ReceiveDt >= @FromTime AND receipt.ReceiveDt < @ToTimeExclusive
GROUP BY CAST(receipt.ReceiveDt AS DATE)
ORDER BY [Date];";

        public void ProbeWmsDb()
        {
            using (SqlConnection configured = DbConnectionFactory.Create())
            using (SqlConnection connection = CreateHealthConnection(configured.ConnectionString)) Probe(connection);
        }

        public void ProbeMesDb()
        {
            using (SqlConnection configured = DbConnectionFactory.Create())
            using (SqlConnection connection = CreateHealthConnection(configured.ConnectionString)) Probe(connection);
        }

        public MonitoringBusinessSummaryDto GetBusinessSummary(DateTime fromTime, DateTime toTimeExclusive)
        {
            using (SqlConnection connection = DbConnectionFactory.Create())
            using (SqlCommand command = new SqlCommand(SummarySql, connection))
            {
                command.CommandTimeout = HealthTimeoutSeconds;
                AddRange(command, fromTime, toTimeExclusive);
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    reader.Read();
                    return new MonitoringBusinessSummaryDto
                    {
                        TodayReceiptCount = Int(reader, "TodayReceiptCount"),
                        TodayBoxCount = Int(reader, "TodayBoxCount"),
                        TodayPackCount = Int(reader, "TodayPackCount"),
                        TodayQty = Decimal(reader, "TodayQty"),
                        ActiveHoldBoxCount = Int(reader, "ActiveHoldBoxCount"),
                        InboundExceptionReceiptCount = Int(reader, "InboundExceptionReceiptCount")
                    };
                }
            }
        }

        public List<SuccessfulReceiptTrendPointDto> GetSuccessfulReceiptTrend(DateTime fromTime, DateTime toTimeExclusive)
        {
            List<SuccessfulReceiptTrendPointDto> result = new List<SuccessfulReceiptTrendPointDto>();
            using (SqlConnection connection = DbConnectionFactory.Create())
            using (SqlCommand command = new SqlCommand(TrendSql, connection))
            {
                command.CommandTimeout = HealthTimeoutSeconds;
                AddRange(command, fromTime, toTimeExclusive);
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader()) while (reader.Read()) result.Add(new SuccessfulReceiptTrendPointDto
                {
                    Date = Convert.ToDateTime(reader["Date"]), ReceiptCount = Int(reader, "ReceiptCount"),
                    BoxCount = Int(reader, "BoxCount"), PackCount = Int(reader, "PackCount"), Qty = Decimal(reader, "Qty")
                });
            }
            return result;
        }

        private static SqlConnection CreateHealthConnection(string connectionString)
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(connectionString) { ConnectTimeout = HealthTimeoutSeconds };
            return new SqlConnection(builder.ConnectionString);
        }

        private static void Probe(SqlConnection connection)
        {
            connection.Open();
            using (SqlCommand command = new SqlCommand("SELECT 1;", connection))
            {
                command.CommandTimeout = HealthTimeoutSeconds;
                command.ExecuteScalar();
            }
        }

        private static void AddRange(SqlCommand command, DateTime fromTime, DateTime toTimeExclusive)
        {
            command.Parameters.Add("@FromTime", SqlDbType.DateTime2).Value = fromTime;
            command.Parameters.Add("@ToTimeExclusive", SqlDbType.DateTime2).Value = toTimeExclusive;
        }

        private static int Int(SqlDataReader reader, string name) { return Convert.ToInt32(reader[name]); }
        private static decimal Decimal(SqlDataReader reader, string name) { return Convert.ToDecimal(reader[name]); }
    }
}
