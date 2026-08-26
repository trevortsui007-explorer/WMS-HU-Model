using System;
using System.Data;
using System.Data.SqlClient;
using WMS_HU_Model.Infrastructure.Database;
using WMS_HU_Model.Models.Putaway;

namespace WMS_HU_Model.Repositories
{
    public class PutawayRecommendationRepository : IPutawayRecommendationRepository
    {
        private const string BoxSqlPrefix = @"
;WITH Ancestors AS
(
    SELECT box.Id AS HUId, box.HUCode, box.HUType, CAST(0 AS INT) AS LevelNo
    FROM dbo.WMS_HU box
    WHERE box.HUCode = @BoxHUCode

    UNION ALL

    SELECT parent.Id, parent.HUCode, parent.HUType, ancestor.LevelNo + 1
    FROM Ancestors ancestor
    INNER JOIN dbo.WMS_HU_Relation relation
        ON relation.ChildHUId = ancestor.HUId
       AND relation.IsActive = 1
    INNER JOIN dbo.WMS_HU parent
        ON parent.Id = relation.ParentHUId
       AND ";

        private const string BoxSqlSuffix = @"
)
SELECT
    box.Id AS HuId,
    box.HUCode,
    box.HUType,
    box.PlantCode,
    box.LifecycleStatus,
    box.IsHold,
    effective.LocationId AS EffectiveLocationId,
    effective.LocationCode AS EffectiveLocationCode,
    effective.WarehouseCode AS EffectiveWarehouseCode,
    effective.ZoneCode AS EffectiveZoneCode,
    effective.SourceHUId,
    effective.SourceHUType
FROM dbo.WMS_HU box
OUTER APPLY
(
    SELECT TOP 1
        location.Id AS LocationId,
        location.LocationCode,
        warehouse.WarehouseCode,
        zone.ZoneCode,
        ancestor.HUId AS SourceHUId,
        ancestor.HUType AS SourceHUType
    FROM Ancestors ancestor
    INNER JOIN dbo.WMS_HU_CurrentLocation currentLocation ON currentLocation.HUId = ancestor.HUId
    INNER JOIN dbo.WMS_Location location ON location.Id = currentLocation.LocationId
    INNER JOIN dbo.WMS_Warehouse warehouse ON warehouse.Id = location.WarehouseId
    INNER JOIN dbo.WMS_Zone zone ON zone.Id = location.ZoneId
    ORDER BY ancestor.LevelNo, ancestor.HUId
) effective
WHERE box.HUCode = @BoxHUCode
OPTION (MAXRECURSION 20);";

        private const string IncomingItemsSql = @"
SELECT item.MaterialCode, item.MaterialRevision, item.WeekYear, item.WeekNo, item.WeekSortKey
FROM dbo.WMS_HU_Relation relation
INNER JOIN dbo.WMS_HU pack
    ON pack.Id = relation.ChildHUId
   AND pack.HUType = 'INNER_PACK'
   AND pack.LifecycleStatus = 'ACTIVE'
INNER JOIN dbo.WMS_HU_Item item ON item.HUId = pack.Id AND item.IsActive = 1
WHERE relation.ParentHUId = @BoxHUId
  AND relation.IsActive = 1
ORDER BY pack.HUCode, item.Id;";

        public const string LegalLocationsSql = @"
SELECT
    location.Id AS LocationId,
    location.LocationCode,
    location.PlantPolicy,
    location.DedicatedPlantCode,
    location.AllowMixedMaterial,
    location.PutawayPriority,
    location.PhysicalRackGroupCode,
    location.PhysicalRowCode,
    location.PositionSequence
FROM dbo.WMS_Location location
INNER JOIN dbo.WMS_Warehouse warehouse ON warehouse.Id = location.WarehouseId
INNER JOIN dbo.WMS_Zone zone ON zone.Id = location.ZoneId
WHERE warehouse.WarehouseCode = @WarehouseCode
  AND warehouse.IsActive = 1
  AND zone.ZoneCode = 'STORAGE'
  AND location.MasterStatus = 'ACTIVE'
  AND location.IsVirtual = 0
  AND location.IsScannable = 1
ORDER BY location.PutawayPriority, location.LocationCode;";

        private const string InventorySqlPrefix = @"
;WITH Roots AS
(
    SELECT hu.Id AS RootHUId, hu.Id AS HUId, hu.HUCode, hu.HUType, CAST(0 AS INT) AS LevelNo
    FROM dbo.WMS_HU hu
    WHERE hu.HUType = 'OUTER_BOX'
      AND hu.LifecycleStatus = 'ACTIVE'
),
Ancestors AS
(
    SELECT RootHUId, HUId, HUCode, HUType, LevelNo
    FROM Roots

    UNION ALL

    SELECT ancestor.RootHUId, parent.Id, parent.HUCode, parent.HUType, ancestor.LevelNo + 1
    FROM Ancestors ancestor
    INNER JOIN dbo.WMS_HU_Relation relation
        ON relation.ChildHUId = ancestor.HUId
       AND relation.IsActive = 1
    INNER JOIN dbo.WMS_HU parent
        ON parent.Id = relation.ParentHUId
       AND ";

        private const string InventorySqlSuffix = @"
),
RankedLocations AS
(
    SELECT
        ancestor.RootHUId,
        currentLocation.LocationId,
        ROW_NUMBER() OVER
        (
            PARTITION BY ancestor.RootHUId
            ORDER BY ancestor.LevelNo, ancestor.HUId
        ) AS LocationRank
    FROM Ancestors ancestor
    INNER JOIN dbo.WMS_HU_CurrentLocation currentLocation ON currentLocation.HUId = ancestor.HUId
)
SELECT
    box.Id AS BoxHuId,
    box.HUCode AS BoxHuCode,
    box.PlantCode,
    resolved.LocationId,
    item.MaterialCode,
    item.MaterialRevision,
    item.WeekYear,
    item.WeekNo,
    item.WeekSortKey
FROM RankedLocations resolved
INNER JOIN dbo.WMS_HU box ON box.Id = resolved.RootHUId
INNER JOIN dbo.WMS_Location location ON location.Id = resolved.LocationId
INNER JOIN dbo.WMS_Warehouse warehouse ON warehouse.Id = location.WarehouseId
INNER JOIN dbo.WMS_Zone zone ON zone.Id = location.ZoneId
LEFT JOIN dbo.WMS_HU_Relation relation
    ON relation.ParentHUId = box.Id
   AND relation.IsActive = 1
LEFT JOIN dbo.WMS_HU pack
    ON pack.Id = relation.ChildHUId
   AND pack.HUType = 'INNER_PACK'
   AND pack.LifecycleStatus = 'ACTIVE'
LEFT JOIN dbo.WMS_HU_Item item
    ON item.HUId = pack.Id
   AND item.IsActive = 1
WHERE resolved.LocationRank = 1
  AND warehouse.WarehouseCode = @WarehouseCode
  AND zone.ZoneCode = 'STORAGE'
ORDER BY resolved.LocationId, box.Id, item.Id
OPTION (MAXRECURSION 20);";

        public PutawayRecommendationSnapshot LoadSnapshot(string warehouseCode, string boxHuCode)
        {
            PutawayRecommendationSnapshot snapshot = new PutawayRecommendationSnapshot();
            using (SqlConnection connection = DbConnectionFactory.Create())
            {
                connection.Open();
                snapshot.Box = ReadBox(connection, boxHuCode);
                if (snapshot.Box == null) return snapshot;

                ReadIncomingItems(connection, snapshot.Box.HuId, snapshot);
                snapshot.WarehouseAllowsPlant = ReadWarehousePlantPolicy(connection, warehouseCode, snapshot.Box.PlantCode);
                ReadLocations(connection, warehouseCode, snapshot);
                ReadInventory(connection, warehouseCode, snapshot);
            }
            return snapshot;
        }

        private static PutawayRecommendationBoxSnapshot ReadBox(SqlConnection connection, string boxHuCode)
        {
            string sql = BoxSqlPrefix + EffectiveLocationResolver.ActiveParentPredicateSql + BoxSqlSuffix;
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add("@BoxHUCode", SqlDbType.VarChar, 100).Value = boxHuCode;
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read()) return null;
                    long huId = Convert.ToInt64(reader["HuId"]);
                    long? sourceHuId = NullableInt64(reader, "SourceHUId");
                    string sourceHuType = StringValue(reader, "SourceHUType");
                    return new PutawayRecommendationBoxSnapshot
                    {
                        HuId = huId,
                        HuCode = StringValue(reader, "HUCode"),
                        HuType = StringValue(reader, "HUType"),
                        PlantCode = StringValue(reader, "PlantCode"),
                        LifecycleStatus = StringValue(reader, "LifecycleStatus"),
                        IsHold = Convert.ToBoolean(reader["IsHold"]),
                        EffectiveLocationId = NullableInt64(reader, "EffectiveLocationId"),
                        EffectiveLocationCode = StringValue(reader, "EffectiveLocationCode"),
                        EffectiveWarehouseCode = StringValue(reader, "EffectiveWarehouseCode"),
                        EffectiveZoneCode = StringValue(reader, "EffectiveZoneCode"),
                        LocationSource = LocationSource(huId, sourceHuId, sourceHuType)
                    };
                }
            }
        }

        private static void ReadIncomingItems(SqlConnection connection, long boxHuId, PutawayRecommendationSnapshot snapshot)
        {
            using (SqlCommand command = new SqlCommand(IncomingItemsSql, connection))
            {
                command.Parameters.Add("@BoxHUId", SqlDbType.BigInt).Value = boxHuId;
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        snapshot.IncomingItems.Add(new PutawayRecommendationItemSnapshot
                        {
                            MaterialCode = StringValue(reader, "MaterialCode"),
                            MaterialRevision = StringValue(reader, "MaterialRevision"),
                            WeekYear = NullableInt16(reader, "WeekYear"),
                            WeekNo = NullableByte(reader, "WeekNo"),
                            WeekSortKey = NullableInt32(reader, "WeekSortKey")
                        });
                    }
                }
            }
        }

        private static bool ReadWarehousePlantPolicy(SqlConnection connection, string warehouseCode, string plantCode)
        {
            const string sql = @"
SELECT COUNT(1)
FROM dbo.WMS_WarehousePlant warehousePlant
INNER JOIN dbo.WMS_Warehouse warehouse ON warehouse.Id = warehousePlant.WarehouseId
WHERE warehouse.WarehouseCode = @WarehouseCode
  AND warehouse.IsActive = 1
  AND warehousePlant.PlantCode = @PlantCode
  AND warehousePlant.IsActive = 1;";
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add("@WarehouseCode", SqlDbType.VarChar, 30).Value = warehouseCode;
                command.Parameters.Add("@PlantCode", SqlDbType.VarChar, 20).Value = plantCode;
                return Convert.ToInt32(command.ExecuteScalar()) > 0;
            }
        }

        private static void ReadLocations(SqlConnection connection, string warehouseCode, PutawayRecommendationSnapshot snapshot)
        {
            using (SqlCommand command = new SqlCommand(LegalLocationsSql, connection))
            {
                command.Parameters.Add("@WarehouseCode", SqlDbType.VarChar, 30).Value = warehouseCode;
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        snapshot.Locations.Add(new PutawayRecommendationLocationSnapshot
                        {
                            LocationId = Convert.ToInt64(reader["LocationId"]),
                            LocationCode = StringValue(reader, "LocationCode"),
                            PlantPolicy = StringValue(reader, "PlantPolicy"),
                            DedicatedPlantCode = StringValue(reader, "DedicatedPlantCode"),
                            AllowMixedMaterial = Convert.ToBoolean(reader["AllowMixedMaterial"]),
                            PutawayPriority = Convert.ToInt32(reader["PutawayPriority"]),
                            PhysicalRackGroupCode = StringValue(reader, "PhysicalRackGroupCode"),
                            PhysicalRowCode = StringValue(reader, "PhysicalRowCode"),
                            PositionSequence = NullableInt32(reader, "PositionSequence")
                        });
                    }
                }
            }
        }

        private static void ReadInventory(SqlConnection connection, string warehouseCode, PutawayRecommendationSnapshot snapshot)
        {
            string sql = InventorySqlPrefix + EffectiveLocationResolver.ActiveParentPredicateSql + InventorySqlSuffix;
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add("@WarehouseCode", SqlDbType.VarChar, 30).Value = warehouseCode;
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        snapshot.Inventory.Add(new PutawayRecommendationInventorySnapshot
                        {
                            BoxHuId = Convert.ToInt64(reader["BoxHuId"]),
                            BoxHuCode = StringValue(reader, "BoxHuCode"),
                            PlantCode = StringValue(reader, "PlantCode"),
                            LocationId = Convert.ToInt64(reader["LocationId"]),
                            MaterialCode = StringValue(reader, "MaterialCode"),
                            MaterialRevision = StringValue(reader, "MaterialRevision"),
                            WeekYear = NullableInt16(reader, "WeekYear"),
                            WeekNo = NullableByte(reader, "WeekNo"),
                            WeekSortKey = NullableInt32(reader, "WeekSortKey")
                        });
                    }
                }
            }
        }

        private static string LocationSource(long huId, long? sourceHuId, string sourceHuType)
        {
            if (!sourceHuId.HasValue) return string.Empty;
            if (sourceHuId.Value == huId) return "SELF";
            return string.Equals(sourceHuType, "PALLET", StringComparison.OrdinalIgnoreCase) ? "PARENT_PALLET" : string.Empty;
        }

        private static string StringValue(SqlDataReader reader, string name)
        {
            int index = reader.GetOrdinal(name);
            return reader.IsDBNull(index) ? null : Convert.ToString(reader.GetValue(index));
        }

        private static long? NullableInt64(SqlDataReader reader, string name)
        {
            int index = reader.GetOrdinal(name);
            return reader.IsDBNull(index) ? (long?)null : Convert.ToInt64(reader.GetValue(index));
        }

        private static short? NullableInt16(SqlDataReader reader, string name)
        {
            int index = reader.GetOrdinal(name);
            return reader.IsDBNull(index) ? (short?)null : Convert.ToInt16(reader.GetValue(index));
        }

        private static byte? NullableByte(SqlDataReader reader, string name)
        {
            int index = reader.GetOrdinal(name);
            return reader.IsDBNull(index) ? (byte?)null : Convert.ToByte(reader.GetValue(index));
        }

        private static int? NullableInt32(SqlDataReader reader, string name)
        {
            int index = reader.GetOrdinal(name);
            return reader.IsDBNull(index) ? (int?)null : Convert.ToInt32(reader.GetValue(index));
        }
    }
}
