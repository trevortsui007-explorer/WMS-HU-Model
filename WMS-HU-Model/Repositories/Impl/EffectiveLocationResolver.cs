using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using WMS_HU_Model.Infrastructure.Database;
using WMS_HU_Model.Models.HU;

namespace WMS_HU_Model.Repositories
{
    public class EffectiveLocationResolver : IEffectiveLocationResolver
    {
        public const string ActiveParentPredicateSql = "parent.LifecycleStatus = 'ACTIVE'";

        // The shared CTE is intentionally kept with the resolver: it is the canonical
        // SQL definition used where server-side filtering/paging must remain set based.
        internal const string AncestorAndRankedLocationCteSql = @"
, Ancestors AS
(
    SELECT RootHUId, HUId, HUCode, HUType, LevelNo FROM Roots
    UNION ALL
    SELECT ancestor.RootHUId, parent.Id, parent.HUCode, parent.HUType, ancestor.LevelNo + 1
    FROM Ancestors ancestor
    INNER JOIN dbo.WMS_FG_HU_Relation relation ON relation.ChildHUId = ancestor.HUId AND relation.IsActive = 1
    INNER JOIN dbo.WMS_FG_HU parent ON parent.Id = relation.ParentHUId AND parent.LifecycleStatus = 'ACTIVE'
)
, RankedEffectiveLocations AS
(
    SELECT ancestor.RootHUId, currentLocation.LocationId,
        ROW_NUMBER() OVER (PARTITION BY ancestor.RootHUId ORDER BY ancestor.LevelNo, ancestor.HUId) AS LocationRank
    FROM Ancestors ancestor
    INNER JOIN dbo.WMS_FG_HU_CurrentLocation currentLocation ON currentLocation.HUId = ancestor.HUId
)";

        public HuLocationDto Resolve(long huId)
        {
            return ResolveMany(new List<long> { huId }).FirstOrDefault();
        }

        public List<HuLocationDto> ResolveMany(IList<long> huIds)
        {
            List<long> ids = (huIds ?? new List<long>()).Distinct().ToList();
            List<HuLocationDto> results = new List<HuLocationDto>();
            if (ids.Count == 0) return results;

            for (int offset = 0; offset < ids.Count; offset += 1000)
                results.AddRange(ResolveBatch(ids.GetRange(offset, Math.Min(1000, ids.Count - offset))));
            return results;
        }

        private static List<HuLocationDto> ResolveBatch(List<long> ids)
        {
            List<HuLocationDto> results = new List<HuLocationDto>();

            StringBuilder parameterNames = new StringBuilder();
            for (int index = 0; index < ids.Count; index++)
            {
                if (index > 0) parameterNames.Append(",");
                parameterNames.Append("@HUId").Append(index);
            }

            string sql = @"
;WITH Roots AS
(
    SELECT h.Id AS RootHUId, h.Id AS HUId, h.HUCode, h.HUType, CAST(0 AS INT) AS LevelNo
    FROM dbo.WMS_FG_HU h
    WHERE h.Id IN (" + parameterNames + @")
)
" + AncestorAndRankedLocationCteSql + @"
SELECT
    hu.Id AS HuId,
    hu.HUCode,
    hu.HUType,
    hu.PlantCode,
    directLocation.Id AS DirectLocationId,
    directLocation.LocationCode AS DirectLocationCode,
    directLocation.LocationName AS DirectLocationName,
    directWarehouse.WarehouseCode AS DirectWarehouseCode,
    directZone.ZoneCode AS DirectZoneCode,
    directLocation.LocationTypeCode AS DirectLocationTypeCode,
    directCurrent.LocationSince AS DirectLocationSince,
    directCurrent.LastMoveLogId,
    directMove.MoveNo AS LastMoveNo,
    directMove.MoveType AS LastMoveType,
    effective.LocationId AS EffectiveLocationId,
    effective.LocationCode AS EffectiveLocationCode,
    effective.LocationName AS EffectiveLocationName,
    effective.WarehouseCode AS EffectiveWarehouseCode,
    effective.ZoneCode AS EffectiveZoneCode,
    effective.LocationTypeCode AS EffectiveLocationTypeCode,
    effective.SourceHUId AS LocationSourceHuId,
    effective.SourceHUCode AS LocationSourceHuCode,
    effective.SourceHUType AS LocationSourceHuType,
    CAST(CASE WHEN effective.SourceHUId IS NULL OR effective.SourceHUId = hu.Id THEN 0 ELSE 1 END AS BIT) AS IsInherited
FROM dbo.WMS_FG_HU hu
LEFT JOIN dbo.WMS_FG_HU_CurrentLocation directCurrent ON directCurrent.HUId = hu.Id
LEFT JOIN dbo.WMS_FG_Location directLocation ON directLocation.Id = directCurrent.LocationId
LEFT JOIN dbo.WMS_FG_HU_MoveLog directMove ON directMove.Id = directCurrent.LastMoveLogId
LEFT JOIN dbo.WMS_FG_Warehouse directWarehouse ON directWarehouse.Id = directLocation.WarehouseId
LEFT JOIN dbo.WMS_FG_Zone directZone ON directZone.Id = directLocation.ZoneId
OUTER APPLY
(
    SELECT TOP 1
        location.Id AS LocationId,
        location.LocationCode,
        location.LocationName,
        location.LocationTypeCode,
        warehouse.WarehouseCode,
        zone.ZoneCode,
        ancestor.HUId AS SourceHUId,
        ancestor.HUCode AS SourceHUCode,
        ancestor.HUType AS SourceHUType
    FROM Ancestors ancestor
    INNER JOIN dbo.WMS_FG_HU_CurrentLocation currentLocation ON currentLocation.HUId = ancestor.HUId
    INNER JOIN dbo.WMS_FG_Location location ON location.Id = currentLocation.LocationId
    INNER JOIN dbo.WMS_FG_Warehouse warehouse ON warehouse.Id = location.WarehouseId
    INNER JOIN dbo.WMS_FG_Zone zone ON zone.Id = location.ZoneId
    WHERE ancestor.RootHUId = hu.Id
    ORDER BY ancestor.LevelNo
) effective
WHERE hu.Id IN (" + parameterNames + @")
OPTION (MAXRECURSION 20);";

            using (SqlConnection connection = DbConnectionFactory.Create())
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                for (int index = 0; index < ids.Count; index++)
                    command.Parameters.Add("@HUId" + index, SqlDbType.BigInt).Value = ids[index];

                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read()) results.Add(Map(reader));
                }
            }

            return results;
        }

        public List<long> FindHuIdsAtLocation(long locationId, string huType)
        {
            const string sqlPrefix = @"
;WITH Roots AS
(
    SELECT h.Id AS RootHUId, h.Id AS HUId, h.HUCode, h.HUType, CAST(0 AS INT) AS LevelNo
    FROM dbo.WMS_FG_HU h
    WHERE h.HUType = @HUType
)
";
            const string sqlSuffix = @"
SELECT RootHUId
FROM RankedEffectiveLocations
WHERE LocationRank = 1 AND LocationId = @LocationId
ORDER BY RootHUId
OPTION (MAXRECURSION 20);";
            string sql = sqlPrefix + AncestorAndRankedLocationCteSql + sqlSuffix;
            List<long> results = new List<long>();

            using (SqlConnection connection = DbConnectionFactory.Create())
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add("@LocationId", SqlDbType.BigInt).Value = locationId;
                command.Parameters.Add("@HUType", SqlDbType.VarChar, 30).Value = huType;
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read()) results.Add(Convert.ToInt64(reader["RootHUId"]));
                }
            }

            return results;
        }

        private static HuLocationDto Map(SqlDataReader reader)
        {
            HuLocationDto result = new HuLocationDto
            {
                HuId = Convert.ToInt64(reader["HuId"]),
                HuCode = DbString(reader, "HUCode"),
                HuType = DbString(reader, "HUType"),
                PlantCode = DbString(reader, "PlantCode"),
                DirectLocationId = DbInt64(reader, "DirectLocationId"),
                DirectLocationCode = DbString(reader, "DirectLocationCode"),
                DirectLocationName = DbString(reader, "DirectLocationName"),
                DirectWarehouseCode = DbString(reader, "DirectWarehouseCode"),
                DirectZoneCode = DbString(reader, "DirectZoneCode"),
                DirectLocationTypeCode = DbString(reader, "DirectLocationTypeCode"),
                DirectLocationSince = DbDateTime(reader, "DirectLocationSince"),
                EffectiveLocationId = DbInt64(reader, "EffectiveLocationId"),
                EffectiveLocationCode = DbString(reader, "EffectiveLocationCode"),
                EffectiveLocationName = DbString(reader, "EffectiveLocationName"),
                EffectiveWarehouseCode = DbString(reader, "EffectiveWarehouseCode"),
                EffectiveZoneCode = DbString(reader, "EffectiveZoneCode"),
                EffectiveLocationTypeCode = DbString(reader, "EffectiveLocationTypeCode"),
                LocationSourceHuId = DbInt64(reader, "LocationSourceHuId"),
                LocationSourceHuCode = DbString(reader, "LocationSourceHuCode"),
                LocationSourceHuType = DbString(reader, "LocationSourceHuType"),
                IsInherited = Convert.ToBoolean(reader["IsInherited"]),
                LastMoveLogId = DbInt64(reader, "LastMoveLogId"),
                LastMoveNo = DbString(reader, "LastMoveNo"),
                LastMoveType = DbString(reader, "LastMoveType")
            };
            result.LocationSource = Source(result.HuId, result.LocationSourceHuId, result.LocationSourceHuType);
            return result;
        }

        private static string Source(long huId, long? sourceHuId, string sourceHuType)
        {
            if (!sourceHuId.HasValue) return string.Empty;
            if (sourceHuId.Value == huId) return "SELF";
            if (string.Equals(sourceHuType, "OUTER_BOX", StringComparison.OrdinalIgnoreCase)) return "PARENT_BOX";
            if (string.Equals(sourceHuType, "PALLET", StringComparison.OrdinalIgnoreCase)) return "PARENT_PALLET";
            return string.Empty;
        }

        private static string DbString(SqlDataReader reader, string name)
        {
            int index = reader.GetOrdinal(name);
            return reader.IsDBNull(index) ? null : Convert.ToString(reader.GetValue(index));
        }

        private static long? DbInt64(SqlDataReader reader, string name)
        {
            int index = reader.GetOrdinal(name);
            return reader.IsDBNull(index) ? (long?)null : Convert.ToInt64(reader.GetValue(index));
        }

        private static DateTime? DbDateTime(SqlDataReader reader, string name)
        {
            int index = reader.GetOrdinal(name);
            return reader.IsDBNull(index) ? (DateTime?)null : Convert.ToDateTime(reader.GetValue(index));
        }
    }
}
