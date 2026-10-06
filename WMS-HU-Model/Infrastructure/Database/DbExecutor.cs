using System.Collections.Generic;
using System.Data.SqlClient;
using Dapper;

namespace WMS_HU_Model.Infrastructure.Database
{
    /// <summary>
    /// Dapper 执行入口，统一收敛连接创建和事务内 SQL 执行样板。
    /// </summary>
    public static class DbExecutor
    {
        /// <summary>
        /// 使用独立数据库连接查询多行数据，并在查询结束后释放连接。
        /// </summary>
        /// <typeparam name="T">查询结果映射类型。</typeparam>
        /// <param name="sql">要执行的 SQL 文本。</param>
        /// <param name="param">SQL 参数对象。</param>
        /// <returns>查询结果列表。</returns>
        public static List<T> Query<T>(string sql, object param = null)
        {
            using (SqlConnection conn = DbConnectionFactory.Create())
            {
                return conn.Query<T>(sql, param).AsList();
            }
        }

        /// <summary>
        /// 使用独立数据库连接查询单行数据，并在查询结束后释放连接。
        /// </summary>
        /// <typeparam name="T">查询结果映射类型。</typeparam>
        /// <param name="sql">要执行的 SQL 文本。</param>
        /// <param name="param">SQL 参数对象。</param>
        /// <returns>查询到的单行结果；未查询到时返回类型默认值。</returns>
        public static T QueryFirstOrDefault<T>(string sql, object param = null)
        {
            using (SqlConnection conn = DbConnectionFactory.Create())
            {
                return conn.QueryFirstOrDefault<T>(sql, param);
            }
        }

        /// <summary>
        /// 使用独立数据库连接执行标量查询，并在查询结束后释放连接。
        /// </summary>
        /// <typeparam name="T">标量结果类型。</typeparam>
        /// <param name="sql">要执行的 SQL 文本。</param>
        /// <param name="param">SQL 参数对象。</param>
        /// <returns>标量查询结果。</returns>
        public static T ExecuteScalar<T>(string sql, object param = null)
        {
            using (SqlConnection conn = DbConnectionFactory.Create())
            {
                return conn.ExecuteScalar<T>(sql, param);
            }
        }

        /// <summary>
        /// 使用独立数据库连接执行非查询 SQL，并在执行结束后释放连接。
        /// </summary>
        /// <param name="sql">要执行的 SQL 文本。</param>
        /// <param name="param">SQL 参数对象。</param>
        /// <returns>受影响的行数。</returns>
        public static int Execute(string sql, object param = null)
        {
            using (SqlConnection conn = DbConnectionFactory.Create())
            {
                return conn.Execute(sql, param);
            }
        }

        /// <summary>
        /// 使用调用方传入的连接和事务查询多行数据，不提交、回滚或释放事务。
        /// </summary>
        /// <typeparam name="T">查询结果映射类型。</typeparam>
        /// <param name="conn">调用方管理生命周期的数据库连接。</param>
        /// <param name="tran">调用方管理生命周期的数据库事务。</param>
        /// <param name="sql">要执行的 SQL 文本。</param>
        /// <param name="param">SQL 参数对象。</param>
        /// <returns>查询结果列表。</returns>
        public static List<T> Query<T>(
            SqlConnection conn,
            SqlTransaction tran,
            string sql,
            object param = null)
        {
            return conn.Query<T>(sql, param, tran).AsList();
        }

        /// <summary>
        /// 使用调用方传入的连接和事务查询单行数据，不提交、回滚或释放事务。
        /// </summary>
        /// <typeparam name="T">查询结果映射类型。</typeparam>
        /// <param name="conn">调用方管理生命周期的数据库连接。</param>
        /// <param name="tran">调用方管理生命周期的数据库事务。</param>
        /// <param name="sql">要执行的 SQL 文本。</param>
        /// <param name="param">SQL 参数对象。</param>
        /// <returns>查询到的单行结果；未查询到时返回类型默认值。</returns>
        public static T QueryFirstOrDefault<T>(
            SqlConnection conn,
            SqlTransaction tran,
            string sql,
            object param = null)
        {
            return conn.QueryFirstOrDefault<T>(sql, param, tran);
        }

        /// <summary>
        /// 使用调用方传入的连接和事务执行标量查询，不提交、回滚或释放事务。
        /// </summary>
        /// <typeparam name="T">标量结果类型。</typeparam>
        /// <param name="conn">调用方管理生命周期的数据库连接。</param>
        /// <param name="tran">调用方管理生命周期的数据库事务。</param>
        /// <param name="sql">要执行的 SQL 文本。</param>
        /// <param name="param">SQL 参数对象。</param>
        /// <returns>标量查询结果。</returns>
        public static T ExecuteScalar<T>(
            SqlConnection conn,
            SqlTransaction tran,
            string sql,
            object param = null)
        {
            return conn.ExecuteScalar<T>(sql, param, tran);
        }

        /// <summary>
        /// 使用调用方传入的连接和事务执行非查询 SQL，不提交、回滚或释放事务。
        /// </summary>
        /// <param name="conn">调用方管理生命周期的数据库连接。</param>
        /// <param name="tran">调用方管理生命周期的数据库事务。</param>
        /// <param name="sql">要执行的 SQL 文本。</param>
        /// <param name="param">SQL 参数对象。</param>
        /// <returns>受影响的行数。</returns>
        public static int Execute(
            SqlConnection conn,
            SqlTransaction tran,
            string sql,
            object param = null)
        {
            return conn.Execute(sql, param, tran);
        }
    }
}
