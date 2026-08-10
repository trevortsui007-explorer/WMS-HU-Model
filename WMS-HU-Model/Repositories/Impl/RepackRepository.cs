using System;
using System.Data;
using System.Data.SqlClient;
using WMS_HU_Model.Infrastructure.Database;
using WMS_HU_Model.Models.Repack;

namespace WMS_HU_Model.Repositories
{
    public class RepackRepository : IRepackRepository
    {
        public BoxRepackStatusDto GetBoxStatus(long boxHuId)
        {
            const string boxSql = @"
SELECT
    h.Id,
    h.HUCode,
    h.PlantCode,
    h.LifecycleStatus,
    h.IsHold,
    l.LocationCode,
    w.WarehouseCode,
    z.ZoneCode
FROM dbo.WMS_HU h
LEFT JOIN dbo.WMS_HU_CurrentLocation cl
    ON cl.HUId = h.Id
LEFT JOIN dbo.WMS_Location l
    ON l.Id = cl.LocationId
LEFT JOIN dbo.WMS_Warehouse w
    ON w.Id = l.WarehouseId
LEFT JOIN dbo.WMS_Zone z
    ON z.Id = l.ZoneId
WHERE h.Id = @BoxHUId;
";

            const string innerSql = @"
SELECT
    innerHU.HUCode,
    Qty =
    ISNULL
    (
        (
            SELECT SUM(item.Qty)
            FROM dbo.WMS_HU_Item item
            WHERE item.HUId = innerHU.Id
            AND item.IsActive = 1
        ),
        0
    )
FROM dbo.WMS_HU_Relation r
INNER JOIN dbo.WMS_HU innerHU
    ON innerHU.Id = r.ChildHUId
WHERE r.ParentHUId = @BoxHUId
AND r.IsActive = 1
AND innerHU.HUType = 'INNER_PACK'
AND innerHU.LifecycleStatus = 'ACTIVE'
ORDER BY innerHU.HUCode;
";

            using (SqlConnection conn = DbConnectionFactory.Create())
            {
                conn.Open();

                BoxRepackStatusDto result = null;

                using (SqlCommand cmd = new SqlCommand(boxSql, conn))
                {
                    cmd.Parameters.Add("@BoxHUId", SqlDbType.BigInt)
                        .Value = boxHuId;

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (!reader.Read())
                            return null;

                        result = new BoxRepackStatusDto();

                        result.BoxHuId = Convert.ToInt64(reader["Id"]);
                        result.BoxHuCode = Convert.ToString(reader["HUCode"]);
                        result.PlantCode = Convert.ToString(reader["PlantCode"]);
                        result.LifecycleStatus = Convert.ToString(reader["LifecycleStatus"]);
                        result.IsHold = Convert.ToBoolean(reader["IsHold"]);
                        result.LocationCode = DbString(reader["LocationCode"]);
                        result.WarehouseCode = DbString(reader["WarehouseCode"]);
                        result.ZoneCode = DbString(reader["ZoneCode"]);
                    }
                }

                using (SqlCommand cmd = new SqlCommand(innerSql, conn))
                {
                    cmd.Parameters.Add("@BoxHUId", SqlDbType.BigInt)
                        .Value = boxHuId;

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string innerCode = Convert.ToString(reader["HUCode"]);

                            result.InnerPackCodes.Add(innerCode);
                            result.TotalQty += Convert.ToDecimal(reader["Qty"]);
                        }
                    }
                }

                result.InnerPackCount = result.InnerPackCodes.Count;

                return result;
            }
        }

        public TransferInnerResultDto GetTransferByRequestId(string requestId)
        {
            if (string.IsNullOrWhiteSpace(requestId))
                return null;

            const string sql = @"
SELECT TOP 1
    op.OperationNo,
    moveLog.Id AS MoveLogId,
    innerHU.HUCode AS InnerHUCode,
    sourceBox.HUCode AS SourceBoxHUCode,
    targetBox.HUCode AS TargetBoxHUCode,
    fromLoc.LocationCode AS FromLocationCode,
    toLoc.LocationCode AS ToLocationCode,
    moveLog.MoveTime AS OperationTime,

    SourceRemainingInnerCount =
    (
        SELECT COUNT(1)
        FROM dbo.WMS_HU_Relation r1
        INNER JOIN dbo.WMS_HU h1
            ON h1.Id = r1.ChildHUId
        WHERE r1.ParentHUId = sourceBox.Id
        AND r1.IsActive = 1
        AND h1.HUType = 'INNER_PACK'
        AND h1.LifecycleStatus = 'ACTIVE'
    ),

    TargetInnerCount =
    (
        SELECT COUNT(1)
        FROM dbo.WMS_HU_Relation r2
        INNER JOIN dbo.WMS_HU h2
            ON h2.Id = r2.ChildHUId
        WHERE r2.ParentHUId = targetBox.Id
        AND r2.IsActive = 1
        AND h2.HUType = 'INNER_PACK'
        AND h2.LifecycleStatus = 'ACTIVE'
    )
FROM dbo.WMS_HU_OperationLog op
INNER JOIN dbo.WMS_HU innerHU
    ON innerHU.Id = op.HUId
INNER JOIN dbo.WMS_HU targetBox
    ON targetBox.Id = op.RelatedHUId
INNER JOIN dbo.WMS_HU_Relation sourceRel
    ON sourceRel.ChildHUId = innerHU.Id
   AND sourceRel.CloseOperationNo = op.OperationNo
INNER JOIN dbo.WMS_HU sourceBox
    ON sourceBox.Id = sourceRel.ParentHUId
INNER JOIN dbo.WMS_HU_Relation targetRel
    ON targetRel.ChildHUId = innerHU.Id
   AND targetRel.OperationNo = op.OperationNo
INNER JOIN dbo.WMS_HU_MoveLog moveLog
    ON moveLog.MoveNo = op.OperationNo
   AND moveLog.HUId = innerHU.Id
LEFT JOIN dbo.WMS_Location fromLoc
    ON fromLoc.Id = moveLog.FromLocationId
INNER JOIN dbo.WMS_Location toLoc
    ON toLoc.Id = moveLog.ToLocationId
WHERE op.OperationType = 'REPACK_TRANSFER'
AND op.RequestId = @RequestId;
";

            using (SqlConnection conn = DbConnectionFactory.Create())
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.Add("@RequestId", SqlDbType.VarChar, 100)
                    .Value = requestId;

                conn.Open();

                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (!reader.Read())
                        return null;

                    TransferInnerResultDto dto = new TransferInnerResultDto();

                    dto.OperationNo = Convert.ToString(reader["OperationNo"]);
                    dto.MoveLogId = Convert.ToInt64(reader["MoveLogId"]);
                    dto.InnerHuCode = Convert.ToString(reader["InnerHUCode"]);
                    dto.SourceBoxHuCode = Convert.ToString(reader["SourceBoxHUCode"]);
                    dto.TargetBoxHuCode = Convert.ToString(reader["TargetBoxHUCode"]);
                    dto.FromLocationCode = DbString(reader["FromLocationCode"]);
                    dto.ToLocationCode = Convert.ToString(reader["ToLocationCode"]);
                    dto.SourceRemainingInnerCount = Convert.ToInt32(reader["SourceRemainingInnerCount"]);
                    dto.TargetInnerCount = Convert.ToInt32(reader["TargetInnerCount"]);
                    dto.IsSourceEmpty = dto.SourceRemainingInnerCount == 0;
                    dto.IsDuplicate = true;
                    dto.OperationTime = Convert.ToDateTime(reader["OperationTime"]);

                    return dto;
                }
            }
        }

        public TransferInnerPersistenceResult ExecuteTransferInner(TransferInnerCommand command)
        {
            using (SqlConnection conn = DbConnectionFactory.Create())
            {
                conn.Open();

                using (SqlTransaction tran = conn.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        DateTime now = DateTime.Now;

                        //------------------------------------------------
                        // 1. 锁三个HU
                        //------------------------------------------------

                        const string lockHuSql = @"
SELECT
    Id,
    HUCode,
    HUType,
    PlantCode,
    LifecycleStatus,
    IsHold
FROM dbo.WMS_HU
WITH (UPDLOCK, HOLDLOCK)
WHERE Id IN
(
    @SourceBoxHUId,
    @TargetBoxHUId,
    @InnerHUId
);
";

                        int huCount = 0;

                        using (SqlCommand cmd = new SqlCommand(lockHuSql, conn, tran))
                        {
                            cmd.Parameters.Add("@SourceBoxHUId", SqlDbType.BigInt)
                                .Value = command.SourceBoxHuId;

                            cmd.Parameters.Add("@TargetBoxHUId", SqlDbType.BigInt)
                                .Value = command.TargetBoxHuId;

                            cmd.Parameters.Add("@InnerHUId", SqlDbType.BigInt)
                                .Value = command.InnerHuId;

                            using (SqlDataReader reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    huCount++;

                                    string status = Convert.ToString(reader["LifecycleStatus"]);
                                    bool isHold = Convert.ToBoolean(reader["IsHold"]);

                                    if (status != "ACTIVE")
                                    {
                                        tran.Rollback();
                                        return TransferInnerPersistenceResult.Fail("参与Repack的HU状态已变化。");
                                    }

                                    if (isHold)
                                    {
                                        tran.Rollback();
                                        return TransferInnerPersistenceResult.Fail("参与Repack的HU已被冻结。");
                                    }
                                }
                            }
                        }

                        if (huCount != 3)
                        {
                            tran.Rollback();
                            return TransferInnerPersistenceResult.Fail("Repack HU数据不完整。");
                        }

                        //------------------------------------------------
                        // 2. Source → Inner Active Relation
                        //------------------------------------------------

                        long sourceRelationId;

                        const string sourceRelationSql = @"
SELECT Id
FROM dbo.WMS_HU_Relation
WITH (UPDLOCK, HOLDLOCK)
WHERE ParentHUId = @SourceBoxHUId
AND ChildHUId = @InnerHUId
AND IsActive = 1;
";

                        using (SqlCommand cmd = new SqlCommand(sourceRelationSql, conn, tran))
                        {
                            cmd.Parameters.Add("@SourceBoxHUId", SqlDbType.BigInt)
                                .Value = command.SourceBoxHuId;

                            cmd.Parameters.Add("@InnerHUId", SqlDbType.BigInt)
                                .Value = command.InnerHuId;

                            object value = cmd.ExecuteScalar();

                            if (value == null || value == DBNull.Value)
                            {
                                tran.Rollback();
                                return TransferInnerPersistenceResult.Fail("内包当前已不属于源外箱，请刷新数据。");
                            }

                            sourceRelationId = Convert.ToInt64(value);
                        }

                        //------------------------------------------------
                        // 3. Inner不能已有DirectLocation
                        //------------------------------------------------

                        const string innerLocationSql = @"
SELECT COUNT(1)
FROM dbo.WMS_HU_CurrentLocation
WITH (UPDLOCK, HOLDLOCK)
WHERE HUId = @InnerHUId;
";

                        using (SqlCommand cmd = new SqlCommand(innerLocationSql, conn, tran))
                        {
                            cmd.Parameters.Add("@InnerHUId", SqlDbType.BigInt)
                                .Value = command.InnerHuId;

                            int count = Convert.ToInt32(cmd.ExecuteScalar());

                            if (count > 0)
                            {
                                tran.Rollback();
                                return TransferInnerPersistenceResult.Fail("内包已经存在直接货位，不能执行箱到箱Transfer。");
                            }
                        }

                        //------------------------------------------------
                        // 4. Source/Target直接位置重新锁定
                        //------------------------------------------------

                        long sourceLocationId;
                        string sourceLocationCode;
                        long targetLocationId;
                        string targetLocationCode;

                        ReadBoxLocation(
                            conn,
                            tran,
                            command.SourceBoxHuId,
                            out sourceLocationId,
                            out sourceLocationCode);

                        ReadBoxLocation(
                            conn,
                            tran,
                            command.TargetBoxHuId,
                            out targetLocationId,
                            out targetLocationCode);

                        if (sourceLocationId != command.FromLocationId)
                        {
                            tran.Rollback();
                            return TransferInnerPersistenceResult.Fail("源外箱位置已经发生变化。");
                        }

                        if (targetLocationId != command.ToLocationId)
                        {
                            tran.Rollback();
                            return TransferInnerPersistenceResult.Fail("目标外箱位置已经发生变化。");
                        }

                        //------------------------------------------------
                        // 5. 关闭旧Relation
                        //------------------------------------------------

                        const string closeRelationSql = @"
UPDATE dbo.WMS_HU_Relation
SET
    EffectiveTo = @Now,
    IsActive = 0,
    CloseOperationNo = @OperationNo,
    CloseUser = @Operator,
    CloseReason = N'REPACK_TRANSFER'
WHERE Id = @RelationId
AND IsActive = 1;
";

                        using (SqlCommand cmd = new SqlCommand(closeRelationSql, conn, tran))
                        {
                            cmd.Parameters.Add("@Now", SqlDbType.DateTime2)
                                .Value = now;

                            cmd.Parameters.Add("@OperationNo", SqlDbType.VarChar, 50)
                                .Value = command.OperationNo;

                            cmd.Parameters.Add("@Operator", SqlDbType.VarChar, 50)
                                .Value = DbValue(command.Operator);

                            cmd.Parameters.Add("@RelationId", SqlDbType.BigInt)
                                .Value = sourceRelationId;

                            if (cmd.ExecuteNonQuery() != 1)
                            {
                                tran.Rollback();
                                return TransferInnerPersistenceResult.Fail("关闭源包装关系失败。");
                            }
                        }

                        //------------------------------------------------
                        // 6. 建立 Target → Inner Relation
                        //------------------------------------------------

                        const string createRelationSql = @"
INSERT INTO dbo.WMS_HU_Relation
(
    ParentHUId,
    ChildHUId,
    RelationType,
    EffectiveFrom,
    IsActive,
    OperationNo,
    CreateUser
)
VALUES
(
    @TargetBoxHUId,
    @InnerHUId,
    'CONTAIN',
    @Now,
    1,
    @OperationNo,
    @Operator
);
";

                        using (SqlCommand cmd = new SqlCommand(createRelationSql, conn, tran))
                        {
                            cmd.Parameters.Add("@TargetBoxHUId", SqlDbType.BigInt)
                                .Value = command.TargetBoxHuId;

                            cmd.Parameters.Add("@InnerHUId", SqlDbType.BigInt)
                                .Value = command.InnerHuId;

                            cmd.Parameters.Add("@Now", SqlDbType.DateTime2)
                                .Value = now;

                            cmd.Parameters.Add("@OperationNo", SqlDbType.VarChar, 50)
                                .Value = command.OperationNo;

                            cmd.Parameters.Add("@Operator", SqlDbType.VarChar, 50)
                                .Value = DbValue(command.Operator);

                            cmd.ExecuteNonQuery();
                        }

                        //------------------------------------------------
                        // 7. Inner MoveLog
                        //------------------------------------------------

                        long moveLogId;

                        const string moveSql = @"
INSERT INTO dbo.WMS_HU_MoveLog
(
    MoveNo,
    HUId,
    FromLocationId,
    ToLocationId,
    MoveType,
    ScannedLocationCode,
    BusinessNo,
    RequestId,
    Operator,
    MoveTime,
    Remark
)
OUTPUT INSERTED.Id
VALUES
(
    @MoveNo,
    @HUId,
    @FromLocationId,
    @ToLocationId,
    'REPACK_TRANSFER',
    @ToLocationCode,
    @BusinessNo,
    @RequestId,
    @Operator,
    @MoveTime,
    @Remark
);
";

                        using (SqlCommand cmd = new SqlCommand(moveSql, conn, tran))
                        {
                            cmd.Parameters.Add("@MoveNo", SqlDbType.VarChar, 50)
                                .Value = command.OperationNo;

                            cmd.Parameters.Add("@HUId", SqlDbType.BigInt)
                                .Value = command.InnerHuId;

                            cmd.Parameters.Add("@FromLocationId", SqlDbType.BigInt)
                                .Value = sourceLocationId;

                            cmd.Parameters.Add("@ToLocationId", SqlDbType.BigInt)
                                .Value = targetLocationId;

                            cmd.Parameters.Add("@ToLocationCode", SqlDbType.VarChar, 100)
                                .Value = targetLocationCode;

                            cmd.Parameters.Add("@BusinessNo", SqlDbType.VarChar, 100)
                                .Value = DbValue(command.BusinessNo);

                            cmd.Parameters.Add("@RequestId", SqlDbType.VarChar, 100)
                                .Value = command.RequestId;

                            cmd.Parameters.Add("@Operator", SqlDbType.VarChar, 50)
                                .Value = DbValue(command.Operator);

                            cmd.Parameters.Add("@MoveTime", SqlDbType.DateTime2)
                                .Value = now;

                            cmd.Parameters.Add("@Remark", SqlDbType.NVarChar, 500)
                                .Value = DbValue(command.Remark);

                            moveLogId = Convert.ToInt64(cmd.ExecuteScalar());
                        }

                        //------------------------------------------------
                        // 8. OperationLog
                        //------------------------------------------------

                        const string operationSql = @"
INSERT INTO dbo.WMS_HU_OperationLog
(
    OperationNo,
    OperationType,
    HUId,
    RelatedHUId,
    BusinessNo,
    RequestId,
    Operator,
    OperationTime,
    Remark
)
VALUES
(
    @OperationNo,
    'REPACK_TRANSFER',
    @InnerHUId,
    @TargetBoxHUId,
    @BusinessNo,
    @RequestId,
    @Operator,
    @OperationTime,
    @Remark
);
";

                        using (SqlCommand cmd = new SqlCommand(operationSql, conn, tran))
                        {
                            cmd.Parameters.Add("@OperationNo", SqlDbType.VarChar, 50)
                                .Value = command.OperationNo;

                            cmd.Parameters.Add("@InnerHUId", SqlDbType.BigInt)
                                .Value = command.InnerHuId;

                            cmd.Parameters.Add("@TargetBoxHUId", SqlDbType.BigInt)
                                .Value = command.TargetBoxHuId;

                            cmd.Parameters.Add("@BusinessNo", SqlDbType.VarChar, 100)
                                .Value = DbValue(command.BusinessNo);

                            cmd.Parameters.Add("@RequestId", SqlDbType.VarChar, 100)
                                .Value = command.RequestId;

                            cmd.Parameters.Add("@Operator", SqlDbType.VarChar, 50)
                                .Value = DbValue(command.Operator);

                            cmd.Parameters.Add("@OperationTime", SqlDbType.DateTime2)
                                .Value = now;

                            cmd.Parameters.Add("@Remark", SqlDbType.NVarChar, 500)
                                .Value = DbValue(command.Remark);

                            cmd.ExecuteNonQuery();
                        }

                        //------------------------------------------------
                        // 9. 统计
                        //------------------------------------------------

                        int sourceCount = CountInner(conn, tran, command.SourceBoxHuId);
                        int targetCount = CountInner(conn, tran, command.TargetBoxHuId);

                        tran.Commit();

                        TransferInnerResultDto result = new TransferInnerResultDto();

                        result.OperationNo = command.OperationNo;
                        result.MoveLogId = moveLogId;
                        result.InnerHuCode = command.InnerHuCode;
                        result.SourceBoxHuCode = command.SourceBoxHuCode;
                        result.TargetBoxHuCode = command.TargetBoxHuCode;
                        result.FromLocationCode = sourceLocationCode;
                        result.ToLocationCode = targetLocationCode;
                        result.SourceRemainingInnerCount = sourceCount;
                        result.TargetInnerCount = targetCount;
                        result.IsSourceEmpty = sourceCount == 0;
                        result.IsDuplicate = false;
                        result.OperationTime = now;

                        return TransferInnerPersistenceResult.Success(result);
                    }
                    catch (SqlException ex)
                    {
                        if (tran.Connection != null)
                            tran.Rollback();

                        //------------------------------------------------
                        // RequestId唯一索引命中
                        //------------------------------------------------

                        if (ex.Number == 2601 || ex.Number == 2627)
                        {
                            TransferInnerResultDto duplicate = GetTransferByRequestId(command.RequestId);

                            if (duplicate != null)
                            {
                                bool sameRequest =
                                    string.Equals(
                                        duplicate.InnerHuCode,
                                        command.InnerHuCode,
                                        StringComparison.OrdinalIgnoreCase)
                                    &&
                                    string.Equals(
                                        duplicate.SourceBoxHuCode,
                                        command.SourceBoxHuCode,
                                        StringComparison.OrdinalIgnoreCase)
                                    &&
                                    string.Equals(
                                        duplicate.TargetBoxHuCode,
                                        command.TargetBoxHuCode,
                                        StringComparison.OrdinalIgnoreCase);

                                if (sameRequest)
                                {
                                    duplicate.IsDuplicate = true;
                                    return TransferInnerPersistenceResult.Success(duplicate);
                                }
                            }
                        }

                        throw;
                    }
                    catch
                    {
                        if (tran.Connection != null)
                            tran.Rollback();

                        throw;
                    }
                }
            }
        }

        private static void ReadBoxLocation(
            SqlConnection conn,
            SqlTransaction tran,
            long boxHuId,
            out long locationId,
            out string locationCode)
        {
            const string sql = @"
SELECT
    cl.LocationId,
    l.LocationCode
FROM dbo.WMS_HU_CurrentLocation cl
WITH (UPDLOCK, HOLDLOCK)
INNER JOIN dbo.WMS_Location l
    ON l.Id = cl.LocationId
WHERE cl.HUId = @BoxHUId;
";

            using (SqlCommand cmd = new SqlCommand(sql, conn, tran))
            {
                cmd.Parameters.Add("@BoxHUId", SqlDbType.BigInt)
                    .Value = boxHuId;

                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (!reader.Read())
                        throw new InvalidOperationException("外箱没有直接货位，无法执行Repack。");

                    locationId = Convert.ToInt64(reader["LocationId"]);
                    locationCode = Convert.ToString(reader["LocationCode"]);
                }
            }
        }

        private static int CountInner(SqlConnection conn, SqlTransaction tran, long boxHuId)
        {
            const string sql = @"
SELECT COUNT(1)
FROM dbo.WMS_HU_Relation r
INNER JOIN dbo.WMS_HU h
    ON h.Id = r.ChildHUId
WHERE r.ParentHUId = @BoxHUId
AND r.IsActive = 1
AND h.HUType = 'INNER_PACK'
AND h.LifecycleStatus = 'ACTIVE';
";

            using (SqlCommand cmd = new SqlCommand(sql, conn, tran))
            {
                cmd.Parameters.Add("@BoxHUId", SqlDbType.BigInt)
                    .Value = boxHuId;

                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        private static object DbValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? (object)DBNull.Value : value;
        }

        private static string DbString(object value)
        {
            return value == DBNull.Value ? null : Convert.ToString(value);
        }
    }
}