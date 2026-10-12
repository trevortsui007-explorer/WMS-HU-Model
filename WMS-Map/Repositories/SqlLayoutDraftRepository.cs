using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Map.Application;
using Map.Infrastructure.Database;
namespace Map.Infrastructure.Persistence
{
    public sealed class SqlLayoutDraftRepository : ILayoutDraftRepository
    {
        private readonly ISqlConnectionFactory connections;
        private readonly SqlUnitOfWork unitOfWork;
        private readonly SqlOptions options;
        private readonly IClock clock;
        public SqlLayoutDraftRepository(ISqlConnectionFactory connections, SqlUnitOfWork unitOfWork, SqlOptions options, IClock clock)
        { this.connections = connections; this.unitOfWork = unitOfWork; this.options = options; this.clock = clock; }
        public async Task<Draft> GetAsync(string layoutCode, CancellationToken ct)
        {
            using (var connection = await connections.OpenAsync(ct))
            using (var command = SqlCommands.Create(connection, null, @"SELECT d.DraftRevision, d.BasedOnPublishedVersion, d.LayoutJson, d.SavedDt, d.SavedBy
            FROM dbo.FG_MapLayout l INNER JOIN dbo.FG_MapLayoutDraft d ON d.LayoutId=l.Id
            WHERE l.LayoutCode=@Code AND l.IsActive=1;", options.CommandTimeoutSeconds))
            {
                command.Text("@Code", layoutCode, 64);
                using (var reader = await command.ExecuteReaderAsync(ct))
                {
                    if (!await reader.ReadAsync(ct)) return null;
                    return new Draft(layoutCode, reader.GetInt64(0), reader.IsDBNull(1) ? (int?)null : reader.GetInt32(1),
                        (JObject)MapJson.Parse(reader.GetString(2)), reader.GetFieldValue<DateTimeOffset>(3), reader.GetString(4));
                }
            }
        }
        public Task<SavedDraft> SaveAsync(string layoutCode, SaveDraftRequest request, string actorId, Guid requestId, CancellationToken ct)
        {
            DraftValidation.Validate(layoutCode, request);
            return unitOfWork.ExecuteAsync(async (connection, transaction, token) =>
            {
                Guid id;
                int? published;
                // Every writer, including publication, locks Layout before Draft.
                using (var find = SqlCommands.Create(connection, transaction, @"SELECT Id, CurrentPublishedVersion FROM dbo.FG_MapLayout WITH (UPDLOCK,HOLDLOCK)
                WHERE LayoutCode=@Code AND IsActive=1;", options.CommandTimeoutSeconds))
                {
                    find.Text("@Code", layoutCode, 64);
                    using (var reader = await find.ExecuteReaderAsync(token))
                    {
                        if (!await reader.ReadAsync(token)) throw new MapException("LAYOUT_NOT_FOUND", "布局尚未初始化。", 404);
                        id = reader.GetGuid(0);
                        published = reader.IsDBNull(1) ? (int?)null : reader.GetInt32(1);
                    }
                }
                if (published != request.BasedOnPublishedVersion) throw Conflict();
                var savedDt = clock.UtcNow;
                using (var save = SqlCommands.Create(connection, transaction, @"UPDATE dbo.FG_MapLayoutDraft SET DraftRevision=DraftRevision+1, SchemaVersion=@Schema,
                    LayoutJson=@Json, SavedBy=@Actor, SavedDt=@Now, LastEditBy=@Actor, LastEditDt=@Now
                WHERE LayoutId=@Id AND DraftRevision=@Expected
                  AND (BasedOnPublishedVersion=@Published OR (BasedOnPublishedVersion IS NULL AND @Published IS NULL));
                SELECT @@ROWCOUNT;", options.CommandTimeoutSeconds))
                {
                    save.Value("@Id", SqlDbType.UniqueIdentifier, id);
                    save.Value("@Schema", SqlDbType.Int, request.SchemaVersion);
                    save.Value("@Expected", SqlDbType.BigInt, request.ExpectedDraftRevision);
                    save.Value("@Published", SqlDbType.Int, published);
                    save.Value("@Now", SqlDbType.DateTimeOffset, savedDt);
                    save.Text("@Json", request.Layout.ToString(Formatting.None), -1);
                    save.Text("@Actor", actorId, 128);
                    if (Convert.ToInt32(await save.ExecuteScalarAsync(token)) != 1) throw Conflict();
                }
                using (var audit = SqlCommands.Create(connection, transaction, @"INSERT dbo.FG_MapOperationLog(LayoutId,Action,DraftRevision,PublishedVersion,RequestId,
                    Summary,CreateBy,CreateDt)
                VALUES(@Id,'SAVE_DRAFT',@Revision,@Published,@RequestId,N'保存服务器草稿',@Actor,@Now);", options.CommandTimeoutSeconds))
                {
                    audit.Value("@Id", SqlDbType.UniqueIdentifier, id);
                    audit.Value("@Revision", SqlDbType.BigInt, request.ExpectedDraftRevision + 1);
                    audit.Value("@Published", SqlDbType.Int, published);
                    audit.Value("@RequestId", SqlDbType.UniqueIdentifier, requestId);
                    audit.Value("@Now", SqlDbType.DateTimeOffset, savedDt);
                    audit.Text("@Actor", actorId, 128);
                    await audit.ExecuteNonQueryAsync(token);
                }
                return new SavedDraft(layoutCode, request.ExpectedDraftRevision + 1, published, savedDt, actorId);
            }, ct);
        }
        private static MapException Conflict() => new MapException("LAYOUT_DRAFT_CONFLICT", "服务器草稿或发布基线已变化，请重新加载。", 409);
    }
}
