using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using WMS_HU_Model.Infrastructure.Database;
using WMS_HU_Model.Models.HU;

namespace WMS_HU_Model.Repositories
{
    public class HuRepository : IHuRepository
    {
        #region Query HU

        /// <summary>
        /// 根据 HUCode 查询 HU
        /// </summary>
        public HuDto GetByCode(string huCode)
        {
            const string sql = @"
SELECT
    h.Id,
    h.HUCode,
    h.HUType,
    h.PlantCode,
    ps.PackageSpecCode,
    h.LifecycleStatus,
    h.IsHold,
    h.HoldReason,
    h.SourceSystem,
    h.SourceEntityId,
    h.SourceBusinessNo,
    h.Remark
FROM dbo.WMS_HU h
LEFT JOIN dbo.WMS_PackageSpec ps
    ON h.PackageSpecId = ps.Id
WHERE h.HUCode = @HUCode;
";

            using (SqlConnection conn =
                   DbConnectionFactory.Create())
            using (SqlCommand cmd =
                   new SqlCommand(sql, conn))
            {
                cmd.Parameters.Add(
                    "@HUCode",
                    SqlDbType.VarChar,
                    100)
                    .Value = huCode;

                conn.Open();

                using (SqlDataReader reader =
                       cmd.ExecuteReader())
                {
                    if (!reader.Read())
                        return null;

                    return MapHu(reader);
                }
            }
        }


        /// <summary>
        /// 根据 HUCode 获取 HU 主键
        /// </summary>
        public long? GetHuId(string huCode)
        {
            const string sql = @"
SELECT Id
FROM dbo.WMS_HU
WHERE HUCode = @HUCode;
";

            using (SqlConnection conn =
                   DbConnectionFactory.Create())
            using (SqlCommand cmd =
                   new SqlCommand(sql, conn))
            {
                cmd.Parameters.Add(
                    "@HUCode",
                    SqlDbType.VarChar,
                    100)
                    .Value = huCode;

                conn.Open();

                object value = cmd.ExecuteScalar();

                if (value == null ||
                    value == DBNull.Value)
                {
                    return null;
                }

                return Convert.ToInt64(value);
            }
        }


        /// <summary>
        /// 根据 HUId 获取 HU 类型
        /// </summary>
        public string GetHuType(long huId)
        {
            const string sql = @"
SELECT HUType
FROM dbo.WMS_HU
WHERE Id = @HUId;
";

            using (SqlConnection conn =
                   DbConnectionFactory.Create())
            using (SqlCommand cmd =
                   new SqlCommand(sql, conn))
            {
                cmd.Parameters.Add(
                    "@HUId",
                    SqlDbType.BigInt)
                    .Value = huId;

                conn.Open();

                object value = cmd.ExecuteScalar();

                if (value == null ||
                    value == DBNull.Value)
                {
                    return null;
                }

                return Convert.ToString(value);
            }
        }


        /// <summary>
        /// 根据 HUId 获取厂别
        /// </summary>
        public string GetPlantCode(long huId)
        {
            const string sql = @"
SELECT PlantCode
FROM dbo.WMS_HU
WHERE Id = @HUId;
";

            using (SqlConnection conn =
                   DbConnectionFactory.Create())
            using (SqlCommand cmd =
                   new SqlCommand(sql, conn))
            {
                cmd.Parameters.Add(
                    "@HUId",
                    SqlDbType.BigInt)
                    .Value = huId;

                conn.Open();

                object value = cmd.ExecuteScalar();

                if (value == null ||
                    value == DBNull.Value)
                {
                    return null;
                }

                return Convert.ToString(value);
            }
        }


        /// <summary>
        /// 根据包装规格编码获取 PackageSpecId
        /// </summary>
        public long? GetPackageSpecId(
            string packageSpecCode)
        {
            const string sql = @"
SELECT Id
FROM dbo.WMS_PackageSpec
WHERE PackageSpecCode = @PackageSpecCode
AND IsActive = 1;
";

            using (SqlConnection conn =
                   DbConnectionFactory.Create())
            using (SqlCommand cmd =
                   new SqlCommand(sql, conn))
            {
                cmd.Parameters.Add(
                    "@PackageSpecCode",
                    SqlDbType.VarChar,
                    30)
                    .Value = packageSpecCode;

                conn.Open();

                object value = cmd.ExecuteScalar();

                if (value == null ||
                    value == DBNull.Value)
                {
                    return null;
                }

                return Convert.ToInt64(value);
            }
        }

        #endregion


        #region Create HU

        /// <summary>
        /// 创建 HU
        ///
        /// 同一事务内：
        /// 1. 创建 WMS_HU
        /// 2. 写 CREATE_HU 操作流水
        /// </summary>
        public long Create(
            CreateHuRequest request,
            long? packageSpecId,
            string operationNo)
        {
            using (SqlConnection conn =
                   DbConnectionFactory.Create())
            {
                conn.Open();

                using (SqlTransaction tran =
                       conn.BeginTransaction())
                {
                    try
                    {
                        long huId = InsertHu(
                            conn,
                            tran,
                            request,
                            packageSpecId);

                        InsertOperationLog(
                            conn: conn,
                            tran: tran,
                            operationNo: operationNo,
                            operationType: "CREATE_HU",
                            huId: huId,
                            relatedHuId: null,
                            businessNo:
                                request.SourceBusinessNo,
                            requestId:
                                request.RequestId,
                            operatorCode:
                                request.Operator,
                            detailJson: null,
                            remark:
                                request.Remark);

                        tran.Commit();

                        return huId;
                    }
                    catch
                    {
                        tran.Rollback();
                        throw;
                    }
                }
            }
        }


        private static long InsertHu(
            SqlConnection conn,
            SqlTransaction tran,
            CreateHuRequest request,
            long? packageSpecId)
        {
            const string sql = @"
INSERT INTO dbo.WMS_HU
(
    HUCode,
    HUType,
    PlantCode,
    PackageSpecId,
    LifecycleStatus,
    IsHold,
    SourceSystem,
    SourceEntityId,
    SourceBusinessNo,
    Remark,
    CreateUser
)
VALUES
(
    @HUCode,
    @HUType,
    @PlantCode,
    @PackageSpecId,
    'ACTIVE',
    0,
    @SourceSystem,
    @SourceEntityId,
    @SourceBusinessNo,
    @Remark,
    @CreateUser
);

SELECT CAST(SCOPE_IDENTITY() AS BIGINT);
";

            using (SqlCommand cmd =
                   new SqlCommand(
                       sql,
                       conn,
                       tran))
            {
                cmd.Parameters.Add(
                    "@HUCode",
                    SqlDbType.VarChar,
                    100)
                    .Value = request.HuCode;

                cmd.Parameters.Add(
                    "@HUType",
                    SqlDbType.VarChar,
                    30)
                    .Value = request.HuType;

                cmd.Parameters.Add(
                    "@PlantCode",
                    SqlDbType.VarChar,
                    20)
                    .Value = request.PlantCode;

                AddNullableLong(
                    cmd,
                    "@PackageSpecId",
                    packageSpecId);

                AddNullableString(
                    cmd,
                    "@SourceSystem",
                    SqlDbType.VarChar,
                    30,
                    request.SourceSystem);

                AddNullableString(
                    cmd,
                    "@SourceEntityId",
                    SqlDbType.VarChar,
                    100,
                    request.SourceEntityId);

                AddNullableString(
                    cmd,
                    "@SourceBusinessNo",
                    SqlDbType.VarChar,
                    100,
                    request.SourceBusinessNo);

                AddNullableString(
                    cmd,
                    "@Remark",
                    SqlDbType.NVarChar,
                    500,
                    request.Remark);

                AddNullableString(
                    cmd,
                    "@CreateUser",
                    SqlDbType.VarChar,
                    50,
                    request.Operator);

                object result =
                    cmd.ExecuteScalar();

                return Convert.ToInt64(result);
            }
        }

        #endregion


        #region Relation Query

        /// <summary>
        /// 判断子 HU 是否已经存在有效父 HU
        /// </summary>
        public bool HasActiveParent(
            long childHuId)
        {
            const string sql = @"
SELECT COUNT(1)
FROM dbo.WMS_HU_Relation
WHERE ChildHUId = @ChildHUId
AND IsActive = 1;
";

            using (SqlConnection conn =
                   DbConnectionFactory.Create())
            using (SqlCommand cmd =
                   new SqlCommand(sql, conn))
            {
                cmd.Parameters.Add(
                    "@ChildHUId",
                    SqlDbType.BigInt)
                    .Value = childHuId;

                conn.Open();

                int count =
                    Convert.ToInt32(
                        cmd.ExecuteScalar());

                return count > 0;
            }
        }


        /// <summary>
        /// 检查新增 Parent -> Child 是否会形成循环
        ///
        /// 例如当前：
        ///
        /// A -> B
        /// B -> C
        ///
        /// 此时如果尝试：
        ///
        /// C -> A
        ///
        /// 就会形成循环。
        ///
        /// 算法：
        /// 从 Child 开始向下查所有后代，
        /// 如果 Parent 已经是 Child 的后代，
        /// 则 Parent -> Child 会产生循环。
        /// </summary>
        public bool WouldCreateCycle(
            long parentHuId,
            long childHuId)
        {
            const string sql = @"
;WITH Descendants AS
(
    SELECT
        @ChildHUId AS HUId,
        CAST(
            '/' +
            CAST(@ChildHUId AS VARCHAR(30)) +
            '/'
            AS VARCHAR(MAX)
        ) AS HUPath

    UNION ALL

    SELECT
        r.ChildHUId,

        CAST(
            d.HUPath +
            CAST(r.ChildHUId AS VARCHAR(30)) +
            '/'
            AS VARCHAR(MAX)
        ) AS HUPath

    FROM Descendants d

    INNER JOIN dbo.WMS_HU_Relation r
        ON r.ParentHUId = d.HUId
       AND r.IsActive = 1

    WHERE
        d.HUPath NOT LIKE
        '%/' +
        CAST(r.ChildHUId AS VARCHAR(30)) +
        '/%'
)

SELECT
    CASE
        WHEN EXISTS
        (
            SELECT 1
            FROM Descendants
            WHERE HUId = @ParentHUId
        )
        THEN 1
        ELSE 0
    END

OPTION (MAXRECURSION 100);
";

            using (SqlConnection conn =
                   DbConnectionFactory.Create())
            using (SqlCommand cmd =
                   new SqlCommand(sql, conn))
            {
                cmd.Parameters.Add(
                    "@ParentHUId",
                    SqlDbType.BigInt)
                    .Value = parentHuId;

                cmd.Parameters.Add(
                    "@ChildHUId",
                    SqlDbType.BigInt)
                    .Value = childHuId;

                conn.Open();

                int result =
                    Convert.ToInt32(
                        cmd.ExecuteScalar());

                return result == 1;
            }
        }

        #endregion


        #region Attach HU

        /// <summary>
        /// 建立 Parent -> Child 包装关系
        ///
        /// 同一事务：
        /// 1. INSERT WMS_HU_Relation
        /// 2. INSERT WMS_HU_OperationLog
        /// </summary>
        public void Attach(
            long parentHuId,
            long childHuId,
            AttachHuRequest request,
            string operationNo)
        {
            using (SqlConnection conn =
                   DbConnectionFactory.Create())
            {
                conn.Open();

                using (SqlTransaction tran =
                       conn.BeginTransaction(
                           IsolationLevel.ReadCommitted))
                {
                    try
                    {
                        /*
                         * 再做一次数据库级并发检查。
                         *
                         * Service 已经检查过一次，
                         * 但是 Service Check -> Insert
                         * 之间可能存在并发窗口。
                         */
                        if (HasActiveParent(
                            conn,
                            tran,
                            childHuId))
                        {
                            throw new InvalidOperationException(
                                "子HU已经存在有效父HU，请先拆出后再重新建立关系。");
                        }


                        const string relationSql = @"
INSERT INTO dbo.WMS_HU_Relation
(
    ParentHUId,
    ChildHUId,
    RelationType,
    EffectiveFrom,
    IsActive,
    OperationNo,
    CreateUser,
    CreateTime
)
VALUES
(
    @ParentHUId,
    @ChildHUId,
    'CONTAIN',
    SYSDATETIME(),
    1,
    @OperationNo,
    @CreateUser,
    SYSDATETIME()
);
";

                        using (SqlCommand cmd =
                               new SqlCommand(
                                   relationSql,
                                   conn,
                                   tran))
                        {
                            cmd.Parameters.Add(
                                "@ParentHUId",
                                SqlDbType.BigInt)
                                .Value = parentHuId;

                            cmd.Parameters.Add(
                                "@ChildHUId",
                                SqlDbType.BigInt)
                                .Value = childHuId;

                            cmd.Parameters.Add(
                                "@OperationNo",
                                SqlDbType.VarChar,
                                50)
                                .Value = operationNo;

                            AddNullableString(
                                cmd,
                                "@CreateUser",
                                SqlDbType.VarChar,
                                50,
                                request.Operator);

                            cmd.ExecuteNonQuery();
                        }


                        InsertOperationLog(
                            conn: conn,
                            tran: tran,
                            operationNo:
                                operationNo,
                            operationType:
                                "ATTACH_HU",
                            huId:
                                parentHuId,
                            relatedHuId:
                                childHuId,
                            businessNo:
                                null,
                            requestId:
                                request.RequestId,
                            operatorCode:
                                request.Operator,
                            detailJson:
                                null,
                            remark:
                                request.Remark);


                        tran.Commit();
                    }
                    catch
                    {
                        tran.Rollback();
                        throw;
                    }
                }
            }
        }


        private static bool HasActiveParent(
            SqlConnection conn,
            SqlTransaction tran,
            long childHuId)
        {
            /*
             * UPDLOCK:
             * 在当前事务期间锁定相关索引记录，
             * 降低两个人同时把一个箱装进两个父HU的风险。
             *
             * 最终数据库还有：
             *
             * UX_WMS_HU_Relation_ActiveChild
             *
             * 双保险。
             */
            const string sql = @"
SELECT COUNT(1)
FROM dbo.WMS_HU_Relation WITH (UPDLOCK, HOLDLOCK)
WHERE ChildHUId = @ChildHUId
AND IsActive = 1;
";

            using (SqlCommand cmd =
                   new SqlCommand(
                       sql,
                       conn,
                       tran))
            {
                cmd.Parameters.Add(
                    "@ChildHUId",
                    SqlDbType.BigInt)
                    .Value = childHuId;

                int count =
                    Convert.ToInt32(
                        cmd.ExecuteScalar());

                return count > 0;
            }
        }

        #endregion


        #region Add Item

        /// <summary>
        /// 给 HU 增加库存明细
        ///
        /// 当前 Service 限制：
        /// 只允许 INNER_PACK 直接挂 Item。
        ///
        /// Repository 不重复承担业务规则。
        /// </summary>
        public long AddItem(
            long huId,
            AddHuItemRequest request,
            string operationNo)
        {
            using (SqlConnection conn =
                   DbConnectionFactory.Create())
            {
                conn.Open();

                using (SqlTransaction tran =
                       conn.BeginTransaction())
                {
                    try
                    {
                        const string sql = @"
INSERT INTO dbo.WMS_HU_Item
(
    HUId,
    MaterialCode,
    MaterialRevision,
    BatchNo,
    SAPBatchNo,
    ProductionOrderNo,
    CustomerCode,
    SalesOrderNo,
    SalesOrderItem,
    DateCode,
    Qty,
    Uom,
    StockStatus,
    IsActive,
    Remark,
    CreateUser,
    CreateTime
)
VALUES
(
    @HUId,
    @MaterialCode,
    @MaterialRevision,
    @BatchNo,
    @SAPBatchNo,
    @ProductionOrderNo,
    @CustomerCode,
    @SalesOrderNo,
    @SalesOrderItem,
    @DateCode,
    @Qty,
    @Uom,
    @StockStatus,
    1,
    @Remark,
    @CreateUser,
    SYSDATETIME()
);

SELECT CAST(SCOPE_IDENTITY() AS BIGINT);
";

                        long itemId;

                        using (SqlCommand cmd =
                               new SqlCommand(
                                   sql,
                                   conn,
                                   tran))
                        {
                            cmd.Parameters.Add(
                                "@HUId",
                                SqlDbType.BigInt)
                                .Value = huId;

                            cmd.Parameters.Add(
                                "@MaterialCode",
                                SqlDbType.VarChar,
                                50)
                                .Value =
                                request.MaterialCode;

                            AddNullableString(
                                cmd,
                                "@MaterialRevision",
                                SqlDbType.VarChar,
                                30,
                                request.MaterialRevision);

                            AddNullableString(
                                cmd,
                                "@BatchNo",
                                SqlDbType.VarChar,
                                100,
                                request.BatchNo);

                            AddNullableString(
                                cmd,
                                "@SAPBatchNo",
                                SqlDbType.VarChar,
                                100,
                                request.SapBatchNo);

                            AddNullableString(
                                cmd,
                                "@ProductionOrderNo",
                                SqlDbType.VarChar,
                                50,
                                request.ProductionOrderNo);

                            AddNullableString(
                                cmd,
                                "@CustomerCode",
                                SqlDbType.VarChar,
                                50,
                                request.CustomerCode);

                            AddNullableString(
                                cmd,
                                "@SalesOrderNo",
                                SqlDbType.VarChar,
                                50,
                                request.SalesOrderNo);

                            AddNullableString(
                                cmd,
                                "@SalesOrderItem",
                                SqlDbType.VarChar,
                                30,
                                request.SalesOrderItem);

                            AddNullableString(
                                cmd,
                                "@DateCode",
                                SqlDbType.VarChar,
                                50,
                                request.DateCode);

                            SqlParameter qtyParameter =
                                cmd.Parameters.Add(
                                    "@Qty",
                                    SqlDbType.Decimal);

                            qtyParameter.Precision = 18;
                            qtyParameter.Scale = 4;
                            qtyParameter.Value =
                                request.Qty;

                            cmd.Parameters.Add(
                                "@Uom",
                                SqlDbType.VarChar,
                                20)
                                .Value =
                                request.Uom;

                            cmd.Parameters.Add(
                                "@StockStatus",
                                SqlDbType.VarChar,
                                20)
                                .Value =
                                request.StockStatus;

                            AddNullableString(
                                cmd,
                                "@Remark",
                                SqlDbType.NVarChar,
                                500,
                                request.Remark);

                            AddNullableString(
                                cmd,
                                "@CreateUser",
                                SqlDbType.VarChar,
                                50,
                                request.Operator);

                            itemId =
                                Convert.ToInt64(
                                    cmd.ExecuteScalar());
                        }


                        InsertOperationLog(
                            conn: conn,
                            tran: tran,
                            operationNo:
                                operationNo,
                            operationType:
                                "ADD_ITEM",
                            huId:
                                huId,
                            relatedHuId:
                                null,
                            businessNo:
                                request.ProductionOrderNo,
                            requestId:
                                request.RequestId,
                            operatorCode:
                                request.Operator,
                            detailJson:
                                null,
                            remark:
                                request.Remark);


                        tran.Commit();

                        return itemId;
                    }
                    catch
                    {
                        tran.Rollback();
                        throw;
                    }
                }
            }
        }

        #endregion


        #region HU Tree

        /// <summary>
        /// 查询指定 HU 为根节点的当前有效包装树
        ///
        /// 返回平铺结果，
        /// Service 负责转成强类型树结构。
        /// </summary>
        public List<HuTreeFlatRow> GetTree(
            long rootHuId)
        {
            const string sql = @"
;WITH HuTree AS
(
    SELECT
        h.Id AS HUId,

        CAST(NULL AS BIGINT)
            AS ParentHUId,

        h.HUCode,

        h.HUType,

        h.PlantCode,

        h.LifecycleStatus,

        h.IsHold,

        CAST(0 AS INT)
            AS LevelNo,

        CAST(
            '/' +
            CAST(h.Id AS VARCHAR(30)) +
            '/'
            AS VARCHAR(MAX)
        ) AS HUPath

    FROM dbo.WMS_HU h

    WHERE h.Id = @RootHUId


    UNION ALL


    SELECT
        child.Id AS HUId,

        relation.ParentHUId,

        child.HUCode,

        child.HUType,

        child.PlantCode,

        child.LifecycleStatus,

        child.IsHold,

        parent.LevelNo + 1,

        CAST(
            parent.HUPath +
            CAST(child.Id AS VARCHAR(30)) +
            '/'
            AS VARCHAR(MAX)
        ) AS HUPath

    FROM HuTree parent

    INNER JOIN dbo.WMS_HU_Relation relation
        ON relation.ParentHUId = parent.HUId
       AND relation.IsActive = 1

    INNER JOIN dbo.WMS_HU child
        ON child.Id = relation.ChildHUId

    WHERE
        parent.HUPath NOT LIKE
        '%/' +
        CAST(child.Id AS VARCHAR(30)) +
        '/%'
)

SELECT
    HUId,
    ParentHUId,
    HUCode,
    HUType,
    PlantCode,
    LifecycleStatus,
    IsHold,
    LevelNo

FROM HuTree

ORDER BY
    LevelNo,
    HUId

OPTION (MAXRECURSION 100);
";

            List<HuTreeFlatRow> list =
                new List<HuTreeFlatRow>();

            using (SqlConnection conn =
                   DbConnectionFactory.Create())
            using (SqlCommand cmd =
                   new SqlCommand(sql, conn))
            {
                cmd.Parameters.Add(
                    "@RootHUId",
                    SqlDbType.BigInt)
                    .Value = rootHuId;

                conn.Open();

                using (SqlDataReader reader =
                       cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        HuTreeFlatRow row =
                            new HuTreeFlatRow
                            {
                                HuId =
                                    Convert.ToInt64(
                                        reader["HUId"]),

                                ParentHuId =
                                    DbLong(
                                        reader[
                                            "ParentHUId"]),

                                HuCode =
                                    Convert.ToString(
                                        reader[
                                            "HUCode"]),

                                HuType =
                                    Convert.ToString(
                                        reader[
                                            "HUType"]),

                                PlantCode =
                                    Convert.ToString(
                                        reader[
                                            "PlantCode"]),

                                LifecycleStatus =
                                    Convert.ToString(
                                        reader[
                                            "LifecycleStatus"]),

                                IsHold =
                                    Convert.ToBoolean(
                                        reader[
                                            "IsHold"]),

                                LevelNo =
                                    Convert.ToInt32(
                                        reader[
                                            "LevelNo"])
                            };

                        list.Add(row);
                    }
                }
            }

            return list;
        }

        #endregion


        #region Items

        /// <summary>
        /// 获取指定 HU 当前有效库存明细
        /// </summary>
        public List<HuItemDto> GetItems(
            long huId)
        {
            const string sql = @"
SELECT
    Id,
    MaterialCode,
    MaterialRevision,
    BatchNo,
    SAPBatchNo,
    ProductionOrderNo,
    DateCode,
    Qty,
    Uom,
    StockStatus
FROM dbo.WMS_HU_Item
WHERE HUId = @HUId
AND IsActive = 1
ORDER BY Id;
";

            List<HuItemDto> list =
                new List<HuItemDto>();

            using (SqlConnection conn =
                   DbConnectionFactory.Create())
            using (SqlCommand cmd =
                   new SqlCommand(sql, conn))
            {
                cmd.Parameters.Add(
                    "@HUId",
                    SqlDbType.BigInt)
                    .Value = huId;

                conn.Open();

                using (SqlDataReader reader =
                       cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        HuItemDto item =
                            new HuItemDto
                            {
                                Id =
                                    Convert.ToInt64(
                                        reader["Id"]),

                                MaterialCode =
                                    Convert.ToString(
                                        reader[
                                            "MaterialCode"]),

                                MaterialRevision =
                                    DbString(
                                        reader[
                                            "MaterialRevision"]),

                                BatchNo =
                                    DbString(
                                        reader[
                                            "BatchNo"]),

                                SapBatchNo =
                                    DbString(
                                        reader[
                                            "SAPBatchNo"]),

                                ProductionOrderNo =
                                    DbString(
                                        reader[
                                            "ProductionOrderNo"]),

                                DateCode =
                                    DbString(
                                        reader[
                                            "DateCode"]),

                                Qty =
                                    Convert.ToDecimal(
                                        reader["Qty"]),

                                Uom =
                                    Convert.ToString(
                                        reader["Uom"]),

                                StockStatus =
                                    Convert.ToString(
                                        reader[
                                            "StockStatus"])
                            };

                        list.Add(item);
                    }
                }
            }

            return list;
        }

        #endregion


        #region Operation Log

        /// <summary>
        /// HU统一操作流水
        ///
        /// 注意：
        /// 此方法接收已有 Connection + Transaction，
        /// 保证业务事实和日志事实同事务提交。
        /// </summary>
        private static void InsertOperationLog(
            SqlConnection conn,
            SqlTransaction tran,
            string operationNo,
            string operationType,
            long huId,
            long? relatedHuId,
            string businessNo,
            string requestId,
            string operatorCode,
            string detailJson,
            string remark)
        {
            const string sql = @"
INSERT INTO dbo.WMS_HU_OperationLog
(
    OperationNo,
    OperationType,
    HUId,
    RelatedHUId,
    BusinessNo,
    RequestId,
    DetailJson,
    Operator,
    OperationTime,
    Remark
)
VALUES
(
    @OperationNo,
    @OperationType,
    @HUId,
    @RelatedHUId,
    @BusinessNo,
    @RequestId,
    @DetailJson,
    @Operator,
    SYSDATETIME(),
    @Remark
);
";

            using (SqlCommand cmd =
                   new SqlCommand(
                       sql,
                       conn,
                       tran))
            {
                cmd.Parameters.Add(
                    "@OperationNo",
                    SqlDbType.VarChar,
                    50)
                    .Value = operationNo;

                cmd.Parameters.Add(
                    "@OperationType",
                    SqlDbType.VarChar,
                    30)
                    .Value = operationType;

                cmd.Parameters.Add(
                    "@HUId",
                    SqlDbType.BigInt)
                    .Value = huId;

                AddNullableLong(
                    cmd,
                    "@RelatedHUId",
                    relatedHuId);

                AddNullableString(
                    cmd,
                    "@BusinessNo",
                    SqlDbType.VarChar,
                    100,
                    businessNo);

                AddNullableString(
                    cmd,
                    "@RequestId",
                    SqlDbType.VarChar,
                    100,
                    requestId);

                AddNullableString(
                    cmd,
                    "@DetailJson",
                    SqlDbType.NVarChar,
                    -1,
                    detailJson);

                AddNullableString(
                    cmd,
                    "@Operator",
                    SqlDbType.VarChar,
                    50,
                    operatorCode);

                AddNullableString(
                    cmd,
                    "@Remark",
                    SqlDbType.NVarChar,
                    500,
                    remark);

                cmd.ExecuteNonQuery();
            }
        }

        #endregion


        #region Mapper

        private static HuDto MapHu(
            IDataRecord reader)
        {
            HuDto entity =
                new HuDto
                {
                    Id =
                        Convert.ToInt64(
                            reader["Id"]),

                    HuCode =
                        Convert.ToString(
                            reader["HUCode"]),

                    HuType =
                        Convert.ToString(
                            reader["HUType"]),

                    PlantCode =
                        Convert.ToString(
                            reader["PlantCode"]),

                    PackageSpecCode =
                        DbString(
                            reader[
                                "PackageSpecCode"]),

                    LifecycleStatus =
                        Convert.ToString(
                            reader[
                                "LifecycleStatus"]),

                    IsHold =
                        Convert.ToBoolean(
                            reader["IsHold"]),

                    HoldReason =
                        DbString(
                            reader[
                                "HoldReason"]),

                    SourceSystem =
                        DbString(
                            reader[
                                "SourceSystem"]),

                    SourceEntityId =
                        DbString(
                            reader[
                                "SourceEntityId"]),

                    SourceBusinessNo =
                        DbString(
                            reader[
                                "SourceBusinessNo"]),

                    Remark =
                        DbString(
                            reader[
                                "Remark"])
                };

            return entity;
        }

        #endregion


        #region SQL Parameter Helper

        private static void AddNullableString(
            SqlCommand cmd,
            string parameterName,
            SqlDbType dbType,
            int size,
            string value)
        {
            SqlParameter parameter;

            if (size == -1)
            {
                parameter =
                    cmd.Parameters.Add(
                        parameterName,
                        dbType,
                        -1);
            }
            else
            {
                parameter =
                    cmd.Parameters.Add(
                        parameterName,
                        dbType,
                        size);
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                parameter.Value =
                    DBNull.Value;
            }
            else
            {
                parameter.Value =
                    value;
            }
        }


        private static void AddNullableLong(
            SqlCommand cmd,
            string parameterName,
            long? value)
        {
            SqlParameter parameter =
                cmd.Parameters.Add(
                    parameterName,
                    SqlDbType.BigInt);

            parameter.Value =
                value.HasValue
                    ? (object)value.Value
                    : DBNull.Value;
        }

        #endregion


        #region DB Value Helper

        private static string DbString(
            object value)
        {
            if (value == null ||
                value == DBNull.Value)
            {
                return null;
            }

            return Convert.ToString(value);
        }


        private static long? DbLong(
            object value)
        {
            if (value == null ||
                value == DBNull.Value)
            {
                return null;
            }

            return Convert.ToInt64(value);
        }

        #endregion
    }
}