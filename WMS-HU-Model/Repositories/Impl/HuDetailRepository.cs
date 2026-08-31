using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using WMS_HU_Model.Infrastructure.Database;
using WMS_HU_Model.Models.HU;

namespace WMS_HU_Model.Repositories
{
    public class HuDetailRepository : IHuDetailRepository
    {
        public const string OuterBoxContentsSql = @"
SELECT
    pack.Id AS HuId,
    pack.HUCode AS HuCode,
    pack.HUType AS HuType,
    pack.LifecycleStatus,
    pack.IsHold,
    item.Id AS ItemId,
    item.ProdNo AS MaterialCode,
    item.ProdRev AS MaterialRevision,
    item.LotNo AS BatchNo,
    item.ProductionOrderNo,
    item.DateCode,
    item.WeekFormatCode,
    item.WeekYear,
    item.WeekNo,
    item.WeekSortKey,
    item.WeekRuleSource,
    item.WeekRuleValue,
    item.Qty,
    item.Uom,
    item.StockStatus,
    item.CreateDt AS CreatedTime
FROM dbo.WMS_FG_HU_Relation relation
INNER JOIN dbo.WMS_FG_HU pack ON pack.Id = relation.ChildHUId
    AND pack.HUType = 'INNER_PACK'
INNER JOIN dbo.WMS_FG_HU_Item item ON item.HUId = pack.Id AND item.IsActive = 1
WHERE relation.ParentHUId = @HUId AND relation.IsActive = 1
ORDER BY pack.Id, item.Id;";

        public const string InnerPackContentsSql = @"
SELECT
    hu.Id AS HuId,
    hu.HUCode AS HuCode,
    hu.HUType AS HuType,
    hu.LifecycleStatus,
    hu.IsHold,
    item.Id AS ItemId,
    item.ProdNo AS MaterialCode,
    item.ProdRev AS MaterialRevision,
    item.LotNo AS BatchNo,
    item.ProductionOrderNo,
    item.DateCode,
    item.WeekFormatCode,
    item.WeekYear,
    item.WeekNo,
    item.WeekSortKey,
    item.WeekRuleSource,
    item.WeekRuleValue,
    item.Qty,
    item.Uom,
    item.StockStatus,
    item.CreateDt AS CreatedTime
FROM dbo.WMS_FG_HU hu
INNER JOIN dbo.WMS_FG_HU_Item item ON item.HUId = hu.Id AND item.IsActive = 1
WHERE hu.Id = @HUId
ORDER BY item.Id;";

        public const string PalletContentsSql = @"
SELECT
    box.Id AS HuId,
    box.HUCode AS HuCode,
    box.HUType AS HuType,
    box.LifecycleStatus,
    box.IsHold,
    CAST(NULL AS BIGINT) AS ItemId,
    MIN(item.ProdNo) AS MaterialCode,
    MIN(item.ProdRev) AS MaterialRevision,
    CAST(NULL AS VARCHAR(100)) AS BatchNo,
    CAST(NULL AS VARCHAR(50)) AS ProductionOrderNo,
    CAST(NULL AS VARCHAR(50)) AS DateCode,
    CAST(NULL AS VARCHAR(30)) AS WeekFormatCode,
    CAST(NULL AS SMALLINT) AS WeekYear,
    CAST(NULL AS TINYINT) AS WeekNo,
    MIN(item.WeekSortKey) AS WeekSortKey,
    CAST(NULL AS VARCHAR(30)) AS WeekRuleSource,
    CAST(NULL AS VARCHAR(100)) AS WeekRuleValue,
    ISNULL(SUM(item.Qty), 0) AS Qty,
    MIN(item.Uom) AS Uom,
    MIN(item.StockStatus) AS StockStatus,
    MIN(item.CreateDt) AS CreatedTime
FROM dbo.WMS_FG_HU_Relation palletRelation
INNER JOIN dbo.WMS_FG_HU box ON box.Id = palletRelation.ChildHUId
    AND box.HUType = 'OUTER_BOX'
LEFT JOIN dbo.WMS_FG_HU_Relation boxRelation ON boxRelation.ParentHUId = box.Id AND boxRelation.IsActive = 1
LEFT JOIN dbo.WMS_FG_HU pack ON pack.Id = boxRelation.ChildHUId AND pack.HUType = 'INNER_PACK'
LEFT JOIN dbo.WMS_FG_HU_Item item ON item.HUId = pack.Id AND item.IsActive = 1
WHERE palletRelation.ParentHUId = @HUId AND palletRelation.IsActive = 1
GROUP BY box.Id, box.HUCode, box.HUType, box.LifecycleStatus, box.IsHold
ORDER BY box.Id;";

        public const string OperationLogsSql = @"
SELECT TOP (@Top)
    log.Id,
    log.OperationNo,
    log.OperationType,
    hu.HUCode AS HuCode,
    related.HUCode AS RelatedHuCode,
    log.BusinessNo,
    log.RequestId,
    log.DetailJson,
    log.OperationBy AS Operator,
    log.OperationDt AS OperationTime,
    log.Remark
FROM dbo.WMS_FG_HU_OperationLog log
INNER JOIN dbo.WMS_FG_HU hu ON hu.Id = log.HUId
LEFT JOIN dbo.WMS_FG_HU related ON related.Id = log.RelatedHUId
WHERE log.HUId = @HUId
ORDER BY log.OperationDt DESC, log.Id DESC;";

        public const string HoldLogsSql = @"
SELECT TOP (@Top)
    log.Id,
    log.HoldNo,
    hu.HUCode AS HuCode,
    log.HoldType,
    log.HoldReasonCode,
    log.HoldReason,
    log.SourceBizType,
    log.SourceBizId,
    log.SourceBizNo,
    log.IsActive,
    log.HoldDt AS HoldTime,
    log.HoldBy AS HoldUser,
    log.HoldRequestId,
    log.ReleaseDt AS ReleaseTime,
    log.ReleaseBy AS ReleaseUser,
    log.ReleaseReason,
    log.ReleaseRequestId
FROM dbo.WMS_FG_HU_HoldLog log
INNER JOIN dbo.WMS_FG_HU hu ON hu.Id = log.HUId
WHERE log.HUId = @HUId
ORDER BY log.HoldDt DESC, log.Id DESC;";

        private const string ActiveHoldSql = @"
SELECT TOP 1
    log.Id,
    log.HoldNo,
    hu.HUCode AS HuCode,
    log.HoldType,
    log.HoldReasonCode,
    log.HoldReason,
    log.SourceBizType,
    log.SourceBizId,
    log.SourceBizNo,
    log.IsActive,
    log.HoldDt AS HoldTime,
    log.HoldBy AS HoldUser,
    log.HoldRequestId,
    log.ReleaseDt AS ReleaseTime,
    log.ReleaseBy AS ReleaseUser,
    log.ReleaseReason,
    log.ReleaseRequestId
FROM dbo.WMS_FG_HU_HoldLog log
INNER JOIN dbo.WMS_FG_HU hu ON hu.Id = log.HUId
WHERE log.HUId = @HUId AND log.IsActive = 1
ORDER BY log.HoldDt DESC, log.Id DESC;";

        public List<HuDetailContentDto> GetContents(long huId, string huType)
        {
            string sql = string.Equals(huType, "PALLET", StringComparison.OrdinalIgnoreCase)
                ? PalletContentsSql
                : string.Equals(huType, "OUTER_BOX", StringComparison.OrdinalIgnoreCase)
                    ? OuterBoxContentsSql
                    : InnerPackContentsSql;
            List<HuDetailContentDto> items = new List<HuDetailContentDto>();
            using (SqlConnection connection = DbConnectionFactory.Create())
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add("@HUId", SqlDbType.BigInt).Value = huId;
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read()) items.Add(ReadContent(reader));
                }
            }
            return items;
        }

        public HuHoldLogDto GetActiveHold(long huId)
        {
            using (SqlConnection connection = DbConnectionFactory.Create())
            using (SqlCommand command = new SqlCommand(ActiveHoldSql, connection))
            {
                command.Parameters.Add("@HUId", SqlDbType.BigInt).Value = huId;
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    return reader.Read() ? ReadHold(reader) : null;
                }
            }
        }

        public List<HuOperationLogDto> GetOperationLogs(long huId, int top)
        {
            List<HuOperationLogDto> logs = new List<HuOperationLogDto>();
            using (SqlConnection connection = DbConnectionFactory.Create())
            using (SqlCommand command = new SqlCommand(OperationLogsSql, connection))
            {
                command.Parameters.Add("@HUId", SqlDbType.BigInt).Value = huId;
                command.Parameters.Add("@Top", SqlDbType.Int).Value = top;
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        logs.Add(new HuOperationLogDto
                        {
                            Id = Int64Value(reader, "Id"),
                            OperationNo = StringValue(reader, "OperationNo"),
                            OperationType = StringValue(reader, "OperationType"),
                            HuCode = StringValue(reader, "HuCode"),
                            RelatedHuCode = StringValue(reader, "RelatedHuCode"),
                            BusinessNo = StringValue(reader, "BusinessNo"),
                            RequestId = StringValue(reader, "RequestId"),
                            DetailJson = StringValue(reader, "DetailJson"),
                            Operator = StringValue(reader, "Operator"),
                            OperationTime = DateTimeValue(reader, "OperationTime"),
                            Remark = StringValue(reader, "Remark")
                        });
                    }
                }
            }
            return logs;
        }

        public List<HuHoldLogDto> GetHoldLogs(long huId, int top)
        {
            List<HuHoldLogDto> logs = new List<HuHoldLogDto>();
            using (SqlConnection connection = DbConnectionFactory.Create())
            using (SqlCommand command = new SqlCommand(HoldLogsSql, connection))
            {
                command.Parameters.Add("@HUId", SqlDbType.BigInt).Value = huId;
                command.Parameters.Add("@Top", SqlDbType.Int).Value = top;
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read()) logs.Add(ReadHold(reader));
                }
            }
            return logs;
        }

        private static HuDetailContentDto ReadContent(SqlDataReader reader)
        {
            return new HuDetailContentDto
            {
                HuId = Int64Value(reader, "HuId"),
                HuCode = StringValue(reader, "HuCode"),
                HuType = StringValue(reader, "HuType"),
                LifecycleStatus = StringValue(reader, "LifecycleStatus"),
                IsHold = BooleanValue(reader, "IsHold"),
                ItemId = NullableInt64(reader, "ItemId"),
                MaterialCode = StringValue(reader, "MaterialCode"),
                MaterialRevision = StringValue(reader, "MaterialRevision"),
                BatchNo = StringValue(reader, "BatchNo"),
                ProductionOrderNo = StringValue(reader, "ProductionOrderNo"),
                DateCode = StringValue(reader, "DateCode"),
                WeekFormatCode = StringValue(reader, "WeekFormatCode"),
                WeekYear = NullableInt16(reader, "WeekYear"),
                WeekNo = NullableByte(reader, "WeekNo"),
                WeekSortKey = NullableInt32(reader, "WeekSortKey"),
                WeekRuleSource = StringValue(reader, "WeekRuleSource"),
                WeekRuleValue = StringValue(reader, "WeekRuleValue"),
                Qty = DecimalValue(reader, "Qty"),
                Uom = StringValue(reader, "Uom"),
                StockStatus = StringValue(reader, "StockStatus"),
                CreatedTime = NullableDateTime(reader, "CreatedTime")
            };
        }

        private static HuHoldLogDto ReadHold(SqlDataReader reader)
        {
            return new HuHoldLogDto
            {
                Id = Int64Value(reader, "Id"),
                HoldNo = StringValue(reader, "HoldNo"),
                HuCode = StringValue(reader, "HuCode"),
                HoldType = StringValue(reader, "HoldType"),
                HoldReasonCode = StringValue(reader, "HoldReasonCode"),
                HoldReason = StringValue(reader, "HoldReason"),
                SourceBizType = StringValue(reader, "SourceBizType"),
                SourceBizId = NullableInt64(reader, "SourceBizId"),
                SourceBizNo = StringValue(reader, "SourceBizNo"),
                IsActive = BooleanValue(reader, "IsActive"),
                HoldTime = DateTimeValue(reader, "HoldTime"),
                HoldUser = StringValue(reader, "HoldUser"),
                HoldRequestId = StringValue(reader, "HoldRequestId"),
                ReleaseTime = NullableDateTime(reader, "ReleaseTime"),
                ReleaseUser = StringValue(reader, "ReleaseUser"),
                ReleaseReason = StringValue(reader, "ReleaseReason"),
                ReleaseRequestId = StringValue(reader, "ReleaseRequestId")
            };
        }

        private static string StringValue(SqlDataReader reader, string name) { int index = reader.GetOrdinal(name); return reader.IsDBNull(index) ? null : Convert.ToString(reader.GetValue(index)); }
        private static long Int64Value(SqlDataReader reader, string name) { return Convert.ToInt64(reader.GetValue(reader.GetOrdinal(name))); }
        private static long? NullableInt64(SqlDataReader reader, string name) { int index = reader.GetOrdinal(name); return reader.IsDBNull(index) ? (long?)null : Convert.ToInt64(reader.GetValue(index)); }
        private static int? NullableInt32(SqlDataReader reader, string name) { int index = reader.GetOrdinal(name); return reader.IsDBNull(index) ? (int?)null : Convert.ToInt32(reader.GetValue(index)); }
        private static short? NullableInt16(SqlDataReader reader, string name) { int index = reader.GetOrdinal(name); return reader.IsDBNull(index) ? (short?)null : Convert.ToInt16(reader.GetValue(index)); }
        private static byte? NullableByte(SqlDataReader reader, string name) { int index = reader.GetOrdinal(name); return reader.IsDBNull(index) ? (byte?)null : Convert.ToByte(reader.GetValue(index)); }
        private static decimal DecimalValue(SqlDataReader reader, string name) { return Convert.ToDecimal(reader.GetValue(reader.GetOrdinal(name))); }
        private static bool BooleanValue(SqlDataReader reader, string name) { return Convert.ToBoolean(reader.GetValue(reader.GetOrdinal(name))); }
        private static DateTime DateTimeValue(SqlDataReader reader, string name) { return Convert.ToDateTime(reader.GetValue(reader.GetOrdinal(name))); }
        private static DateTime? NullableDateTime(SqlDataReader reader, string name) { int index = reader.GetOrdinal(name); return reader.IsDBNull(index) ? (DateTime?)null : Convert.ToDateTime(reader.GetValue(index)); }
    }
}
