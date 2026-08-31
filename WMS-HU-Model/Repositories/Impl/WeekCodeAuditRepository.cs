using System.Collections.Generic;
using System.Data;
using System.Linq;
using Dapper;
using WMS_HU_Model.Infrastructure.Database;
using WMS_HU_Model.Models.WeekCode;

namespace WMS_HU_Model.Repositories
{
    public class WeekCodeAuditRepository : IWeekCodeAuditRepository
    {
        public List<WeekCodeAuditFact> QueryFacts(WeekCodeAuditQuery query, int maxRows)
        {
            const string sql = @"
SELECT TOP (@MaxRows)
    box.Id AS BoxHUId,
    box.HUCode AS BoxCode,
    box.PlantCode,
    item.ProdNo AS MaterialCode,
    item.ProdRev AS MaterialRevision,
    item.DateCode,
    item.WeekRuleSource,
    item.WeekRuleValue,
    item.WeekYear,
    item.WeekNo,
    item.WeekSortKey,
    box.IsHold,
    hold.HoldReasonCode,
    hold.HoldReason,
    innerHu.SourceSystem AS PackSourceSystem,
    innerHu.SourceEntityId AS PackSourceEntityId,
    SUM(item.Qty) AS TotalQty
FROM dbo.WMS_FG_HU box
INNER JOIN dbo.WMS_FG_HU_Relation relation ON relation.ParentHUId = box.Id AND relation.IsActive = 1
INNER JOIN dbo.WMS_FG_HU innerHu ON innerHu.Id = relation.ChildHUId AND innerHu.HUType = 'INNER_PACK'
INNER JOIN dbo.WMS_FG_HU_Item item ON item.HUId = innerHu.Id AND item.IsActive = 1
LEFT JOIN dbo.WMS_FG_HU_HoldLog hold ON hold.HUId = box.Id AND hold.IsActive = 1
WHERE box.HUType = 'OUTER_BOX'
  AND box.LifecycleStatus = 'ACTIVE'
  AND (@PlantCode IS NULL OR box.PlantCode = @PlantCode)
  AND (@MaterialCode IS NULL OR item.ProdNo = @MaterialCode)
  AND (@MaterialRevision IS NULL OR item.ProdRev = @MaterialRevision)
GROUP BY
    box.Id, box.HUCode, box.PlantCode,
    item.ProdNo, item.ProdRev, item.DateCode,
    item.WeekRuleSource, item.WeekRuleValue, item.WeekYear, item.WeekNo, item.WeekSortKey,
    box.IsHold, hold.HoldReasonCode, hold.HoldReason,
    innerHu.SourceSystem, innerHu.SourceEntityId
ORDER BY box.Id, item.DateCode, item.WeekSortKey;";
            DynamicParameters parameters = new DynamicParameters();
            parameters.Add("@MaxRows", maxRows, DbType.Int32);
            parameters.Add("@PlantCode", DbParameterValue.StringOrNull(query.PlantCode), DbType.AnsiString, size: 20);
            parameters.Add("@MaterialCode", DbParameterValue.StringOrNull(query.MaterialCode), DbType.AnsiString, size: 50);
            parameters.Add("@MaterialRevision", DbParameterValue.StringOrNull(query.MaterialRevision), DbType.AnsiString, size: 30);
            List<WeekCodeAuditRawFact> rows = DbExecutor.Query<WeekCodeAuditRawFact>(sql, parameters);
            return rows.GroupBy(x => new
            {
                x.BoxHUId, x.BoxCode, x.PlantCode, x.MaterialCode, x.MaterialRevision, x.DateCode,
                x.WeekRuleSource, x.WeekRuleValue, x.WeekYear, x.WeekNo, x.WeekSortKey,
                x.IsHold, x.HoldReasonCode, x.HoldReason
            }).Select(group => new WeekCodeAuditFact
            {
                BoxHUId = group.Key.BoxHUId,
                BoxCode = group.Key.BoxCode,
                PlantCode = group.Key.PlantCode,
                MaterialCode = group.Key.MaterialCode,
                MaterialRevision = group.Key.MaterialRevision,
                DateCode = group.Key.DateCode,
                WeekRuleSource = group.Key.WeekRuleSource,
                WeekRuleValue = group.Key.WeekRuleValue,
                WeekYear = group.Key.WeekYear,
                WeekNo = group.Key.WeekNo,
                WeekSortKey = group.Key.WeekSortKey,
                IsHold = group.Key.IsHold,
                HoldReasonCode = group.Key.HoldReasonCode,
                HoldReason = group.Key.HoldReason,
                PackCount = group.Count(),
                TotalQty = group.Sum(x => x.TotalQty),
                PackSources = group.Select(x => new WeekCodeAuditPackSource { SourceSystem = x.PackSourceSystem, SourceEntityId = x.PackSourceEntityId }).ToList()
            }).ToList();
        }

        private class WeekCodeAuditRawFact : WeekCodeAuditFact
        {
            public string PackSourceSystem { get; set; }
            public string PackSourceEntityId { get; set; }
        }
    }
}
