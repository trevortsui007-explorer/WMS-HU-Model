using System;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
namespace Map.Infrastructure.Database
{
    // Framework SqlTransaction has synchronous Begin/Commit/Rollback; queries remain cancellable async.
    // Never retry writes automatically: interrupted COMMIT may have an ambiguous result.
    public sealed class SqlUnitOfWork
    {
        private readonly ISqlConnectionFactory connections;
        public SqlUnitOfWork(ISqlConnectionFactory connections) { this.connections = connections; }
        public async Task<T> ExecuteAsync<T>(Func<SqlConnection, SqlTransaction, CancellationToken, Task<T>> work, CancellationToken ct)
        {
            using (var connection = await connections.OpenAsync(ct))
            using (var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
            {
                try
                {
                    var result = await work(connection, transaction, ct);
                    ct.ThrowIfCancellationRequested();
                    transaction.Commit();
                    return result;
                }
                catch
                {
                    try { transaction.Rollback(); }
                    catch (Exception error) { Trace.TraceWarning("Map transaction rollback failed: {0}", error); }
                    throw;
                }
            }
        }
    }
}
