using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using WMS_HU_Model.Infrastructure.Database;
using WMS_HU_Model.Map.Dtos;
using WMS_HU_Model.Map.Models;

namespace WMS_HU_Model.Map.Repositories.Impl
{
    public class MapRepository : IMapRepository
    {
        public List<MapLayout> GetLayouts(string warehouseCode)
        {
            const string sql = @"SELECT layout.*, warehouse.WarehouseCode
FROM dbo.WMS_FG_MapLayout layout
INNER JOIN dbo.WMS_FG_Warehouse warehouse ON warehouse.Id=layout.WarehouseId
WHERE (@WarehouseCode IS NULL OR warehouse.WarehouseCode=@WarehouseCode)
ORDER BY warehouse.WarehouseCode,layout.FloorNo,layout.LayoutCode,layout.VersionNo DESC;";
            using (SqlConnection connection = DbConnectionFactory.Create())
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                AddString(command, "@WarehouseCode", warehouseCode, 20, true);
                connection.Open();
                return ReadLayouts(command);
            }
        }

        public MapLayout GetLayout(long layoutId)
        {
            const string sql = @"SELECT layout.*, warehouse.WarehouseCode
FROM dbo.WMS_FG_MapLayout layout
INNER JOIN dbo.WMS_FG_Warehouse warehouse ON warehouse.Id=layout.WarehouseId
WHERE layout.Id=@LayoutId;";
            using (SqlConnection connection = DbConnectionFactory.Create())
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                Add(command, "@LayoutId", SqlDbType.BigInt, layoutId);
                connection.Open();
                List<MapLayout> layouts = ReadLayouts(command);
                return layouts.Count == 0 ? null : layouts[0];
            }
        }

        public MapLayout GetRuntimeLayout(string warehouseCode)
        {
            const string sql = @"SELECT layout.*, warehouse.WarehouseCode
FROM dbo.WMS_FG_MapLayout layout
INNER JOIN dbo.WMS_FG_Warehouse warehouse ON warehouse.Id=layout.WarehouseId
WHERE warehouse.WarehouseCode=@WarehouseCode AND layout.Status='PUBLISHED' AND layout.IsActive=1
ORDER BY layout.VersionNo DESC;";
            using (SqlConnection connection = DbConnectionFactory.Create())
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                AddString(command, "@WarehouseCode", warehouseCode, 20, false);
                connection.Open();
                List<MapLayout> layouts = ReadLayouts(command);
                if (layouts.Count > 1) throw new InvalidOperationException("仓库存在多个 Active PUBLISHED 地图，请先修正数据。");
                return layouts.Count == 0 ? null : layouts[0];
            }
        }

        public List<MapObject> GetObjects(long layoutId)
        {
            const string sql = @"SELECT Id,MapLayoutId,ParentObjectId,ObjectCode,ObjectName,ObjectType,BindingType,BindingCode,
GeometryType,X,Y,Z,SizeX,SizeY,SizeZ,RotationZ,GeometryJson,StyleKey,StyleJson,LayerCode,SortNo,IsVisible,Remark
FROM dbo.WMS_FG_MapObject WHERE MapLayoutId=@LayoutId ORDER BY SortNo,Id;";
            using (SqlConnection connection = DbConnectionFactory.Create())
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                Add(command, "@LayoutId", SqlDbType.BigInt, layoutId);
                connection.Open();
                List<MapObject> result = new List<MapObject>();
                using (SqlDataReader reader = command.ExecuteReader()) while (reader.Read()) result.Add(ReadObject(reader));
                return result;
            }
        }

        public long CreateDraftLayout(CreateMapLayoutRequest request)
        {
            const string sql = @"SET XACT_ABORT ON;
DECLARE @WarehouseId bigint;
SELECT @WarehouseId=Id FROM dbo.WMS_FG_Warehouse WITH (UPDLOCK,HOLDLOCK) WHERE WarehouseCode=@WarehouseCode AND IsActive=1;
IF @WarehouseId IS NULL THROW 51019, 'Warehouse not found or inactive.', 1;
DECLARE @ExistingId bigint;
SELECT @ExistingId=Id FROM dbo.WMS_FG_MapLayout WITH (UPDLOCK,HOLDLOCK)
WHERE WarehouseId=@WarehouseId AND LayoutCode=@LayoutCode AND FloorNo=@FloorNo AND Status='DRAFT';
IF @ExistingId IS NOT NULL SELECT @ExistingId;
ELSE BEGIN
 INSERT dbo.WMS_FG_MapLayout(WarehouseId,LayoutCode,LayoutName,FloorNo,CoordinateMode,CoordinateUnit,MinX,MinY,MaxX,MaxY,SourceType,VersionNo,Status,IsActive,Remark,CreateBy)
 VALUES(@WarehouseId,@LayoutCode,@LayoutName,@FloorNo,'LOGICAL','UNIT',@MinX,@MinY,@MaxX,@MaxY,'EDITOR',1,'DRAFT',0,@Remark,@Operator);
 SELECT CAST(SCOPE_IDENTITY() AS bigint);
END";
            using (SqlConnection connection = DbConnectionFactory.Create())
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                using (SqlCommand command = new SqlCommand(sql, connection, transaction))
                {
                    AddString(command, "@WarehouseCode", request.WarehouseCode, 20, false);
                    AddString(command, "@LayoutCode", request.LayoutCode, 50, false);
                    AddNString(command, "@LayoutName", request.LayoutName, 100, false);
                    Add(command, "@FloorNo", SqlDbType.Int, request.FloorNo);
                    AddDecimal(command, "@MinX", request.MinX);
                    AddDecimal(command, "@MinY", request.MinY);
                    AddDecimal(command, "@MaxX", request.MaxX);
                    AddDecimal(command, "@MaxY", request.MaxY);
                    AddNString(command, "@Remark", request.Remark, 500, true);
                    AddString(command, "@Operator", request.Operator, 50, false);
                    long id = Convert.ToInt64(command.ExecuteScalar());
                    transaction.Commit();
                    return id;
                }
            }
        }

        public List<MapLocationBinding> GetLocationBindings(long warehouseId)
        {
            const string sql = @"SELECT Id,LocationCode,LocationName,LocationTypeCode,IsVirtual,IsActive
FROM dbo.WMS_FG_Location WHERE WarehouseId=@WarehouseId ORDER BY LocationCode;";
            using (SqlConnection connection = DbConnectionFactory.Create())
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                Add(command, "@WarehouseId", SqlDbType.BigInt, warehouseId);
                connection.Open();
                List<MapLocationBinding> result = new List<MapLocationBinding>();
                using (SqlDataReader reader = command.ExecuteReader()) while (reader.Read()) result.Add(new MapLocationBinding
                {
                    Id = reader.GetInt64(reader.GetOrdinal("Id")),
                    LocationCode = reader.GetString(reader.GetOrdinal("LocationCode")),
                    LocationName = NullableString(reader, "LocationName"),
                    LocationTypeCode = reader.GetString(reader.GetOrdinal("LocationTypeCode")),
                    IsVirtual = reader.GetBoolean(reader.GetOrdinal("IsVirtual")),
                    IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"))
                });
                return result;
            }
        }

        public void SaveObjects(long layoutId, IList<MapObject> upserts, IList<long> deletedIds, string operatorName)
        {
            using (SqlConnection connection = DbConnectionFactory.Create())
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    EnsureDraft(connection, transaction, layoutId);
                    foreach (long deletedId in deletedIds) DeleteObject(connection, transaction, layoutId, deletedId);
                    foreach (MapObject item in upserts)
                    {
                        item.MapLayoutId = layoutId;
                        if (item.Id > 0) UpdateObject(connection, transaction, item, operatorName);
                        else item.Id = InsertObject(connection, transaction, item, operatorName);
                    }
                    transaction.Commit();
                }
            }
        }

        public void ImportObjects(long layoutId, IList<MapObject> objects, decimal minX, decimal minY, decimal maxX, decimal maxY, string operatorName)
        {
            using (SqlConnection connection = DbConnectionFactory.Create())
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    EnsureDraft(connection, transaction, layoutId);
                    using (SqlCommand existing = new SqlCommand("SELECT COUNT(1) FROM dbo.WMS_FG_MapObject WITH (UPDLOCK,HOLDLOCK) WHERE MapLayoutId=@LayoutId;", connection, transaction))
                    {
                        Add(existing, "@LayoutId", SqlDbType.BigInt, layoutId);
                        if (Convert.ToInt32(existing.ExecuteScalar()) > 0) throw new InvalidOperationException("MAP_ALREADY_HAS_OBJECTS");
                    }

                    Dictionary<long, long> importedIds = new Dictionary<long, long>();
                    foreach (MapObject item in objects)
                    {
                        long temporaryId = item.Id;
                        if (item.ParentObjectId.HasValue && item.ParentObjectId.Value < 0)
                        {
                            long parentId;
                            if (!importedIds.TryGetValue(item.ParentObjectId.Value, out parentId)) throw new InvalidOperationException("导入对象的父组合尚未创建。");
                            item.ParentObjectId = parentId;
                        }
                        item.MapLayoutId = layoutId;
                        item.Id = InsertObject(connection, transaction, item, operatorName);
                        if (temporaryId < 0) importedIds.Add(temporaryId, item.Id);
                    }
                    using (SqlCommand bounds = new SqlCommand(@"UPDATE dbo.WMS_FG_MapLayout SET MinX=@MinX,MinY=@MinY,MaxX=@MaxX,MaxY=@MaxY,LastEditBy=@Operator,LastEditDt=SYSDATETIME() WHERE Id=@LayoutId;", connection, transaction))
                    {
                        AddDecimal(bounds, "@MinX", minX); AddDecimal(bounds, "@MinY", minY); AddDecimal(bounds, "@MaxX", maxX); AddDecimal(bounds, "@MaxY", maxY);
                        AddString(bounds, "@Operator", operatorName, 50, false); Add(bounds, "@LayoutId", SqlDbType.BigInt, layoutId);
                        if (bounds.ExecuteNonQuery() != 1) throw new InvalidOperationException("地图布局不存在。");
                    }
                    transaction.Commit();
                }
            }
        }

        private static void EnsureDraft(SqlConnection connection, SqlTransaction transaction, long layoutId)
        {
            using (SqlCommand command = new SqlCommand("SELECT Status FROM dbo.WMS_FG_MapLayout WITH (UPDLOCK,HOLDLOCK) WHERE Id=@LayoutId;", connection, transaction))
            {
                Add(command, "@LayoutId", SqlDbType.BigInt, layoutId);
                object status = command.ExecuteScalar();
                if (!string.Equals(Convert.ToString(status), "DRAFT", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("只有 DRAFT 地图允许保存。");
            }
        }

        private static void DeleteObject(SqlConnection connection, SqlTransaction transaction, long layoutId, long objectId)
        {
            using (SqlCommand command = new SqlCommand("DELETE dbo.WMS_FG_MapObject WHERE Id=@Id AND MapLayoutId=@LayoutId;", connection, transaction))
            {
                Add(command, "@Id", SqlDbType.BigInt, objectId);
                Add(command, "@LayoutId", SqlDbType.BigInt, layoutId);
                command.ExecuteNonQuery();
            }
        }

        private static long InsertObject(SqlConnection connection, SqlTransaction transaction, MapObject item, string operatorName)
        {
            const string sql = @"INSERT dbo.WMS_FG_MapObject(MapLayoutId,ParentObjectId,ObjectCode,ObjectName,ObjectType,BindingType,BindingCode,GeometryType,X,Y,Z,SizeX,SizeY,SizeZ,RotationZ,GeometryJson,StyleKey,StyleJson,LayerCode,SortNo,IsVisible,Remark,CreateBy)
VALUES(@MapLayoutId,@ParentObjectId,@ObjectCode,@ObjectName,@ObjectType,@BindingType,@BindingCode,@GeometryType,@X,@Y,@Z,@SizeX,@SizeY,@SizeZ,@RotationZ,@GeometryJson,@StyleKey,@StyleJson,@LayerCode,@SortNo,@IsVisible,@Remark,@Operator);
SELECT CAST(SCOPE_IDENTITY() AS bigint);";
            using (SqlCommand command = new SqlCommand(sql, connection, transaction))
            {
                AddObjectParameters(command, item, operatorName);
                return Convert.ToInt64(command.ExecuteScalar());
            }
        }

        private static void UpdateObject(SqlConnection connection, SqlTransaction transaction, MapObject item, string operatorName)
        {
            const string sql = @"UPDATE dbo.WMS_FG_MapObject SET ParentObjectId=@ParentObjectId,ObjectCode=@ObjectCode,ObjectName=@ObjectName,ObjectType=@ObjectType,BindingType=@BindingType,BindingCode=@BindingCode,GeometryType=@GeometryType,X=@X,Y=@Y,Z=@Z,SizeX=@SizeX,SizeY=@SizeY,SizeZ=@SizeZ,RotationZ=@RotationZ,GeometryJson=@GeometryJson,StyleKey=@StyleKey,StyleJson=@StyleJson,LayerCode=@LayerCode,SortNo=@SortNo,IsVisible=@IsVisible,Remark=@Remark,LastEditBy=@Operator,LastEditDt=SYSDATETIME()
WHERE Id=@Id AND MapLayoutId=@MapLayoutId;";
            using (SqlCommand command = new SqlCommand(sql, connection, transaction))
            {
                AddObjectParameters(command, item, operatorName);
                Add(command, "@Id", SqlDbType.BigInt, item.Id);
                if (command.ExecuteNonQuery() != 1) throw new InvalidOperationException("地图对象不存在或不属于当前布局：" + item.Id);
            }
        }

        private static void AddObjectParameters(SqlCommand command, MapObject item, string operatorName)
        {
            Add(command, "@MapLayoutId", SqlDbType.BigInt, item.MapLayoutId);
            Add(command, "@ParentObjectId", SqlDbType.BigInt, item.ParentObjectId.HasValue ? (object)item.ParentObjectId.Value : DBNull.Value);
            AddString(command, "@ObjectCode", item.ObjectCode, 80, false);
            AddNString(command, "@ObjectName", item.ObjectName, 120, true);
            AddString(command, "@ObjectType", item.ObjectType, 30, false);
            AddString(command, "@BindingType", item.BindingType, 30, true);
            AddString(command, "@BindingCode", item.BindingCode, 100, true);
            AddString(command, "@GeometryType", item.GeometryType, 30, false);
            AddDecimal(command, "@X", item.X); AddDecimal(command, "@Y", item.Y); AddDecimal(command, "@Z", item.Z);
            AddDecimal(command, "@SizeX", item.SizeX); AddDecimal(command, "@SizeY", item.SizeY); AddDecimal(command, "@SizeZ", item.SizeZ);
            AddDecimal(command, "@RotationZ", item.RotationZ);
            AddNString(command, "@GeometryJson", item.GeometryJson, -1, true);
            AddString(command, "@StyleKey", item.StyleKey, 50, true);
            AddNString(command, "@StyleJson", item.StyleJson, -1, true);
            AddString(command, "@LayerCode", item.LayerCode, 50, true);
            Add(command, "@SortNo", SqlDbType.Int, item.SortNo);
            Add(command, "@IsVisible", SqlDbType.Bit, item.IsVisible);
            AddNString(command, "@Remark", item.Remark, 500, true);
            AddString(command, "@Operator", operatorName, 50, false);
        }

        private static List<MapLayout> ReadLayouts(SqlCommand command)
        {
            List<MapLayout> result = new List<MapLayout>();
            using (SqlDataReader reader = command.ExecuteReader()) while (reader.Read()) result.Add(new MapLayout
            {
                Id = reader.GetInt64(reader.GetOrdinal("Id")), WarehouseId = reader.GetInt64(reader.GetOrdinal("WarehouseId")), WarehouseCode = reader.GetString(reader.GetOrdinal("WarehouseCode")),
                LayoutCode = reader.GetString(reader.GetOrdinal("LayoutCode")), LayoutName = reader.GetString(reader.GetOrdinal("LayoutName")), FloorNo = reader.GetInt32(reader.GetOrdinal("FloorNo")),
                CoordinateMode = reader.GetString(reader.GetOrdinal("CoordinateMode")), CoordinateUnit = reader.GetString(reader.GetOrdinal("CoordinateUnit")),
                MinX = reader.GetDecimal(reader.GetOrdinal("MinX")), MinY = reader.GetDecimal(reader.GetOrdinal("MinY")), MaxX = reader.GetDecimal(reader.GetOrdinal("MaxX")), MaxY = reader.GetDecimal(reader.GetOrdinal("MaxY")),
                SourceType = reader.GetString(reader.GetOrdinal("SourceType")), BackgroundAsset = NullableString(reader, "BackgroundAsset"), VersionNo = reader.GetInt32(reader.GetOrdinal("VersionNo")),
                Status = reader.GetString(reader.GetOrdinal("Status")), IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")), Remark = NullableString(reader, "Remark"),
                CreateDt = reader.GetDateTime(reader.GetOrdinal("CreateDt")), CreateBy = NullableString(reader, "CreateBy"), LastEditDt = NullableDateTime(reader, "LastEditDt"), LastEditBy = NullableString(reader, "LastEditBy")
            });
            return result;
        }

        private static MapObject ReadObject(SqlDataReader reader)
        {
            return new MapObject
            {
                Id = reader.GetInt64(reader.GetOrdinal("Id")), MapLayoutId = reader.GetInt64(reader.GetOrdinal("MapLayoutId")), ParentObjectId = NullableInt64(reader, "ParentObjectId"),
                ObjectCode = reader.GetString(reader.GetOrdinal("ObjectCode")), ObjectName = NullableString(reader, "ObjectName"), ObjectType = reader.GetString(reader.GetOrdinal("ObjectType")),
                BindingType = NullableString(reader, "BindingType"), BindingCode = NullableString(reader, "BindingCode"), GeometryType = reader.GetString(reader.GetOrdinal("GeometryType")),
                X = reader.GetDecimal(reader.GetOrdinal("X")), Y = reader.GetDecimal(reader.GetOrdinal("Y")), Z = reader.GetDecimal(reader.GetOrdinal("Z")),
                SizeX = reader.GetDecimal(reader.GetOrdinal("SizeX")), SizeY = reader.GetDecimal(reader.GetOrdinal("SizeY")), SizeZ = reader.GetDecimal(reader.GetOrdinal("SizeZ")), RotationZ = reader.GetDecimal(reader.GetOrdinal("RotationZ")),
                GeometryJson = NullableString(reader, "GeometryJson"), StyleKey = NullableString(reader, "StyleKey"), StyleJson = NullableString(reader, "StyleJson"), LayerCode = NullableString(reader, "LayerCode"),
                SortNo = reader.GetInt32(reader.GetOrdinal("SortNo")), IsVisible = reader.GetBoolean(reader.GetOrdinal("IsVisible")), Remark = NullableString(reader, "Remark")
            };
        }

        private static string NullableString(SqlDataReader reader, string name) { int index = reader.GetOrdinal(name); return reader.IsDBNull(index) ? null : reader.GetString(index); }
        private static DateTime? NullableDateTime(SqlDataReader reader, string name) { int index = reader.GetOrdinal(name); return reader.IsDBNull(index) ? (DateTime?)null : reader.GetDateTime(index); }
        private static long? NullableInt64(SqlDataReader reader, string name) { int index = reader.GetOrdinal(name); return reader.IsDBNull(index) ? (long?)null : reader.GetInt64(index); }
        private static void Add(SqlCommand command, string name, SqlDbType type, object value) { SqlParameter parameter = command.Parameters.Add(name, type); parameter.Value = value ?? DBNull.Value; }
        private static void AddString(SqlCommand command, string name, string value, int size, bool nullable) { SqlParameter parameter = command.Parameters.Add(name, SqlDbType.VarChar, size); parameter.Value = string.IsNullOrWhiteSpace(value) && nullable ? (object)DBNull.Value : value; }
        private static void AddNString(SqlCommand command, string name, string value, int size, bool nullable) { SqlParameter parameter = command.Parameters.Add(name, SqlDbType.NVarChar, size); parameter.Value = string.IsNullOrWhiteSpace(value) && nullable ? (object)DBNull.Value : value; }
        private static void AddDecimal(SqlCommand command, string name, decimal value) { SqlParameter parameter = command.Parameters.Add(name, SqlDbType.Decimal); parameter.Precision = 18; parameter.Scale = 4; parameter.Value = value; }
    }
}
