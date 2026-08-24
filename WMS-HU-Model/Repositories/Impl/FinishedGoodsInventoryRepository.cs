using System;
using System.Data;
using System.Data.SqlClient;
using WMS_HU_Model.Infrastructure.Database;
using WMS_HU_Model.Models.Inventory;

namespace WMS_HU_Model.Repositories
{
    public class FinishedGoodsInventoryRepository : IFinishedGoodsInventoryRepository
    {
        public const string CurrentInventoryFromWhereSql = @"
FROM dbo.WMS_HU b
LEFT JOIN dbo.WMS_HU_CurrentLocation dc ON dc.HUId = b.Id
LEFT JOIN dbo.WMS_Location dl ON dl.Id = dc.LocationId
LEFT JOIN dbo.WMS_Warehouse dw ON dw.Id = dl.WarehouseId
LEFT JOIN dbo.WMS_Zone dz ON dz.Id = dl.ZoneId
LEFT JOIN dbo.WMS_HU_Relation pr ON pr.ChildHUId = b.Id AND pr.IsActive = 1
LEFT JOIN dbo.WMS_HU ph ON ph.Id = pr.ParentHUId AND ph.LifecycleStatus = 'ACTIVE'
LEFT JOIN dbo.WMS_HU_CurrentLocation pc ON pc.HUId = ph.Id
LEFT JOIN dbo.WMS_Location pl ON pl.Id = pc.LocationId
LEFT JOIN dbo.WMS_Warehouse pw ON pw.Id = pl.WarehouseId
LEFT JOIN dbo.WMS_Zone pz ON pz.Id = pl.ZoneId
LEFT JOIN dbo.WMS_HU_HoldLog hold ON hold.HUId = b.Id AND hold.IsActive = 1
INNER JOIN dbo.WMS_HU_Relation r ON r.ParentHUId = b.Id AND r.IsActive = 1
INNER JOIN dbo.WMS_HU pack ON pack.Id = r.ChildHUId
    AND pack.HUType = 'INNER_PACK'
    AND pack.LifecycleStatus = 'ACTIVE'
INNER JOIN dbo.WMS_HU_Item item ON item.HUId = pack.Id AND item.IsActive = 1
WHERE b.HUType = 'OUTER_BOX'
  AND b.LifecycleStatus = 'ACTIVE'
  AND COALESCE(dl.Id, pl.Id) IS NOT NULL
  AND COALESCE(dl.LocationCode, pl.LocationCode) <> 'SHIPPED'
  AND (@PlantCode IS NULL OR b.PlantCode = @PlantCode)
  AND (@MaterialCode IS NULL OR EXISTS
      (SELECT 1 FROM dbo.WMS_HU_Relation mr
       INNER JOIN dbo.WMS_HU mp ON mp.Id = mr.ChildHUId
           AND mp.HUType = 'INNER_PACK' AND mp.LifecycleStatus = 'ACTIVE'
       INNER JOIN dbo.WMS_HU_Item mi ON mi.HUId = mp.Id AND mi.IsActive = 1
       WHERE mr.ParentHUId = b.Id AND mr.IsActive = 1 AND mi.MaterialCode = @MaterialCode))
  AND (@MaterialRevision IS NULL OR EXISTS
      (SELECT 1 FROM dbo.WMS_HU_Relation rr
       INNER JOIN dbo.WMS_HU rp ON rp.Id = rr.ChildHUId
           AND rp.HUType = 'INNER_PACK' AND rp.LifecycleStatus = 'ACTIVE'
       INNER JOIN dbo.WMS_HU_Item ri ON ri.HUId = rp.Id AND ri.IsActive = 1
       WHERE rr.ParentHUId = b.Id AND rr.IsActive = 1 AND ISNULL(ri.MaterialRevision, '') = @MaterialRevision))
  AND (@BoxCode IS NULL OR b.HUCode LIKE '%' + @BoxCode + '%')
  AND (@ExactBoxCode IS NULL OR b.HUCode = @ExactBoxCode)
  AND (@InnerPackCode IS NULL OR EXISTS
      (SELECT 1 FROM dbo.WMS_HU_Relation ir
       INNER JOIN dbo.WMS_HU ip ON ip.Id = ir.ChildHUId
           AND ip.HUType = 'INNER_PACK' AND ip.LifecycleStatus = 'ACTIVE'
       WHERE ir.ParentHUId = b.Id AND ir.IsActive = 1 AND ip.HUCode LIKE '%' + @InnerPackCode + '%'))
  AND (@LocationCode IS NULL OR COALESCE(dl.LocationCode, pl.LocationCode) LIKE '%' + @LocationCode + '%')
  AND (@StockStatus IS NULL OR EXISTS
      (SELECT 1 FROM dbo.WMS_HU_Relation sr
       INNER JOIN dbo.WMS_HU sp ON sp.Id = sr.ChildHUId
           AND sp.HUType = 'INNER_PACK' AND sp.LifecycleStatus = 'ACTIVE'
       INNER JOIN dbo.WMS_HU_Item si ON si.HUId = sp.Id AND si.IsActive = 1
       WHERE sr.ParentHUId = b.Id AND sr.IsActive = 1 AND si.StockStatus = @StockStatus))
  AND (@IsHold IS NULL OR b.IsHold = @IsHold)
  AND (@WeekCode IS NULL OR EXISTS
      (SELECT 1 FROM dbo.WMS_HU_Relation wr
       INNER JOIN dbo.WMS_HU wp ON wp.Id = wr.ChildHUId
           AND wp.HUType = 'INNER_PACK' AND wp.LifecycleStatus = 'ACTIVE'
       INNER JOIN dbo.WMS_HU_Item wi ON wi.HUId = wp.Id AND wi.IsActive = 1
       WHERE wr.ParentHUId = b.Id AND wr.IsActive = 1
         AND ((wi.WeekYear IS NOT NULL AND wi.WeekNo IS NOT NULL AND
               CAST(wi.WeekYear AS VARCHAR(4)) + '-W' + RIGHT('0' + CAST(wi.WeekNo AS VARCHAR(2)), 2) = @WeekCode)
              OR CAST(wi.WeekSortKey AS VARCHAR(10)) = REPLACE(REPLACE(UPPER(@WeekCode), '-W', ''), 'W', ''))))";

        public const string QuantityAggregationSql = "SUM(item.Qty) AS TotalQty";

        private const string CountSql = @"
SELECT COUNT(DISTINCT b.Id)
" + CurrentInventoryFromWhereSql + ";";

        private const string RowsSql = @"
;WITH InventoryRows AS
(
SELECT
    b.Id AS BoxHuId,
    b.HUCode AS BoxCode,
    b.PlantCode,
    MIN(item.MaterialCode) AS MaterialCode,
    MIN(item.MaterialRevision) AS MaterialRevision,
    " + QuantityAggregationSql + @",
    MIN(item.Uom) AS Uom,
    COALESCE(dl.LocationCode, pl.LocationCode) AS EffectiveLocationCode,
    COALESCE(dw.WarehouseCode, pw.WarehouseCode) AS WarehouseCode,
    COALESCE(dz.ZoneCode, pz.ZoneCode) AS ZoneCode,
    COALESCE(dl.LocationTypeCode, pl.LocationTypeCode) AS LocationTypeCode,
    CAST(CASE WHEN dl.Id IS NULL AND pl.Id IS NOT NULL THEN 1 ELSE 0 END AS BIT) AS IsLocationInherited,
    b.LifecycleStatus,
    b.IsHold,
    hold.HoldReasonCode,
    COALESCE(hold.HoldReason, b.HoldReason) AS HoldReason,
    CASE WHEN ph.HUType = 'PALLET' THEN ph.HUCode ELSE NULL END AS ParentPalletCode,
    COUNT(DISTINCT pack.Id) AS InnerPackCount,
    b.CreateTime AS CreatedTime
" + CurrentInventoryFromWhereSql + @"
GROUP BY
    b.Id, b.HUCode, b.PlantCode, b.LifecycleStatus, b.IsHold, b.HoldReason, b.CreateTime,
    dl.Id, pl.Id, dl.LocationCode, pl.LocationCode, dw.WarehouseCode, pw.WarehouseCode,
    dz.ZoneCode, pz.ZoneCode, dl.LocationTypeCode, pl.LocationTypeCode,
    hold.HoldReasonCode, hold.HoldReason, ph.HUType, ph.HUCode
)
SELECT
    inventory.*,
    STUFF((
        SELECT DISTINCT ', ' + CASE
            WHEN wi.WeekYear IS NULL OR wi.WeekNo IS NULL THEN N'未解析'
            ELSE CAST(wi.WeekYear AS VARCHAR(4)) + '-W' + RIGHT('0' + CAST(wi.WeekNo AS VARCHAR(2)), 2)
        END
        FROM dbo.WMS_HU_Relation wr
        INNER JOIN dbo.WMS_HU wp ON wp.Id = wr.ChildHUId
            AND wp.HUType = 'INNER_PACK' AND wp.LifecycleStatus = 'ACTIVE'
        INNER JOIN dbo.WMS_HU_Item wi ON wi.HUId = wp.Id AND wi.IsActive = 1
        WHERE wr.ParentHUId = inventory.BoxHuId AND wr.IsActive = 1
        FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, '') AS WeekCodeSummary,
    (SELECT COUNT(*) FROM
        (SELECT wi.WeekYear, wi.WeekNo
         FROM dbo.WMS_HU_Relation wr
         INNER JOIN dbo.WMS_HU wp ON wp.Id = wr.ChildHUId
             AND wp.HUType = 'INNER_PACK' AND wp.LifecycleStatus = 'ACTIVE'
         INNER JOIN dbo.WMS_HU_Item wi ON wi.HUId = wp.Id AND wi.IsActive = 1
         WHERE wr.ParentHUId = inventory.BoxHuId AND wr.IsActive = 1
         GROUP BY wi.WeekYear, wi.WeekNo) weekGroups) AS WeekCodeCount,
    STUFF((
        SELECT DISTINCT ', ' + si.StockStatus
        FROM dbo.WMS_HU_Relation sr
        INNER JOIN dbo.WMS_HU sp ON sp.Id = sr.ChildHUId
            AND sp.HUType = 'INNER_PACK' AND sp.LifecycleStatus = 'ACTIVE'
        INNER JOIN dbo.WMS_HU_Item si ON si.HUId = sp.Id AND si.IsActive = 1
        WHERE sr.ParentHUId = inventory.BoxHuId AND sr.IsActive = 1
        FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, '') AS StockStatus
FROM InventoryRows inventory
ORDER BY inventory.CreatedTime DESC, inventory.BoxHuId DESC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";

        private const string PreviewDetailSql = @"
SELECT
    CASE WHEN item.WeekYear IS NULL OR item.WeekNo IS NULL THEN N'未解析'
         ELSE CAST(item.WeekYear AS VARCHAR(4)) + '-W' + RIGHT('0' + CAST(item.WeekNo AS VARCHAR(2)), 2) END AS WeekCode,
    item.WeekSortKey,
    COUNT(DISTINCT pack.Id) AS PackCount,
    SUM(item.Qty) AS Qty
FROM dbo.WMS_HU b
INNER JOIN dbo.WMS_HU_Relation r ON r.ParentHUId = b.Id AND r.IsActive = 1
INNER JOIN dbo.WMS_HU pack ON pack.Id = r.ChildHUId
    AND pack.HUType = 'INNER_PACK' AND pack.LifecycleStatus = 'ACTIVE'
INNER JOIN dbo.WMS_HU_Item item ON item.HUId = pack.Id AND item.IsActive = 1
WHERE b.HUCode = @ExactBoxCode AND b.HUType = 'OUTER_BOX' AND b.LifecycleStatus = 'ACTIVE'
GROUP BY item.WeekYear, item.WeekNo, item.WeekSortKey
ORDER BY item.WeekSortKey, WeekCode;

SELECT
    pack.Id AS PackHuId,
    pack.HUCode AS PackCode,
    SUM(item.Qty) AS Qty,
    MIN(item.Uom) AS Uom,
    MIN(item.DateCode) AS DateCode,
    CASE WHEN MIN(item.WeekYear) IS NULL OR MIN(item.WeekNo) IS NULL THEN N'未解析'
         ELSE CAST(MIN(item.WeekYear) AS VARCHAR(4)) + '-W' + RIGHT('0' + CAST(MIN(item.WeekNo) AS VARCHAR(2)), 2) END AS WeekCode,
    MIN(item.WeekSortKey) AS WeekSortKey,
    MIN(item.StockStatus) AS StockStatus,
    pack.IsHold
FROM dbo.WMS_HU b
INNER JOIN dbo.WMS_HU_Relation r ON r.ParentHUId = b.Id AND r.IsActive = 1
INNER JOIN dbo.WMS_HU pack ON pack.Id = r.ChildHUId
    AND pack.HUType = 'INNER_PACK' AND pack.LifecycleStatus = 'ACTIVE'
INNER JOIN dbo.WMS_HU_Item item ON item.HUId = pack.Id AND item.IsActive = 1
WHERE b.HUCode = @ExactBoxCode AND b.HUType = 'OUTER_BOX' AND b.LifecycleStatus = 'ACTIVE'
GROUP BY pack.Id, pack.HUCode, pack.IsHold
ORDER BY pack.Id;";

        public FinishedGoodsInventoryResultDto Query(FinishedGoodsInventoryQuery query)
        {
            using (SqlConnection connection = DbConnectionFactory.Create())
            {
                connection.Open();
                int totalCount;
                using (SqlCommand countCommand = new SqlCommand(CountSql, connection))
                {
                    AddQueryParameters(countCommand, query, null);
                    totalCount = Convert.ToInt32(countCommand.ExecuteScalar());
                }

                FinishedGoodsInventoryResultDto result = new FinishedGoodsInventoryResultDto
                {
                    TotalCount = totalCount,
                    Page = query.Page,
                    PageSize = query.PageSize
                };
                using (SqlCommand rowsCommand = new SqlCommand(RowsSql, connection))
                {
                    AddQueryParameters(rowsCommand, query, null);
                    rowsCommand.Parameters.Add("@Offset", SqlDbType.Int).Value = (query.Page - 1) * query.PageSize;
                    rowsCommand.Parameters.Add("@PageSize", SqlDbType.Int).Value = query.PageSize;
                    using (SqlDataReader reader = rowsCommand.ExecuteReader())
                    {
                        while (reader.Read()) result.Items.Add(ReadRow(reader));
                    }
                }
                return result;
            }
        }

        public FinishedGoodsInventoryPreviewDto GetPreview(string boxCode)
        {
            FinishedGoodsInventoryQuery query = new FinishedGoodsInventoryQuery { Page = 1, PageSize = 1 };
            using (SqlConnection connection = DbConnectionFactory.Create())
            {
                connection.Open();
                FinishedGoodsInventoryRowDto box = null;
                using (SqlCommand boxCommand = new SqlCommand(RowsSql, connection))
                {
                    AddQueryParameters(boxCommand, query, boxCode);
                    boxCommand.Parameters.Add("@Offset", SqlDbType.Int).Value = 0;
                    boxCommand.Parameters.Add("@PageSize", SqlDbType.Int).Value = 1;
                    using (SqlDataReader reader = boxCommand.ExecuteReader())
                    {
                        if (reader.Read()) box = ReadRow(reader);
                    }
                }
                if (box == null) return null;

                FinishedGoodsInventoryPreviewDto preview = new FinishedGoodsInventoryPreviewDto { Box = box };
                using (SqlCommand detailCommand = new SqlCommand(PreviewDetailSql, connection))
                {
                    AddVarchar(detailCommand, "@ExactBoxCode", boxCode, 100);
                    using (SqlDataReader reader = detailCommand.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            preview.WeekCodes.Add(new FinishedGoodsInventoryWeekDto
                            {
                                WeekCode = StringValue(reader, "WeekCode"),
                                WeekSortKey = NullableInt32(reader, "WeekSortKey"),
                                PackCount = Int32Value(reader, "PackCount"),
                                Qty = DecimalValue(reader, "Qty")
                            });
                        }
                        reader.NextResult();
                        while (reader.Read())
                        {
                            preview.InnerPacks.Add(new FinishedGoodsInventoryPackDto
                            {
                                PackHuId = Int64Value(reader, "PackHuId"),
                                PackCode = StringValue(reader, "PackCode"),
                                Qty = DecimalValue(reader, "Qty"),
                                Uom = StringValue(reader, "Uom"),
                                DateCode = StringValue(reader, "DateCode"),
                                WeekCode = StringValue(reader, "WeekCode"),
                                WeekSortKey = NullableInt32(reader, "WeekSortKey"),
                                StockStatus = StringValue(reader, "StockStatus"),
                                IsHold = BooleanValue(reader, "IsHold")
                            });
                        }
                    }
                }
                return preview;
            }
        }

        private static void AddQueryParameters(SqlCommand command, FinishedGoodsInventoryQuery query, string exactBoxCode)
        {
            AddVarchar(command, "@PlantCode", query.PlantCode, 20);
            AddVarchar(command, "@MaterialCode", query.MaterialCode, 50);
            AddVarchar(command, "@MaterialRevision", query.MaterialRevision, 30);
            AddVarchar(command, "@BoxCode", query.BoxCode, 100);
            AddVarchar(command, "@ExactBoxCode", exactBoxCode, 100);
            AddVarchar(command, "@InnerPackCode", query.InnerPackCode, 100);
            AddVarchar(command, "@LocationCode", query.LocationCode, 50);
            AddVarchar(command, "@WeekCode", query.WeekCode, 20);
            AddVarchar(command, "@StockStatus", query.StockStatus, 20);
            command.Parameters.Add("@IsHold", SqlDbType.Bit).Value = query.IsHold.HasValue ? (object)query.IsHold.Value : DBNull.Value;
        }

        private static void AddVarchar(SqlCommand command, string name, string value, int size)
        {
            command.Parameters.Add(name, SqlDbType.VarChar, size).Value = value == null ? (object)DBNull.Value : value;
        }

        private static FinishedGoodsInventoryRowDto ReadRow(SqlDataReader reader)
        {
            return new FinishedGoodsInventoryRowDto
            {
                BoxHuId = Int64Value(reader, "BoxHuId"),
                BoxCode = StringValue(reader, "BoxCode"),
                PlantCode = StringValue(reader, "PlantCode"),
                MaterialCode = StringValue(reader, "MaterialCode"),
                MaterialRevision = StringValue(reader, "MaterialRevision"),
                TotalQty = DecimalValue(reader, "TotalQty"),
                Uom = StringValue(reader, "Uom"),
                WeekCodeSummary = StringValue(reader, "WeekCodeSummary"),
                WeekCodeCount = Int32Value(reader, "WeekCodeCount"),
                EffectiveLocationCode = StringValue(reader, "EffectiveLocationCode"),
                WarehouseCode = StringValue(reader, "WarehouseCode"),
                ZoneCode = StringValue(reader, "ZoneCode"),
                LocationTypeCode = StringValue(reader, "LocationTypeCode"),
                IsLocationInherited = BooleanValue(reader, "IsLocationInherited"),
                StockStatus = StringValue(reader, "StockStatus"),
                LifecycleStatus = StringValue(reader, "LifecycleStatus"),
                IsHold = BooleanValue(reader, "IsHold"),
                HoldReasonCode = StringValue(reader, "HoldReasonCode"),
                HoldReason = StringValue(reader, "HoldReason"),
                ParentPalletCode = StringValue(reader, "ParentPalletCode"),
                InnerPackCount = Int32Value(reader, "InnerPackCount"),
                CreatedTime = DateTimeValue(reader, "CreatedTime")
            };
        }

        private static string StringValue(SqlDataReader reader, string name) { int index = reader.GetOrdinal(name); return reader.IsDBNull(index) ? null : reader.GetString(index); }
        private static long Int64Value(SqlDataReader reader, string name) { return reader.GetInt64(reader.GetOrdinal(name)); }
        private static int Int32Value(SqlDataReader reader, string name) { return Convert.ToInt32(reader.GetValue(reader.GetOrdinal(name))); }
        private static int? NullableInt32(SqlDataReader reader, string name) { int index = reader.GetOrdinal(name); return reader.IsDBNull(index) ? (int?)null : Convert.ToInt32(reader.GetValue(index)); }
        private static decimal DecimalValue(SqlDataReader reader, string name) { return reader.GetDecimal(reader.GetOrdinal(name)); }
        private static bool BooleanValue(SqlDataReader reader, string name) { return reader.GetBoolean(reader.GetOrdinal(name)); }
        private static DateTime DateTimeValue(SqlDataReader reader, string name) { return reader.GetDateTime(reader.GetOrdinal(name)); }
    }
}
