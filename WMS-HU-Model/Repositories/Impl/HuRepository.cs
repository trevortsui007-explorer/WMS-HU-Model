using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using Dapper;
using WMS_HU_Model.Infrastructure.Database;
using WMS_HU_Model.Models.HU;
using WMS_HU_Model.Models.WeekCode;

namespace WMS_HU_Model.Repositories
{
    public class HuRepository : IHuRepository
    {
        #region 查询 HU

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

            return DbExecutor.QueryFirstOrDefault<HuDto>(
                sql,
                new { HUCode = huCode });
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

            return DbExecutor.ExecuteScalar<long?>(
                sql,
                new { HUCode = huCode });
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

            return DbExecutor.ExecuteScalar<string>(
                sql,
                new { HUId = huId });
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

            return DbExecutor.ExecuteScalar<string>(
                sql,
                new { HUId = huId });
        }

        /// <summary>
        /// 根据包装规格编码获取 PackageSpecId
        /// </summary>
        public long? GetPackageSpecId(string packageSpecCode)
        {
            const string sql = @"
SELECT Id
FROM dbo.WMS_PackageSpec
WHERE PackageSpecCode = @PackageSpecCode
AND IsActive = 1;
";

            return DbExecutor.ExecuteScalar<long?>(
                sql,
                new { PackageSpecCode = packageSpecCode });
        }

        #endregion

        #region 创建 HU

        /// <summary>
        /// 创建 HU
        ///
        /// 同一事务内：
        /// 1. 创建 WMS_HU
        /// 2. 写 CREATE_HU 操作流水
        /// </summary>
        public long Create(CreateHuRequest request, long? packageSpecId, string operationNo)
        {
            using (SqlConnection conn = DbConnectionFactory.Create())
            {
                conn.Open();

                using (SqlTransaction tran = conn.BeginTransaction())
                {
                    try
                    {
                        long huId = InsertHu(conn, tran, request, packageSpecId);

                        InsertOperationLog(
                            conn: conn,
                            tran: tran,
                            operationNo: operationNo,
                            operationType: "CREATE_HU",
                            huId: huId,
                            relatedHuId: null,
                            businessNo: request.SourceBusinessNo,
                            requestId: request.RequestId,
                            operatorCode: request.Operator,
                            detailJson: null,
                            remark: request.Remark);

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

            DynamicParameters parameters = new DynamicParameters();
            parameters.Add("@HUCode", request.HuCode, DbType.AnsiString, size: 100);
            parameters.Add("@HUType", request.HuType, DbType.AnsiString, size: 30);
            parameters.Add("@PlantCode", request.PlantCode, DbType.AnsiString, size: 20);
            parameters.Add("@PackageSpecId", packageSpecId, DbType.Int64);
            parameters.Add("@SourceSystem", DbParameterValue.StringOrNull(request.SourceSystem), DbType.AnsiString, size: 30);
            parameters.Add("@SourceEntityId", DbParameterValue.StringOrNull(request.SourceEntityId), DbType.AnsiString, size: 100);
            parameters.Add("@SourceBusinessNo", DbParameterValue.StringOrNull(request.SourceBusinessNo), DbType.AnsiString, size: 100);
            parameters.Add("@Remark", DbParameterValue.StringOrNull(request.Remark), DbType.String, size: 500);
            parameters.Add("@CreateUser", DbParameterValue.StringOrNull(request.Operator), DbType.AnsiString, size: 50);

            return DbExecutor.ExecuteScalar<long>(conn, tran, sql, parameters);
        }

        #endregion

        #region 关系查询

        /// <summary>
        /// 判断子 HU 是否已经存在有效父 HU
        /// </summary>
        public bool HasActiveParent(long childHuId)
        {
            const string sql = @"
SELECT COUNT(1)
FROM dbo.WMS_HU_Relation
WHERE ChildHUId = @ChildHUId
AND IsActive = 1;
";

            return DbExecutor.ExecuteScalar<int>(
                sql,
                new { ChildHUId = childHuId }) > 0;
        }

        /// <summary>
        /// 获取子 HU 当前有效父 HU
        /// </summary>
        public HuDto GetActiveParent(long childHuId)
        {
            const string sql = @"
SELECT TOP 1
    parent.Id,
    parent.HUCode,
    parent.HUType,
    parent.PlantCode,
    ps.PackageSpecCode,
    parent.LifecycleStatus,
    parent.IsHold,
    parent.HoldReason,
    parent.SourceSystem,
    parent.SourceEntityId,
    parent.SourceBusinessNo,
    parent.Remark
FROM dbo.WMS_HU_Relation r
INNER JOIN dbo.WMS_HU parent
    ON parent.Id = r.ParentHUId
LEFT JOIN dbo.WMS_PackageSpec ps
    ON ps.Id = parent.PackageSpecId
WHERE r.ChildHUId = @ChildHUId
AND r.IsActive = 1;
";

            return DbExecutor.QueryFirstOrDefault<HuDto>(
                sql,
                new { ChildHUId = childHuId });
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
        public bool WouldCreateCycle(long parentHuId, long childHuId)
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
    WHERE d.HUPath NOT LIKE
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

            return DbExecutor.ExecuteScalar<int>(
                sql,
                new
                {
                    ParentHUId = parentHuId,
                    ChildHUId = childHuId
                }) == 1;
        }

        #endregion

        #region 建立 HU 关系

        /// <summary>
        /// 建立 Parent -> Child 包装关系
        ///
        /// 同一事务：
        /// 1. INSERT WMS_HU_Relation
        /// 2. INSERT WMS_HU_OperationLog
        /// </summary>
        public void Attach(long parentHuId, long childHuId, AttachHuRequest request, string operationNo)
        {
            using (SqlConnection conn = DbConnectionFactory.Create())
            {
                conn.Open();

                using (SqlTransaction tran = conn.BeginTransaction(IsolationLevel.ReadCommitted))
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
                        if (HasActiveParent(conn, tran, childHuId))
                            throw new InvalidOperationException("子HU已经存在有效父HU，请先拆出后再重新建立关系。");

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

                        DynamicParameters relationParameters = new DynamicParameters();
                        relationParameters.Add("@ParentHUId", parentHuId, DbType.Int64);
                        relationParameters.Add("@ChildHUId", childHuId, DbType.Int64);
                        relationParameters.Add("@OperationNo", operationNo, DbType.AnsiString, size: 50);
                        relationParameters.Add("@CreateUser", DbParameterValue.StringOrNull(request.Operator), DbType.AnsiString, size: 50);

                        DbExecutor.Execute(conn, tran, relationSql, relationParameters);

                        InsertOperationLog(
                            conn: conn,
                            tran: tran,
                            operationNo: operationNo,
                            operationType: "ATTACH_HU",
                            huId: parentHuId,
                            relatedHuId: childHuId,
                            businessNo: null,
                            requestId: request.RequestId,
                            operatorCode: request.Operator,
                            detailJson: null,
                            remark: request.Remark);

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

            return DbExecutor.ExecuteScalar<int>(
                conn,
                tran,
                sql,
                new { ChildHUId = childHuId }) > 0;
        }

        #endregion

        #region 增加库存明细

        /// <summary>
        /// 给 HU 增加库存明细
        ///
        /// 当前 Service 限制：
        /// 只允许 INNER_PACK 直接挂 Item。
        ///
        /// Repository 不重复承担业务规则。
        /// </summary>
        public long AddItem(long huId, AddHuItemRequest request, WeekCodeSnapshot weekCode, string operationNo)
        {
            using (SqlConnection conn = DbConnectionFactory.Create())
            {
                conn.Open();

                using (SqlTransaction tran = conn.BeginTransaction())
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
    WeekFormatCode,
    WeekYear,
    WeekNo,
    WeekSortKey,
    WeekRuleSource,
    WeekRuleValue,
    WeekResolvedTime,
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
    @WeekFormatCode,
    @WeekYear,
    @WeekNo,
    @WeekSortKey,
    @WeekRuleSource,
    @WeekRuleValue,
    @WeekResolvedTime,
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

                        DynamicParameters parameters = new DynamicParameters();
                        parameters.Add("@HUId", huId, DbType.Int64);
                        parameters.Add("@MaterialCode", request.MaterialCode, DbType.AnsiString, size: 50);
                        parameters.Add("@MaterialRevision", DbParameterValue.StringOrNull(request.MaterialRevision), DbType.AnsiString, size: 30);
                        parameters.Add("@BatchNo", DbParameterValue.StringOrNull(request.BatchNo), DbType.AnsiString, size: 100);
                        parameters.Add("@SAPBatchNo", DbParameterValue.StringOrNull(request.SapBatchNo), DbType.AnsiString, size: 100);
                        parameters.Add("@ProductionOrderNo", DbParameterValue.StringOrNull(request.ProductionOrderNo), DbType.AnsiString, size: 50);
                        parameters.Add("@CustomerCode", DbParameterValue.StringOrNull(request.CustomerCode), DbType.AnsiString, size: 50);
                        parameters.Add("@SalesOrderNo", DbParameterValue.StringOrNull(request.SalesOrderNo), DbType.AnsiString, size: 50);
                        parameters.Add("@SalesOrderItem", DbParameterValue.StringOrNull(request.SalesOrderItem), DbType.AnsiString, size: 30);
                        parameters.Add("@DateCode", DbParameterValue.StringOrNull(request.DateCode), DbType.AnsiString, size: 50);
                        parameters.Add("@WeekFormatCode", DbParameterValue.StringOrNull(weekCode == null ? null : weekCode.WeekFormatCode), DbType.AnsiString, size: 30);
                        parameters.Add("@WeekYear", weekCode == null ? null : (object)weekCode.WeekYear, DbType.Int16);
                        parameters.Add("@WeekNo", weekCode == null ? null : (object)weekCode.WeekNo, DbType.Byte);
                        parameters.Add("@WeekSortKey", weekCode == null ? null : (object)weekCode.WeekSortKey, DbType.Int32);
                        parameters.Add("@WeekRuleSource", DbParameterValue.StringOrNull(weekCode == null ? null : weekCode.WeekRuleSource), DbType.AnsiString, size: 30);
                        parameters.Add("@WeekRuleValue", DbParameterValue.StringOrNull(weekCode == null ? null : weekCode.WeekRuleValue), DbType.AnsiString, size: 100);
                        parameters.Add("@WeekResolvedTime", weekCode == null ? null : (object)weekCode.WeekResolvedTime, DbType.DateTime2);
                        parameters.Add("@Qty", request.Qty, DbType.Decimal, precision: 18, scale: 4);
                        parameters.Add("@Uom", request.Uom, DbType.AnsiString, size: 20);
                        parameters.Add("@StockStatus", request.StockStatus, DbType.AnsiString, size: 20);
                        parameters.Add("@Remark", DbParameterValue.StringOrNull(request.Remark), DbType.String, size: 500);
                        parameters.Add("@CreateUser", DbParameterValue.StringOrNull(request.Operator), DbType.AnsiString, size: 50);

                        long itemId = DbExecutor.ExecuteScalar<long>(conn, tran, sql, parameters);

                        InsertOperationLog(
                            conn: conn,
                            tran: tran,
                            operationNo: operationNo,
                            operationType: "ADD_ITEM",
                            huId: huId,
                            relatedHuId: null,
                            businessNo: request.ProductionOrderNo,
                            requestId: request.RequestId,
                            operatorCode: request.Operator,
                            detailJson: null,
                            remark: request.Remark);

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

        #region HU 树查询

        /// <summary>
        /// 查询指定 HU 为根节点的当前有效包装树
        ///
        /// 返回平铺结果，
        /// Service 负责转成强类型树结构。
        /// </summary>
        public List<HuTreeFlatRow> GetTree(long rootHuId)
        {
            const string sql = @"
;WITH HuTree AS
(
    SELECT
        h.Id AS HUId,
        CAST(NULL AS BIGINT) AS ParentHUId,
        h.HUCode,
        h.HUType,
        h.PlantCode,
        h.LifecycleStatus,
        h.IsHold,
        CAST(0 AS INT) AS LevelNo,
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
    WHERE parent.HUPath NOT LIKE
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

            return DbExecutor.Query<HuTreeFlatRow>(
                sql,
                new { RootHUId = rootHuId });
        }

        #endregion

        #region 库存明细

        /// <summary>
        /// 获取指定 HU 当前有效库存明细
        /// </summary>
        public List<HuItemDto> GetItems(long huId)
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
    WeekFormatCode,
    WeekYear,
    WeekNo,
    WeekSortKey,
    WeekRuleSource,
    WeekRuleValue,
    WeekResolvedTime,
    Qty,
    Uom,
    StockStatus
FROM dbo.WMS_HU_Item
WHERE HUId = @HUId
AND IsActive = 1
ORDER BY Id;
";

            return DbExecutor.Query<HuItemDto>(
                sql,
                new { HUId = huId });
        }

        #endregion

        #region 操作日志

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
            HuOperationLogWriter.Insert(
                conn,
                tran,
                operationNo,
                operationType,
                huId,
                relatedHuId,
                businessNo,
                requestId,
                operatorCode,
                operationTime: null,
                detailJson: detailJson,
                remark: remark);
        }

        #endregion

    }
}
