SELECT l.Id AS MapLayoutId,o.Id AS MapObjectId,o.ObjectCode,o.BindingType,o.BindingCode,
       location.Id AS LocationId,location.LocationTypeCode,location.IsVirtual,location.IsActive,
       CASE WHEN location.Id IS NULL THEN 'LOCATION_NOT_FOUND'
            WHEN location.IsVirtual=1 THEN 'LOCATION_VIRTUAL'
            WHEN location.IsActive=0 THEN 'LOCATION_INACTIVE'
            WHEN duplicateBinding.BindingCount>1 THEN 'LOCATION_DUPLICATE_BINDING'
            ELSE 'OK' END AS ValidationResult
FROM dbo.WMS_FG_MapLayout l
INNER JOIN dbo.WMS_FG_Warehouse w ON w.Id=l.WarehouseId
INNER JOIN dbo.WMS_FG_MapObject o ON o.MapLayoutId=l.Id AND o.BindingType='LOCATION'
LEFT JOIN dbo.WMS_FG_Location location ON location.WarehouseId=l.WarehouseId AND location.LocationCode=o.BindingCode
LEFT JOIN
(
    SELECT MapLayoutId,BindingCode,COUNT(1) AS BindingCount
    FROM dbo.WMS_FG_MapObject
    WHERE BindingType='LOCATION'
    GROUP BY MapLayoutId,BindingCode
) duplicateBinding ON duplicateBinding.MapLayoutId=o.MapLayoutId AND duplicateBinding.BindingCode=o.BindingCode
WHERE w.WarehouseCode='FG01'
ORDER BY l.Id,o.ObjectCode;

SELECT location.LocationCode,location.LocationTypeCode
FROM dbo.WMS_FG_Location location
INNER JOIN dbo.WMS_FG_Warehouse warehouse ON warehouse.Id=location.WarehouseId
WHERE warehouse.WarehouseCode='FG01' AND location.IsVirtual=0 AND location.IsActive=1
  AND NOT EXISTS
  (
      SELECT 1 FROM dbo.WMS_FG_MapLayout layout
      INNER JOIN dbo.WMS_FG_MapObject object ON object.MapLayoutId=layout.Id
      WHERE layout.WarehouseId=location.WarehouseId AND object.BindingType='LOCATION' AND object.BindingCode=location.LocationCode
  )
ORDER BY location.LocationCode;
