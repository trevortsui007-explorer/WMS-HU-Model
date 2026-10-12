using System;
using System.Data;
using System.Data.SqlClient;
namespace Map.Infrastructure.Database
{
    internal static class SqlCommands
    {
        public static SqlCommand Create(SqlConnection connection, SqlTransaction transaction, string sql, int timeout)
            => new SqlCommand(sql, connection, transaction) { CommandTimeout = timeout };
        public static void Text(this SqlCommand command, string name, string value, int size)
            => command.Parameters.Add(name, SqlDbType.NVarChar, size).Value = (object)value ?? DBNull.Value;
        public static void Value(this SqlCommand command, string name, SqlDbType type, object value)
            => command.Parameters.Add(name, type).Value = value ?? DBNull.Value;
    }
}
