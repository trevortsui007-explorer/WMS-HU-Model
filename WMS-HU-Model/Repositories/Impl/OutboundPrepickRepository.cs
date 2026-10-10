using System.Collections.Generic;
using System.Linq;
using WMS_HU_Model.Infrastructure.Database;
using WMS_HU_Model.Models.Outbound.Formal;

namespace WMS_HU_Model.Repositories
{
    public class OutboundPrepickRepository : IOutboundPrepickRepository
    {
        internal const string PackMasterSql = @"
SELECT prodno MaterialCode,prodrev MaterialRevision,pcspersetqty PcsPerSetQty
FROM dbo.MD_ProdMaster
WHERE plantcode=@plantCode AND prodno IN @materials AND status='on' AND ISNULL(isdeleted,0)=0;";
        public List<PrepickPackMaster> ReadPackMasters(string plantCode, string[] materials)
        {
            return materials.Length == 0 ? new List<PrepickPackMaster>()
                : DbExecutor.Query<PrepickPackMaster>(PackMasterSql, new { plantCode, materials });
        }
        public List<PrepickWarehouse> ReadWarehouses(string plantCode)
        {
            return DbExecutor.Query<PrepickWarehouse>(@"
SELECT DISTINCT w.Id WarehouseId,w.WarehouseCode,wp.SAPStorageLocation
FROM dbo.WMS_FG_WarehousePlant wp
JOIN dbo.WMS_FG_Warehouse w ON w.Id=wp.WarehouseId
WHERE wp.PlantCode=@plantCode AND wp.IsActive=1 AND w.IsActive=1
AND w.WarehouseType='FINISHED_GOODS';", new { plantCode });
        }
        public bool HasWork(long orderId)
        {
            return DbExecutor.ExecuteScalar<int>(@"
SELECT CASE WHEN EXISTS(SELECT 1 FROM dbo.WMS_FG_OutboundAllocation a WHERE a.OutboundOrderId=@orderId OR EXISTS (SELECT 1 FROM dbo.WMS_FG_OutboundPickBatch b JOIN dbo.WMS_FG_OutboundPickBatchDetail d ON d.PickBatchId=b.Id JOIN dbo.WMS_FG_OutboundPickDetailItem i ON i.PickDetailId=d.Id JOIN dbo.WMS_FG_OutboundOrderLine l ON l.Id=i.OutboundOrderLineId WHERE b.AllocationId=a.Id AND l.OutboundOrderId=@orderId))
OR EXISTS(SELECT 1 FROM dbo.WMS_FG_OutboundOrderLine WHERE OutboundOrderId=@orderId AND EffectivePickedQty<>0)
THEN 1 ELSE 0 END;", new { orderId }) != 0;
        }
        public List<PrepickStockRow> ReadStock(OutboundOrderDto order, long warehouseId)
        {
            if (order.Lines.Count == 0) return new List<PrepickStockRow>();
            // Read every child of matching boxes, not only valid items: invalid siblings must
            // prevent a misleading whole-box recommendation. EXISTS avoids reservation fan-out.
            string sql = @"
;WITH Roots AS (
 SELECT b.Id RootHUId,b.Id HUId,b.HUCode,b.HUType,0 LevelNo
 FROM dbo.WMS_FG_HU b
 WHERE b.HUType='OUTER_BOX' AND b.PlantCode=@PlantCode
 AND EXISTS(SELECT 1 FROM dbo.WMS_FG_HU_Relation r
 JOIN dbo.WMS_FG_HU_Item i ON i.HUId=r.ChildHUId AND i.IsActive=1
 WHERE r.ParentHUId=b.Id AND r.IsActive=1 AND i.ProdNo IN @Materials)
)" + EffectiveLocationResolver.AncestorAndRankedLocationCteSql + @"
SELECT b.Id BoxHuId,b.HUCode BoxCode,loc.LocationCode,p.Id InnerHuId,p.HUCode InnerCode,
 i.ProdNo MaterialCode,i.ProdRev MaterialRevision,i.Uom,i.DateCode,ISNULL(i.Qty,0) Qty,i.WeekSortKey,
 CAST(CASE WHEN i.WeekSortKey IS NULL OR i.WeekResolvedTime IS NULL THEN 1 ELSE 0 END AS bit) UnresolvedWeek,
 CAST(CASE WHEN b.IsHold=1 OR p.IsHold=1 OR EXISTS(SELECT 1 FROM Ancestors a
 JOIN dbo.WMS_FG_HU h ON h.Id=a.HUId WHERE a.RootHUId=b.Id AND h.IsHold=1) THEN 1 ELSE 0 END AS bit) Held,
 CAST(CASE WHEN loc.IsActive<>1 OR loc.IsVirtual<>0 OR ISNULL(z.ZoneCode,'')<>'STORAGE'
 THEN 1 ELSE 0 END AS bit) NotShelved,
 CAST(CASE WHEN b.LifecycleStatus<>'ACTIVE' OR p.LifecycleStatus<>'ACTIVE'
 OR ISNULL(i.StockStatus,'')<>'AVAILABLE' THEN 1 ELSE 0 END AS bit) BadStock,
 CAST(CASE WHEN p.HUType<>'INNER_PACK' OR ISNULL(p.PlantCode,'')<>b.PlantCode OR i.Id IS NULL OR ISNULL(i.Qty,0)<=0
 OR EXISTS(SELECT 1 FROM dbo.WMS_FG_HU_Relation dup WHERE dup.ChildHUId=p.Id AND dup.IsActive=1 AND dup.Id<>r.Id)
 OR EXISTS(SELECT 1 FROM dbo.WMS_FG_HU_Relation nested WHERE nested.ParentHUId=p.Id AND nested.IsActive=1)
 THEN 1 ELSE 0 END AS bit) BadPackaging,
 CAST(CASE WHEN EXISTS(SELECT 1 FROM dbo.WMS_FG_HU_Reservation res
 LEFT JOIN dbo.WMS_FG_OutboundAllocation alloc ON alloc.Id=res.AllocationId
 WHERE (res.HUId=b.Id OR res.HUId=p.Id) AND res.ReservationStatus='ACTIVE'
 AND (alloc.OutboundOrderId<>@OrderId OR alloc.OutboundOrderId IS NULL) AND NOT EXISTS (SELECT 1 FROM dbo.WMS_FG_OutboundPickBatchDetail d JOIN dbo.WMS_FG_OutboundPickDetailItem i ON i.PickDetailId=d.Id JOIN dbo.WMS_FG_OutboundOrderLine l ON l.Id=i.OutboundOrderLineId WHERE d.ReservationId=res.Id AND l.OutboundOrderId=@OrderId)) THEN 1 ELSE 0 END AS bit) OtherReservation,
 CAST(CASE WHEN EXISTS(SELECT 1 FROM dbo.WMS_FG_HU_Reservation res
 JOIN dbo.WMS_FG_OutboundAllocation alloc ON alloc.Id=res.AllocationId
 WHERE (res.HUId=b.Id OR res.HUId=p.Id) AND res.ReservationStatus='ACTIVE'
 AND (alloc.OutboundOrderId=@OrderId OR EXISTS (SELECT 1 FROM dbo.WMS_FG_OutboundPickBatchDetail d JOIN dbo.WMS_FG_OutboundPickDetailItem i ON i.PickDetailId=d.Id JOIN dbo.WMS_FG_OutboundOrderLine l ON l.Id=i.OutboundOrderLineId WHERE d.ReservationId=res.Id AND l.OutboundOrderId=@OrderId))) THEN 1 ELSE 0 END AS bit) OwnReservation
FROM Roots root
JOIN dbo.WMS_FG_HU b ON b.Id=root.RootHUId
JOIN RankedEffectiveLocations el ON el.RootHUId=b.Id AND el.LocationRank=1
JOIN dbo.WMS_FG_Location loc ON loc.Id=el.LocationId AND loc.WarehouseId=@warehouseId
LEFT JOIN dbo.WMS_FG_Zone z ON z.Id=loc.ZoneId
LEFT JOIN dbo.WMS_FG_HU_Relation r ON r.ParentHUId=b.Id AND r.IsActive=1
LEFT JOIN dbo.WMS_FG_HU p ON p.Id=r.ChildHUId
LEFT JOIN dbo.WMS_FG_HU_Item i ON i.HUId=p.Id AND i.IsActive=1
ORDER BY b.Id,p.Id,i.Id OPTION(MAXRECURSION 100);";
            return DbExecutor.Query<PrepickStockRow>(sql, new {
                order.PlantCode, warehouseId, OrderId=order.Id,
                Materials=order.Lines.Select(x=>x.MaterialCode).Distinct().ToArray()
            });
        }
    }
}
