using System;
using System.Data;
using System.Data.SqlClient;
using WMS_HU_Model.Infrastructure.Database;
using WMS_HU_Model.Models.Inbound;

namespace WMS_HU_Model.Repositories
{
    public class FinishedGoodsInboundReadRepository : IFinishedGoodsInboundReadRepository
    {
        public const string ReceiptStatsSql = @"
;WITH ReceiptStats AS
(
    SELECT
        receipt.Id AS ReceiptId,
        receipt.ReceiveDt AS ReceiveTime,
        receipt.SourceSystem,
        receipt.SourceType,
        receipt.SourceEntityId,
        receipt.RequestId,
        receipt.ReceiveBy AS Operator,
        receipt.Remark,
        huStats.PlantCode,
        huStats.PalletSummary,
        huStats.PalletCount,
        huStats.BoxCount,
        huStats.InnerPackCount,
        ISNULL(itemStats.TotalQty, 0) AS TotalQty,
        itemStats.MaterialSummary,
        ISNULL(itemStats.MaterialCount, 0) AS MaterialCount,
        boxStats.HoldBoxCount,
        boxStats.WeekCodeExceptionBoxCount,
        boxStats.ExceptionBoxCount
    FROM dbo.WMS_FG_FinishedGoodsInboundReceipt receipt
    CROSS APPLY
    (
        SELECT
            MIN(hu.PlantCode) AS PlantCode,
            MIN(CASE WHEN hu.HUType = 'PALLET' THEN hu.HUCode END) AS PalletSummary,
            COUNT(CASE WHEN hu.HUType = 'PALLET' THEN 1 END) AS PalletCount,
            COUNT(CASE WHEN hu.HUType = 'OUTER_BOX' THEN 1 END) AS BoxCount,
            COUNT(CASE WHEN hu.HUType = 'INNER_PACK' THEN 1 END) AS InnerPackCount
        FROM dbo.WMS_FG_HU hu
        WHERE hu.SourceBusinessNo = 'FGI-' + CAST(receipt.Id AS VARCHAR(20))
    ) huStats
    CROSS APPLY
    (
        SELECT
            ISNULL(SUM(CASE WHEN boxFact.IsHold = 1 THEN 1 ELSE 0 END), 0) AS HoldBoxCount,
            ISNULL(SUM(boxFact.HasWeekCodeException), 0) AS WeekCodeExceptionBoxCount,
            ISNULL(SUM(CASE WHEN boxFact.IsHold = 1 OR boxFact.HasWeekCodeException = 1 THEN 1 ELSE 0 END), 0) AS ExceptionBoxCount
        FROM
        (
            SELECT box.IsHold,
                CASE WHEN EXISTS
                    (SELECT 1 FROM dbo.WMS_FG_HU_HoldLog weekHold WHERE weekHold.HUId = box.Id AND weekHold.HoldReasonCode = 'WEEKCODE_UNRESOLVED' AND weekHold.IsActive = 1) THEN 1 ELSE 0 END AS HasWeekCodeException
            FROM dbo.WMS_FG_HU box
            WHERE box.SourceBusinessNo = 'FGI-' + CAST(receipt.Id AS VARCHAR(20)) AND box.HUType = 'OUTER_BOX'
        ) boxFact
    ) boxStats
    OUTER APPLY
    (
        SELECT
            SUM(item.Qty) AS TotalQty,
            COUNT(DISTINCT item.ProdNo) AS MaterialCount,
            STUFF((
                SELECT DISTINCT ', ' + materialItem.ProdNo
                FROM dbo.WMS_FG_HU materialPack
                INNER JOIN dbo.WMS_FG_HU_Item materialItem ON materialItem.HUId = materialPack.Id
                WHERE materialPack.HUType = 'INNER_PACK'
                  AND materialPack.SourceBusinessNo = 'FGI-' + CAST(receipt.Id AS VARCHAR(20))
                FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, '') AS MaterialSummary
        FROM dbo.WMS_FG_HU pack
        INNER JOIN dbo.WMS_FG_HU_Item item ON item.HUId = pack.Id
        WHERE pack.HUType = 'INNER_PACK'
          AND pack.SourceBusinessNo = 'FGI-' + CAST(receipt.Id AS VARCHAR(20))
    ) itemStats
    WHERE (@ReceiptId IS NULL OR receipt.Id = @ReceiptId)
      AND (@FromTime IS NULL OR receipt.ReceiveDt >= @FromTime)
      AND (@ToTimeExclusive IS NULL OR receipt.ReceiveDt < @ToTimeExclusive)
      AND (@SourceSystem IS NULL OR receipt.SourceSystem = @SourceSystem)
      AND (@SourceType IS NULL OR receipt.SourceType = @SourceType)
      AND (@SourceEntityId IS NULL OR receipt.SourceEntityId LIKE '%' + @SourceEntityId + '%')
      AND (@RequestId IS NULL OR receipt.RequestId LIKE '%' + @RequestId + '%')
      AND (@PlantCode IS NULL OR EXISTS
          (SELECT 1 FROM dbo.WMS_FG_HU plantHu WHERE plantHu.SourceBusinessNo = 'FGI-' + CAST(receipt.Id AS VARCHAR(20)) AND plantHu.PlantCode = @PlantCode))
      AND (@PalletCode IS NULL OR EXISTS
          (SELECT 1 FROM dbo.WMS_FG_HU palletHu WHERE palletHu.SourceBusinessNo = 'FGI-' + CAST(receipt.Id AS VARCHAR(20)) AND palletHu.HUType = 'PALLET' AND palletHu.HUCode LIKE '%' + @PalletCode + '%'))
      AND (@BoxCode IS NULL OR EXISTS
          (SELECT 1 FROM dbo.WMS_FG_HU boxHu WHERE boxHu.SourceBusinessNo = 'FGI-' + CAST(receipt.Id AS VARCHAR(20)) AND boxHu.HUType = 'OUTER_BOX' AND boxHu.HUCode LIKE '%' + @BoxCode + '%'))
      AND (@MaterialCode IS NULL OR EXISTS
          (SELECT 1 FROM dbo.WMS_FG_HU materialPack
           INNER JOIN dbo.WMS_FG_HU_Item materialItem ON materialItem.HUId = materialPack.Id
           WHERE materialPack.SourceBusinessNo = 'FGI-' + CAST(receipt.Id AS VARCHAR(20)) AND materialPack.HUType = 'INNER_PACK' AND materialItem.ProdNo = @MaterialCode))
)
";

        public const string RowsSql = ReceiptStatsSql + @"
SELECT *
FROM ReceiptStats
WHERE (@ExceptionOnly = 0 OR ExceptionBoxCount > 0)
  AND (@ExceptionCategory IS NULL
       OR (@ExceptionCategory = 'WEEKCODE' AND WeekCodeExceptionBoxCount > 0)
       OR (@ExceptionCategory = 'HOLD' AND HoldBoxCount > 0))
ORDER BY ReceiveTime DESC, ReceiptId DESC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";

        public const string CountSql = ReceiptStatsSql + @"
SELECT COUNT(1)
FROM ReceiptStats
WHERE (@ExceptionOnly = 0 OR ExceptionBoxCount > 0)
  AND (@ExceptionCategory IS NULL
       OR (@ExceptionCategory = 'WEEKCODE' AND WeekCodeExceptionBoxCount > 0)
       OR (@ExceptionCategory = 'HOLD' AND HoldBoxCount > 0));";

        private const string DetailHeaderSql = ReceiptStatsSql + "SELECT * FROM ReceiptStats;";

        private const string PalletsSql = @"
SELECT hu.Id AS HuId, hu.HUCode AS PalletCode, hu.LifecycleStatus
FROM dbo.WMS_FG_HU hu
WHERE hu.SourceBusinessNo = 'FGI-' + CAST(@ReceiptId AS VARCHAR(20)) AND hu.HUType = 'PALLET'
ORDER BY hu.Id;";

        private const string BoxesSql = @"
SELECT TOP (@BoxLimit)
    box.Id AS HuId,
    box.HUCode AS BoxCode,
    box.LifecycleStatus,
    MIN(item.ProdNo) AS MaterialCode,
    MIN(item.ProdRev) AS MaterialRevision,
    ISNULL(SUM(item.Qty), 0) AS TotalQty,
    MIN(item.Uom) AS Uom,
    COUNT(DISTINCT pack.Id) AS InnerPackCount,
    STUFF((
        SELECT DISTINCT ', ' + CASE WHEN weekItem.WeekYear IS NULL OR weekItem.WeekNo IS NULL THEN N'未解析'
            ELSE CAST(weekItem.WeekYear AS VARCHAR(4)) + '-W' + RIGHT('0' + CAST(weekItem.WeekNo AS VARCHAR(2)), 2) END
        FROM dbo.WMS_FG_HU_Relation weekRelation
        INNER JOIN dbo.WMS_FG_HU weekPack ON weekPack.Id = weekRelation.ChildHUId
            AND weekPack.HUType = 'INNER_PACK' AND weekPack.SourceBusinessNo = box.SourceBusinessNo
        INNER JOIN dbo.WMS_FG_HU_Item weekItem ON weekItem.HUId = weekPack.Id
        WHERE weekRelation.ParentHUId = box.Id
        FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, '') AS WeekCodeSummary,
    box.IsHold,
    activeHold.HoldReasonCode,
    activeHold.HoldReason AS HoldReason,
    CAST(CASE WHEN EXISTS
        (SELECT 1 FROM dbo.WMS_FG_HU_HoldLog weekHold WHERE weekHold.HUId = box.Id AND weekHold.HoldReasonCode = 'WEEKCODE_UNRESOLVED' AND weekHold.IsActive = 1) THEN 1 ELSE 0 END AS BIT) AS HasWeekCodeException
FROM dbo.WMS_FG_FinishedGoodsInboundReceipt receipt
INNER JOIN dbo.WMS_FG_HU box ON box.SourceBusinessNo = 'FGI-' + CAST(receipt.Id AS VARCHAR(20)) AND box.HUType = 'OUTER_BOX'
LEFT JOIN dbo.WMS_FG_HU_Relation relation ON relation.ParentHUId = box.Id
LEFT JOIN dbo.WMS_FG_HU pack ON pack.Id = relation.ChildHUId AND pack.HUType = 'INNER_PACK' AND pack.SourceBusinessNo = box.SourceBusinessNo
LEFT JOIN dbo.WMS_FG_HU_Item item ON item.HUId = pack.Id
OUTER APPLY
(
    SELECT TOP 1 hold.HoldReasonCode, hold.HoldReason
    FROM dbo.WMS_FG_HU_HoldLog hold
    WHERE hold.HUId = box.Id AND hold.IsActive = 1
    ORDER BY hold.HoldDt DESC, hold.Id DESC
) activeHold
WHERE receipt.Id = @ReceiptId
GROUP BY box.Id, box.HUCode, box.LifecycleStatus, box.IsHold, box.SourceBusinessNo,
    activeHold.HoldReasonCode, activeHold.HoldReason, receipt.ReceiveDt
ORDER BY box.Id;";

        public FinishedGoodsInboundRecordResultDto Query(FinishedGoodsInboundRecordQuery query)
        {
            FinishedGoodsInboundRecordResultDto result = new FinishedGoodsInboundRecordResultDto { Page = query.Page, PageSize = query.PageSize };
            using (SqlConnection connection = DbConnectionFactory.Create())
            {
                connection.Open();
                using (SqlCommand count = new SqlCommand(CountSql, connection))
                {
                    AddParameters(count, query, null);
                    result.TotalCount = Convert.ToInt32(count.ExecuteScalar());
                }
                using (SqlCommand rows = new SqlCommand(RowsSql, connection))
                {
                    AddParameters(rows, query, null);
                    rows.Parameters.Add("@Offset", SqlDbType.Int).Value = (query.Page - 1) * query.PageSize;
                    rows.Parameters.Add("@PageSize", SqlDbType.Int).Value = query.PageSize;
                    using (SqlDataReader reader = rows.ExecuteReader()) while (reader.Read()) result.Items.Add(ReadRecord(reader));
                }
            }
            return result;
        }

        public FinishedGoodsInboundDetailDto GetDetail(long receiptId, int boxLimit)
        {
            FinishedGoodsInboundDetailDto detail = new FinishedGoodsInboundDetailDto { BoxLimit = boxLimit };
            FinishedGoodsInboundRecordQuery empty = new FinishedGoodsInboundRecordQuery();
            using (SqlConnection connection = DbConnectionFactory.Create())
            {
                connection.Open();
                using (SqlCommand header = new SqlCommand(DetailHeaderSql, connection))
                {
                    AddParameters(header, empty, receiptId);
                    using (SqlDataReader reader = header.ExecuteReader()) if (reader.Read()) detail.Record = ReadRecord(reader);
                }
                if (detail.Record == null) return null;
                using (SqlCommand pallets = new SqlCommand(PalletsSql, connection))
                {
                    pallets.Parameters.Add("@ReceiptId", SqlDbType.BigInt).Value = receiptId;
                    using (SqlDataReader reader = pallets.ExecuteReader()) while (reader.Read()) detail.Pallets.Add(new FinishedGoodsInboundPalletDto
                    {
                        HuId = Long(reader, "HuId"), PalletCode = Text(reader, "PalletCode"), LifecycleStatus = Text(reader, "LifecycleStatus")
                    });
                }
                using (SqlCommand boxes = new SqlCommand(BoxesSql, connection))
                {
                    boxes.Parameters.Add("@ReceiptId", SqlDbType.BigInt).Value = receiptId;
                    boxes.Parameters.Add("@BoxLimit", SqlDbType.Int).Value = boxLimit;
                    using (SqlDataReader reader = boxes.ExecuteReader()) while (reader.Read()) detail.Boxes.Add(new FinishedGoodsInboundBoxDto
                    {
                        HuId = Long(reader, "HuId"), BoxCode = Text(reader, "BoxCode"), LifecycleStatus = Text(reader, "LifecycleStatus"),
                        MaterialCode = Text(reader, "MaterialCode"), MaterialRevision = Text(reader, "MaterialRevision"), TotalQty = Decimal(reader, "TotalQty"),
                        Uom = Text(reader, "Uom"), InnerPackCount = Int(reader, "InnerPackCount"), WeekCodeSummary = Text(reader, "WeekCodeSummary"),
                        IsHold = Bool(reader, "IsHold"), HoldReasonCode = Text(reader, "HoldReasonCode"), HoldReason = Text(reader, "HoldReason"),
                        HasWeekCodeException = Bool(reader, "HasWeekCodeException")
                    });
                }
            }
            return detail;
        }

        private static void AddParameters(SqlCommand command, FinishedGoodsInboundRecordQuery query, long? receiptId)
        {
            NullableBigInt(command, "@ReceiptId", receiptId);
            NullableDateTime(command, "@FromTime", query.FromTime);
            NullableDateTime(command, "@ToTimeExclusive", query.ToTimeExclusive);
            Varchar(command, "@PlantCode", query.PlantCode, 20);
            Varchar(command, "@SourceSystem", query.SourceSystem, 30);
            Varchar(command, "@SourceType", query.SourceType, 50);
            Varchar(command, "@SourceEntityId", query.SourceEntityId, 100);
            Varchar(command, "@PalletCode", query.PalletCode, 100);
            Varchar(command, "@BoxCode", query.BoxCode, 100);
            Varchar(command, "@MaterialCode", query.MaterialCode, 50);
            Varchar(command, "@RequestId", query.RequestId, 100);
            Varchar(command, "@ExceptionCategory", query.ExceptionCategory, 30);
            command.Parameters.Add("@ExceptionOnly", SqlDbType.Bit).Value = query.ExceptionOnly;
        }

        private static FinishedGoodsInboundRecordDto ReadRecord(SqlDataReader reader)
        {
            return new FinishedGoodsInboundRecordDto
            {
                ReceiptId = Long(reader, "ReceiptId"), ReceiveTime = Date(reader, "ReceiveTime"), SourceSystem = Text(reader, "SourceSystem"),
                SourceType = Text(reader, "SourceType"), SourceEntityId = Text(reader, "SourceEntityId"), PlantCode = Text(reader, "PlantCode"),
                PalletSummary = Text(reader, "PalletSummary"), PalletCount = Int(reader, "PalletCount"), BoxCount = Int(reader, "BoxCount"),
                InnerPackCount = Int(reader, "InnerPackCount"), TotalQty = Decimal(reader, "TotalQty"), MaterialSummary = Text(reader, "MaterialSummary"),
                MaterialCount = Int(reader, "MaterialCount"), HoldBoxCount = Int(reader, "HoldBoxCount"), WeekCodeExceptionBoxCount = Int(reader, "WeekCodeExceptionBoxCount"),
                ExceptionBoxCount = Int(reader, "ExceptionBoxCount"), RequestId = Text(reader, "RequestId"), Operator = Text(reader, "Operator"), Remark = Text(reader, "Remark")
            };
        }

        private static void Varchar(SqlCommand command, string name, string value, int size) { command.Parameters.Add(name, SqlDbType.VarChar, size).Value = (object)value ?? DBNull.Value; }
        private static void NullableDateTime(SqlCommand command, string name, DateTime? value) { command.Parameters.Add(name, SqlDbType.DateTime2).Value = value.HasValue ? (object)value.Value : DBNull.Value; }
        private static void NullableBigInt(SqlCommand command, string name, long? value) { command.Parameters.Add(name, SqlDbType.BigInt).Value = value.HasValue ? (object)value.Value : DBNull.Value; }
        private static string Text(SqlDataReader reader, string name) { int index = reader.GetOrdinal(name); return reader.IsDBNull(index) ? null : Convert.ToString(reader.GetValue(index)); }
        private static long Long(SqlDataReader reader, string name) { return Convert.ToInt64(reader[name]); }
        private static int Int(SqlDataReader reader, string name) { return Convert.ToInt32(reader[name]); }
        private static decimal Decimal(SqlDataReader reader, string name) { return Convert.ToDecimal(reader[name]); }
        private static bool Bool(SqlDataReader reader, string name) { return Convert.ToBoolean(reader[name]); }
        private static DateTime Date(SqlDataReader reader, string name) { return Convert.ToDateTime(reader[name]); }
    }
}
