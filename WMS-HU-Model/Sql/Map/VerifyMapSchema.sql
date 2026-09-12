SELECT t.name AS TableName, c.column_id AS ColumnId, c.name AS ColumnName, ty.name AS DataType,
       c.max_length AS MaxLength, c.precision AS NumericPrecision, c.scale AS NumericScale,
       c.is_nullable AS IsNullable, c.is_identity AS IsIdentity
FROM sys.tables t
INNER JOIN sys.schemas s ON s.schema_id=t.schema_id
INNER JOIN sys.columns c ON c.object_id=t.object_id
INNER JOIN sys.types ty ON ty.user_type_id=c.user_type_id
WHERE s.name='dbo' AND t.name IN ('WMS_FG_MapLayout','WMS_FG_MapObject')
ORDER BY t.name,c.column_id;

SELECT t.name AS TableName, i.name AS IndexName, i.is_unique AS IsUnique, i.filter_definition AS FilterDefinition
FROM sys.tables t
INNER JOIN sys.schemas s ON s.schema_id=t.schema_id
INNER JOIN sys.indexes i ON i.object_id=t.object_id
WHERE s.name='dbo' AND t.name IN ('WMS_FG_MapLayout','WMS_FG_MapObject') AND i.index_id>0
ORDER BY t.name,i.name;

SELECT cc.name AS ConstraintName, cc.definition AS ConstraintDefinition, cc.is_disabled AS IsDisabled,
       cc.is_not_trusted AS IsNotTrusted
FROM sys.check_constraints cc
WHERE cc.parent_object_id=OBJECT_ID(N'dbo.WMS_FG_MapLayout')
ORDER BY cc.name;

SELECT c.name AS ColumnName, dc.name AS DefaultConstraintName, dc.definition AS DefaultDefinition
FROM sys.columns c
LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id
WHERE c.object_id=OBJECT_ID(N'dbo.WMS_FG_MapLayout') AND c.name=N'IsActive';
