using System;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using Map.Application;

namespace Map.Infrastructure.Database
{
    public sealed class SqlOptions
    {
        public string ConnectionString { get; set; } = "";
        public int CommandTimeoutSeconds { get; set; } = 30;
    }
    public interface ISqlConnectionFactory { Task<SqlConnection> OpenAsync(CancellationToken ct); }
    public sealed class SqlConnectionFactory : ISqlConnectionFactory
    {
        private readonly SqlOptions options;
        public SqlConnectionFactory(SqlOptions options) { this.options = options; }
        public async Task<SqlConnection> OpenAsync(CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(options.ConnectionString))
                throw new MapException("DATABASE_NOT_CONFIGURED", "尚未配置地图数据库。", 503);
            var connection = new SqlConnection(options.ConnectionString);
            try { await connection.OpenAsync(ct); return connection; }
            catch { connection.Dispose(); throw; }
        }
    }
}
