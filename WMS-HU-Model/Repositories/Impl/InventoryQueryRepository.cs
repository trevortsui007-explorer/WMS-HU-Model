using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Text;
using WMS_HU_Model.Infrastructure.Database;
using WMS_HU_Model.Models.HU;
using WMS_HU_Model.Models.Inventory;

namespace WMS_HU_Model.Repositories
{
    public class InventoryQueryRepository : IInventoryQueryRepository
    {
        public const string LocationInventorySql = @"
SELECT
    box.Id AS BoxHuId,
    box.HUCode AS BoxCode,
    box.LifecycleStatus AS BoxStatus,
    box.IsHold AS BoxIsHold,
    pack.Id AS PackHuId,
    pack.HUCode AS PackCode,
    item.MaterialCode,
    item.MaterialRevision,
    item.BatchNo,
    item.WeekYear,
    item.WeekNo,
    item.WeekSortKey,
    item.Qty
FROM dbo.WMS_HU box
INNER JOIN dbo.WMS_HU_Relation relation ON relation.ParentHUId = box.Id AND relation.IsActive = 1
INNER JOIN dbo.WMS_HU pack ON pack.Id = relation.ChildHUId
    AND pack.HUType = 'INNER_PACK'
    AND pack.LifecycleStatus = 'ACTIVE'
INNER JOIN dbo.WMS_HU_Item item ON item.HUId = pack.Id AND item.IsActive = 1
WHERE box.Id IN ({HU_IDS})
  AND box.HUType = 'OUTER_BOX'
  AND box.LifecycleStatus = 'ACTIVE'
ORDER BY box.HUCode, pack.HUCode, item.Id;";

        public const string OuterBoxFactsSql = @"
SELECT
    box.Id AS BoxHuId,
    box.HUCode AS BoxCode,
    box.LifecycleStatus AS BoxStatus,
    box.IsHold AS BoxIsHold,
    pack.Id AS PackHuId,
    pack.HUCode AS PackCode,
    item.MaterialCode,
    item.MaterialRevision,
    item.BatchNo,
    item.WeekYear,
    item.WeekNo,
    item.WeekSortKey,
    item.Qty
FROM dbo.WMS_HU box
INNER JOIN dbo.WMS_HU_Relation relation ON relation.ParentHUId = box.Id AND relation.IsActive = 1
INNER JOIN dbo.WMS_HU pack ON pack.Id = relation.ChildHUId
    AND pack.HUType = 'INNER_PACK'
    AND pack.LifecycleStatus = 'ACTIVE'
INNER JOIN dbo.WMS_HU_Item item ON item.HUId = pack.Id AND item.IsActive = 1
WHERE box.Id = @BoxHUId
  AND box.HUType = 'OUTER_BOX'
ORDER BY pack.HUCode, item.Id;";

        public const string InnerPackFactsSql = @"
SELECT
    CAST(0 AS BIGINT) AS BoxHuId,
    CAST(NULL AS VARCHAR(100)) AS BoxCode,
    CAST(NULL AS VARCHAR(20)) AS BoxStatus,
    CAST(0 AS BIT) AS BoxIsHold,
    pack.Id AS PackHuId,
    pack.HUCode AS PackCode,
    item.MaterialCode,
    item.MaterialRevision,
    item.BatchNo,
    item.WeekYear,
    item.WeekNo,
    item.WeekSortKey,
    item.Qty
FROM dbo.WMS_HU pack
INNER JOIN dbo.WMS_HU_Item item ON item.HUId = pack.Id AND item.IsActive = 1
WHERE pack.Id = @PackHUId
  AND pack.HUType = 'INNER_PACK'
ORDER BY item.Id;";

        public InventoryLookupLocationRecord GetLocation(string warehouseCode, string locationCode)
        {
            const string sql = @"
SELECT TOP 1 location.Id, warehouse.WarehouseCode, zone.ZoneCode, location.LocationCode
FROM dbo.WMS_Location location
INNER JOIN dbo.WMS_Warehouse warehouse ON warehouse.Id = location.WarehouseId
INNER JOIN dbo.WMS_Zone zone ON zone.Id = location.ZoneId
WHERE warehouse.WarehouseCode = @WarehouseCode
  AND location.LocationCode = @LocationCode;";
            return QueryLocation(sql, warehouseCode, locationCode, true);
        }

        public InventoryLookupLocationRecord FindLocation(string locationCode)
        {
            const string sql = @"
SELECT TOP 1 location.Id, warehouse.WarehouseCode, zone.ZoneCode, location.LocationCode
FROM dbo.WMS_Location location
INNER JOIN dbo.WMS_Warehouse warehouse ON warehouse.Id = location.WarehouseId
INNER JOIN dbo.WMS_Zone zone ON zone.Id = location.ZoneId
WHERE location.LocationCode = @LocationCode
ORDER BY warehouse.WarehouseCode;";
            return QueryLocation(sql, null, locationCode, false);
        }

        public HuDto GetHu(long huId)
        {
            const string sql = @"
SELECT Id, HUCode, HUType, PlantCode, LifecycleStatus, IsHold, HoldReason,
    SourceSystem, SourceEntityId, SourceBusinessNo, Remark, CreateTime
FROM dbo.WMS_HU
WHERE Id = @HUId;";

            using (SqlConnection connection = DbConnectionFactory.Create())
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add("@HUId", SqlDbType.BigInt).Value = huId;
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read()) return null;
                    return new HuDto
                    {
                        Id = Convert.ToInt64(reader["Id"]),
                        HuCode = DbString(reader, "HUCode"),
                        HuType = DbString(reader, "HUType"),
                        PlantCode = DbString(reader, "PlantCode"),
                        LifecycleStatus = DbString(reader, "LifecycleStatus"),
                        IsHold = Convert.ToBoolean(reader["IsHold"]),
                        HoldReason = DbString(reader, "HoldReason"),
                        SourceSystem = DbString(reader, "SourceSystem"),
                        SourceEntityId = DbString(reader, "SourceEntityId"),
                        SourceBusinessNo = DbString(reader, "SourceBusinessNo"),
                        Remark = DbString(reader, "Remark"),
                        CreateTime = Convert.ToDateTime(reader["CreateTime"])
                    };
                }
            }
        }

        public List<InventoryItemFact> GetLocationInventoryFacts(IList<long> boxHuIds)
        {
            List<long> ids = DistinctIds(boxHuIds);
            if (ids.Count == 0) return new List<InventoryItemFact>();

            List<InventoryItemFact> results = new List<InventoryItemFact>();
            for (int offset = 0; offset < ids.Count; offset += 1000)
                results.AddRange(GetLocationInventoryFactsBatch(ids.GetRange(offset, Math.Min(1000, ids.Count - offset))));
            return results;
        }

        private static List<InventoryItemFact> GetLocationInventoryFactsBatch(List<long> ids)
        {

            StringBuilder parameterNames = new StringBuilder();
            for (int index = 0; index < ids.Count; index++)
            {
                if (index > 0) parameterNames.Append(",");
                parameterNames.Append("@BoxHUId").Append(index);
            }

            string sql = LocationInventorySql.Replace("{HU_IDS}", parameterNames.ToString());
            using (SqlConnection connection = DbConnectionFactory.Create())
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                for (int index = 0; index < ids.Count; index++)
                    command.Parameters.Add("@BoxHUId" + index, SqlDbType.BigInt).Value = ids[index];
                return ReadFacts(connection, command);
            }
        }

        public List<InventoryItemFact> GetOuterBoxFacts(long boxHuId)
        {
            using (SqlConnection connection = DbConnectionFactory.Create())
            using (SqlCommand command = new SqlCommand(OuterBoxFactsSql, connection))
            {
                command.Parameters.Add("@BoxHUId", SqlDbType.BigInt).Value = boxHuId;
                return ReadFacts(connection, command);
            }
        }

        public List<InventoryItemFact> GetInnerPackFacts(long packHuId)
        {
            using (SqlConnection connection = DbConnectionFactory.Create())
            using (SqlCommand command = new SqlCommand(InnerPackFactsSql, connection))
            {
                command.Parameters.Add("@PackHUId", SqlDbType.BigInt).Value = packHuId;
                return ReadFacts(connection, command);
            }
        }

        public string GetActiveParentBoxCode(long packHuId)
        {
            const string sql = @"
SELECT TOP 1 parent.HUCode
FROM dbo.WMS_HU_Relation relation
INNER JOIN dbo.WMS_HU parent ON parent.Id = relation.ParentHUId
    AND parent.HUType = 'OUTER_BOX'
    AND parent.LifecycleStatus = 'ACTIVE'
WHERE relation.ChildHUId = @PackHUId
  AND relation.IsActive = 1
ORDER BY relation.Id DESC;";

            using (SqlConnection connection = DbConnectionFactory.Create())
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add("@PackHUId", SqlDbType.BigInt).Value = packHuId;
                connection.Open();
                object value = command.ExecuteScalar();
                return value == null || value == DBNull.Value ? null : Convert.ToString(value);
            }
        }

        private static InventoryLookupLocationRecord QueryLocation(string sql, string warehouseCode, string locationCode, bool includeWarehouse)
        {
            using (SqlConnection connection = DbConnectionFactory.Create())
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                if (includeWarehouse)
                    command.Parameters.Add("@WarehouseCode", SqlDbType.VarChar, 30).Value = warehouseCode;
                command.Parameters.Add("@LocationCode", SqlDbType.VarChar, 50).Value = locationCode;
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read()) return null;
                    return new InventoryLookupLocationRecord
                    {
                        Id = Convert.ToInt64(reader["Id"]),
                        WarehouseCode = DbString(reader, "WarehouseCode"),
                        ZoneCode = DbString(reader, "ZoneCode"),
                        LocationCode = DbString(reader, "LocationCode")
                    };
                }
            }
        }

        private static List<InventoryItemFact> ReadFacts(SqlConnection connection, SqlCommand command)
        {
            List<InventoryItemFact> results = new List<InventoryItemFact>();
            connection.Open();
            using (SqlDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    results.Add(new InventoryItemFact
                    {
                        BoxHuId = Convert.ToInt64(reader["BoxHuId"]),
                        BoxCode = DbString(reader, "BoxCode"),
                        BoxStatus = DbString(reader, "BoxStatus"),
                        BoxIsHold = Convert.ToBoolean(reader["BoxIsHold"]),
                        PackHuId = Convert.ToInt64(reader["PackHuId"]),
                        PackCode = DbString(reader, "PackCode"),
                        MaterialCode = DbString(reader, "MaterialCode"),
                        MaterialRevision = DbString(reader, "MaterialRevision"),
                        BatchNo = DbString(reader, "BatchNo"),
                        WeekYear = DbInt16(reader, "WeekYear"),
                        WeekNo = DbByte(reader, "WeekNo"),
                        WeekSortKey = DbInt32(reader, "WeekSortKey"),
                        Qty = Convert.ToDecimal(reader["Qty"])
                    });
                }
            }
            return results;
        }

        private static List<long> DistinctIds(IList<long> values)
        {
            List<long> results = new List<long>();
            HashSet<long> seen = new HashSet<long>();
            foreach (long value in values ?? new List<long>())
            {
                if (seen.Add(value)) results.Add(value);
            }
            return results;
        }

        private static string DbString(SqlDataReader reader, string name)
        {
            int index = reader.GetOrdinal(name);
            return reader.IsDBNull(index) ? null : Convert.ToString(reader.GetValue(index));
        }

        private static short? DbInt16(SqlDataReader reader, string name)
        {
            int index = reader.GetOrdinal(name);
            return reader.IsDBNull(index) ? (short?)null : Convert.ToInt16(reader.GetValue(index));
        }

        private static byte? DbByte(SqlDataReader reader, string name)
        {
            int index = reader.GetOrdinal(name);
            return reader.IsDBNull(index) ? (byte?)null : Convert.ToByte(reader.GetValue(index));
        }

        private static int? DbInt32(SqlDataReader reader, string name)
        {
            int index = reader.GetOrdinal(name);
            return reader.IsDBNull(index) ? (int?)null : Convert.ToInt32(reader.GetValue(index));
        }
    }
}
