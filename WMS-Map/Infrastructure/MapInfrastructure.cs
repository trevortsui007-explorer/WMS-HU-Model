using System;
using System.Net.Http;
using System.Threading;
using Map.Application;
using Map.Infrastructure.Database;
using Map.Infrastructure.Persistence;
using Map.Infrastructure.Wms;
namespace Map.Infrastructure
{
    // Explicit composition root; one shared HTTP transport, new SQL connections per operation.
    public sealed class MapInfrastructure : IDisposable
    {
        private readonly HttpClient transport;
        public ILayoutDraftRepository Drafts { get; }
        public SqlLayoutInitializer Initializer { get; }
        public IWmsClient Wms { get; }
        public MapInfrastructure(SqlOptions sql, WmsOptions wms)
        {
            if (sql.CommandTimeoutSeconds < 1 || sql.CommandTimeoutSeconds > 300
                || wms.TimeoutSeconds < 1 || wms.TimeoutSeconds > 120
                || wms.MaxResponseBytes < 1024 || wms.MaxResponseBytes > 20 * 1024 * 1024)
                throw new ArgumentException("SQL/WMS timeout or response size configuration is invalid.");
            var connections = new SqlConnectionFactory(sql);
            var transactions = new SqlUnitOfWork(connections);
            var clock = new SystemClock();
            Drafts = new SqlLayoutDraftRepository(connections, transactions, sql, clock);
            Initializer = new SqlLayoutInitializer(transactions, sql, clock);
            transport = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
            { Timeout = Timeout.InfiniteTimeSpan };
            Wms = new WmsHttpClient(transport, wms);
        }
        public void Dispose() { transport.Dispose(); }
    }
    public sealed class SystemClock : IClock { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }
}
