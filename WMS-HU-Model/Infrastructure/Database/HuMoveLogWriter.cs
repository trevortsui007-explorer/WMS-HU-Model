using System;
using System.Data;
using System.Data.SqlClient;
using Dapper;

namespace WMS_HU_Model.Infrastructure.Database
{
    /// <summary>
    /// HU 移动日志写入器，复用调用方管理的连接和事务。
    /// </summary>
    public static class HuMoveLogWriter
    {
        /// <summary>
        /// 写入 HU 移动日志并返回移动日志主键和移动时间，不提交、回滚或释放事务。
        /// </summary>
        public static HuMoveLogWriteResult InsertWithTime(
            SqlConnection conn,
            SqlTransaction tran,
            string moveNo,
            long huId,
            long? fromLocationId,
            long toLocationId,
            string moveType,
            string scannedLocationCode,
            string businessNo,
            string taskNo,
            string requestId,
            string reasonCode,
            string operatorCode,
            string remark)
        {
            const string sql = @"
INSERT INTO dbo.WMS_FG_HU_MoveLog
(
    MoveNo,
    HUId,
    FromLocationId,
    ToLocationId,
    MoveType,
    BusinessNo,
    RequestId,
    ReasonCode,
    MoveBy,
    Remark
)
OUTPUT
    INSERTED.Id,
    INSERTED.MoveDt AS MoveTime
VALUES
(
    @MoveNo,
    @HUId,
    @FromLocationId,
    @ToLocationId,
    @MoveType,
    @BusinessNo,
    @RequestId,
    @ReasonCode,
    @MoveBy,
    @Remark
);
";

            return DbExecutor.QueryFirstOrDefault<HuMoveLogWriteResult>(
                conn,
                tran,
                sql,
                CreateParameters(
                    moveNo,
                    huId,
                    fromLocationId,
                    toLocationId,
                    moveType,
                    scannedLocationCode,
                    businessNo,
                    taskNo,
                    requestId,
                    reasonCode,
                    operatorCode,
                    moveTime: null,
                    remark: remark));
        }

        /// <summary>
        /// 写入 HU 移动日志并返回移动日志主键，不提交、回滚或释放事务。
        /// </summary>
        public static long Insert(
            SqlConnection conn,
            SqlTransaction tran,
            string moveNo,
            long huId,
            long? fromLocationId,
            long toLocationId,
            string moveType,
            string scannedLocationCode,
            string businessNo,
            string taskNo,
            string requestId,
            string reasonCode,
            string operatorCode,
            DateTime? moveTime,
            string remark)
        {
            const string sql = @"
INSERT INTO dbo.WMS_FG_HU_MoveLog
(
    MoveNo,
    HUId,
    FromLocationId,
    ToLocationId,
    MoveType,
    BusinessNo,
    RequestId,
    ReasonCode,
    MoveBy,
    MoveDt,
    Remark
)
OUTPUT INSERTED.Id
VALUES
(
    @MoveNo,
    @HUId,
    @FromLocationId,
    @ToLocationId,
    @MoveType,
    @BusinessNo,
    @RequestId,
    @ReasonCode,
    @MoveBy,
    ISNULL(@MoveDt, SYSDATETIME()),
    @Remark
);
";

            return DbExecutor.ExecuteScalar<long>(
                conn,
                tran,
                sql,
                CreateParameters(
                    moveNo,
                    huId,
                    fromLocationId,
                    toLocationId,
                    moveType,
                    scannedLocationCode,
                    businessNo,
                    taskNo,
                    requestId,
                    reasonCode,
                    operatorCode,
                    moveTime,
                    remark));
        }

        private static DynamicParameters CreateParameters(
            string moveNo,
            long huId,
            long? fromLocationId,
            long toLocationId,
            string moveType,
            string scannedLocationCode,
            string businessNo,
            string taskNo,
            string requestId,
            string reasonCode,
            string operatorCode,
            DateTime? moveTime,
            string remark)
        {
            DynamicParameters parameters = new DynamicParameters();
            parameters.Add("@MoveNo", moveNo, DbType.AnsiString, size: 100);
            parameters.Add("@HUId", huId, DbType.Int64);
            parameters.Add("@FromLocationId", fromLocationId, DbType.Int64);
            parameters.Add("@ToLocationId", toLocationId, DbType.Int64);
            parameters.Add("@MoveType", moveType, DbType.AnsiString, size: 30);
            parameters.Add("@BusinessNo", DbParameterValue.StringOrNull(businessNo), DbType.AnsiString, size: 100);
            parameters.Add("@RequestId", DbParameterValue.StringOrNull(requestId), DbType.AnsiString, size: 100);
            parameters.Add("@ReasonCode", DbParameterValue.StringOrNull(reasonCode), DbType.AnsiString, size: 50);
            parameters.Add("@MoveBy", DbParameterValue.StringOrNull(operatorCode), DbType.AnsiString, size: 50);
            parameters.Add("@MoveDt", moveTime, DbType.DateTime2);
            parameters.Add("@Remark", BusinessLogText.Move(moveType, remark), DbType.String, size: 500);

            return parameters;
        }

        public class HuMoveLogWriteResult
        {
            public long Id { get; set; }
            public DateTime MoveTime { get; set; }
        }
    }
}
