SELECT w.WarehouseCode,l.Id,l.LayoutCode,l.LayoutName,l.FloorNo,l.CoordinateMode,l.CoordinateUnit,
       l.MinX,l.MinY,l.MaxX,l.MaxY,l.SourceType,l.VersionNo,l.Status,l.IsActive,
       COUNT(o.Id) AS ObjectCount,MIN(o.X) AS ObjectMinX,MIN(o.Y) AS ObjectMinY,
       MAX(o.X+o.SizeX) AS ObjectMaxX,MAX(o.Y+o.SizeY) AS ObjectMaxY
FROM dbo.WMS_FG_MapLayout l
INNER JOIN dbo.WMS_FG_Warehouse w ON w.Id=l.WarehouseId
LEFT JOIN dbo.WMS_FG_MapObject o ON o.MapLayoutId=l.Id
WHERE w.WarehouseCode='FG01'
GROUP BY w.WarehouseCode,l.Id,l.LayoutCode,l.LayoutName,l.FloorNo,l.CoordinateMode,l.CoordinateUnit,
         l.MinX,l.MinY,l.MaxX,l.MaxY,l.SourceType,l.VersionNo,l.Status,l.IsActive
ORDER BY l.LayoutCode,l.VersionNo;
