using System;
using System.Data;
using System.Data.SqlClient;
using Dapper;
using WMS_HU_Model.Infrastructure.Database;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.HU;

namespace WMS_HU_Model.Repositories
{
    public class HuHoldRepository : IHuHoldRepository
    {
        public OperationResult<HuHoldResultDto> Hold(string huCode, HoldHuRequest request)
        {
            using (SqlConnection conn = DbConnectionFactory.Create())
            {
                conn.Open();
                using (SqlTransaction tran = conn.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        HuHoldResultDto duplicate = ReadByRequest(conn, tran, request.RequestId, true);
                        if (duplicate != null)
                        {
                            if (!string.Equals(duplicate.HuCode, huCode, StringComparison.Ordinal))
                            {
                                tran.Rollback();
                                return OperationResult<HuHoldResultDto>.Fail("RequestId已经用于Hold其他HU。");
                            }
                            duplicate.IsDuplicate = true;
                            tran.Commit();
                            return OperationResult<HuHoldResultDto>.Success(duplicate, "重复请求，返回原Hold结果。");
                        }

                        if (IsRequestIdUsed(conn, tran, request.RequestId))
                        {
                            tran.Rollback();
                            return OperationResult<HuHoldResultDto>.Fail("RequestId已经被其他WMS业务使用。");
                        }

                        HuHoldRow hu = ReadHuForUpdate(conn, tran, huCode);
                        if (hu == null)
                        {
                            tran.Rollback();
                            return OperationResult<HuHoldResultDto>.Fail("HU不存在。");
                        }
                        if (hu.HuType != "OUTER_BOX")
                        {
                            tran.Rollback();
                            return OperationResult<HuHoldResultDto>.Fail("Hold API仅支持OUTER_BOX。");
                        }
                        if (hu.IsHold)
                        {
                            tran.Rollback();
                            return OperationResult<HuHoldResultDto>.Fail("HU已经处于Hold状态。");
                        }

                        DateTime now = DateTime.Now;
                        string token = Guid.NewGuid().ToString("N").Substring(0, 12).ToUpperInvariant();
                        string holdNo = "HD" + now.ToString("yyyyMMddHHmmss") + token;
                        conn.Execute(@"
INSERT INTO dbo.WMS_FG_HU_HoldLog
(
    HoldNo, HUId, HoldType, HoldReasonCode, HoldReason,
    IsActive, HoldDt, HoldBy, HoldRequestId
)
VALUES
(
    @HoldNo, @HUId, @HoldType, @ReasonCode, @Reason,
    1, @Now, @Operator, @RequestId
);

UPDATE dbo.WMS_FG_HU
SET IsHold = 1, LastEditBy = @Operator, LastEditDt = @Now
WHERE Id = @HUId AND IsHold = 0;",
                            new
                            {
                                HoldNo = holdNo,
                                HUId = hu.Id,
                                request.HoldType,
                                request.ReasonCode,
                                request.Reason,
                                Now = now,
                                request.Operator,
                                request.RequestId
                            },
                            tran);

                        HuOperationLogWriter.Insert(
                            conn, tran, "OP" + now.ToString("yyyyMMddHHmmss") + token,
                            "HU_HOLD", hu.Id, null, holdNo, request.RequestId,
                            request.Operator, now, null, request.Reason);
                        tran.Commit();
                        return OperationResult<HuHoldResultDto>.Success(new HuHoldResultDto
                        {
                            HuCode = huCode,
                            HoldNo = holdNo,
                            IsHold = true
                        });
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

        public OperationResult<HuHoldResultDto> Release(string huCode, ReleaseHuRequest request)
        {
            using (SqlConnection conn = DbConnectionFactory.Create())
            {
                conn.Open();
                using (SqlTransaction tran = conn.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        HuHoldResultDto duplicate = ReadByRequest(conn, tran, request.RequestId, false);
                        if (duplicate != null)
                        {
                            if (!string.Equals(duplicate.HuCode, huCode, StringComparison.Ordinal))
                            {
                                tran.Rollback();
                                return OperationResult<HuHoldResultDto>.Fail("RequestId已经用于Release其他HU。");
                            }
                            duplicate.IsDuplicate = true;
                            tran.Commit();
                            return OperationResult<HuHoldResultDto>.Success(duplicate, "重复请求，返回原Release结果。");
                        }

                        if (IsRequestIdUsed(conn, tran, request.RequestId))
                        {
                            tran.Rollback();
                            return OperationResult<HuHoldResultDto>.Fail("RequestId已经被其他WMS业务使用。");
                        }

                        HuHoldRow hu = ReadHuForUpdate(conn, tran, huCode);
                        if (hu == null)
                        {
                            tran.Rollback();
                            return OperationResult<HuHoldResultDto>.Fail("HU不存在。");
                        }
                        if (hu.HuType != "OUTER_BOX")
                        {
                            tran.Rollback();
                            return OperationResult<HuHoldResultDto>.Fail("Release API仅支持OUTER_BOX。");
                        }

                        const string activeSql = @"
SELECT TOP 1 Id, HoldNo
FROM dbo.WMS_FG_HU_HoldLog WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
WHERE HUId = @HUId AND IsActive = 1;";
                        ActiveHoldRow hold = conn.QueryFirstOrDefault<ActiveHoldRow>(
                            activeSql,
                            new { HUId = hu.Id },
                            tran);
                        if (!hu.IsHold || hold == null)
                        {
                            tran.Rollback();
                            return OperationResult<HuHoldResultDto>.Fail("HU当前没有有效Hold记录。");
                        }

                        DateTime now = DateTime.Now;
                        conn.Execute(@"
UPDATE dbo.WMS_FG_HU_HoldLog
SET IsActive = 0, ReleaseDt = @Now, ReleaseBy = @Operator,
    ReleaseReason = @Reason, ReleaseRequestId = @RequestId
WHERE Id = @HoldLogId AND IsActive = 1;

UPDATE dbo.WMS_FG_HU
SET IsHold = 0, LastEditBy = @Operator, LastEditDt = @Now
WHERE Id = @HUId AND IsHold = 1;",
                            new
                            {
                                Now = now,
                                request.Operator,
                                request.Reason,
                                request.RequestId,
                                HoldLogId = hold.Id,
                                HUId = hu.Id
                            },
                            tran);
                        string token = Guid.NewGuid().ToString("N").Substring(0, 12).ToUpperInvariant();
                        HuOperationLogWriter.Insert(
                            conn, tran, "OP" + now.ToString("yyyyMMddHHmmss") + token,
                            "HU_RELEASE", hu.Id, null, hold.HoldNo, request.RequestId,
                            request.Operator, now, null, request.Reason);
                        tran.Commit();
                        return OperationResult<HuHoldResultDto>.Success(new HuHoldResultDto
                        {
                            HuCode = huCode,
                            HoldNo = hold.HoldNo,
                            IsHold = false
                        });
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

        private static HuHoldRow ReadHuForUpdate(SqlConnection conn, SqlTransaction tran, string huCode)
        {
            const string sql = @"
SELECT TOP 1 Id, HUCode, HUType, IsHold
FROM dbo.WMS_FG_HU WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
WHERE HUCode = @HUCode;";
            return conn.QueryFirstOrDefault<HuHoldRow>(sql, new { HUCode = huCode }, tran);
        }

        private static HuHoldResultDto ReadByRequest(
            SqlConnection conn,
            SqlTransaction tran,
            string requestId,
            bool holdAction)
        {
            string condition = holdAction ? "log.HoldRequestId = @RequestId" : "log.ReleaseRequestId = @RequestId";
            string sql = @"
SELECT TOP 1 hu.HUCode AS HuCode, log.HoldNo, log.IsActive AS IsHold
FROM dbo.WMS_FG_HU_HoldLog log
INNER JOIN dbo.WMS_FG_HU hu ON hu.Id = log.HUId
WHERE " + condition + ";";
            return conn.QueryFirstOrDefault<HuHoldResultDto>(sql, new { RequestId = requestId }, tran);
        }

        private static bool IsRequestIdUsed(SqlConnection conn, SqlTransaction tran, string requestId)
        {
            const string sql = @"
SELECT CASE WHEN
    EXISTS (SELECT 1 FROM dbo.WMS_FG_HU_MoveLog WHERE RequestId = @RequestId)
 OR EXISTS (SELECT 1 FROM dbo.WMS_FG_HU_OperationLog WHERE RequestId = @RequestId)
 OR EXISTS (SELECT 1 FROM dbo.WMS_FG_HU_HoldLog WHERE HoldRequestId = @RequestId OR ReleaseRequestId = @RequestId)
 OR EXISTS (SELECT 1 FROM dbo.WMS_FG_OutboundAllocation WHERE RequestId = @RequestId OR ReleaseRequestId = @RequestId)
 OR EXISTS (SELECT 1 FROM dbo.WMS_FG_OutboundPickException WHERE RequestId = @RequestId)
THEN CONVERT(bit, 1) ELSE CONVERT(bit, 0) END;";
            return conn.ExecuteScalar<bool>(sql, new { RequestId = requestId }, tran);
        }

        private class HuHoldRow
        {
            public long Id { get; set; }
            public string HuType { get; set; }
            public bool IsHold { get; set; }
        }

        private class ActiveHoldRow
        {
            public long Id { get; set; }
            public string HoldNo { get; set; }
        }
    }
}
