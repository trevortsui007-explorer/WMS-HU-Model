using System;
using System.Data;
using System.Data.SqlClient;
using Dapper;

namespace WMS_HU_Model.Infrastructure.Database
{
    /// <summary>
    /// HU 操作日志写入器，复用调用方管理的连接和事务。
    /// </summary>
    public static class HuOperationLogWriter
    {
        /// <summary>
        /// 写入 HU 操作日志，不提交、回滚或释放事务。
        /// </summary>
        public static void Insert(
            SqlConnection conn,
            SqlTransaction tran,
            string operationNo,
            string operationType,
            long huId,
            long? relatedHuId,
            string businessNo,
            string requestId,
            string operatorCode,
            DateTime? operationTime,
            string detailJson,
            string remark)
        {
            const string sql = @"
INSERT INTO dbo.WMS_FG_HU_OperationLog
(
    OperationNo,
    OperationType,
    HUId,
    RelatedHUId,
    BusinessNo,
    RequestId,
    DetailJson,
    OperationBy,
    OperationDt,
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
    @OperationBy,
    ISNULL(@OperationDt, SYSDATETIME()),
    @Remark
);
";

            DynamicParameters parameters = new DynamicParameters();
            parameters.Add("@OperationNo", operationNo, DbType.AnsiString, size: 100);
            parameters.Add("@OperationType", operationType, DbType.AnsiString, size: 50);
            parameters.Add("@HUId", huId, DbType.Int64);
            parameters.Add("@RelatedHUId", relatedHuId, DbType.Int64);
            parameters.Add("@BusinessNo", DbParameterValue.StringOrNull(businessNo), DbType.AnsiString, size: 100);
            parameters.Add("@RequestId", DbParameterValue.StringOrNull(requestId), DbType.AnsiString, size: 100);
            parameters.Add("@DetailJson", DbParameterValue.StringOrNull(detailJson), DbType.String, size: -1);
            parameters.Add("@OperationBy", DbParameterValue.StringOrNull(operatorCode), DbType.AnsiString, size: 50);
            parameters.Add("@OperationDt", operationTime, DbType.DateTime2);
            parameters.Add("@Remark", DbParameterValue.StringOrNull(remark), DbType.String, size: 500);

            DbExecutor.Execute(conn, tran, sql, parameters);
        }
    }
}
