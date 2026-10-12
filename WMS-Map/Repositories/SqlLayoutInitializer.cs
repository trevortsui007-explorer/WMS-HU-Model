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
    public sealed class SqlLayoutInitializer
    {
        private readonly SqlUnitOfWork unitOfWork;
        private readonly SqlOptions options;
        private readonly IClock clock;
        public SqlLayoutInitializer(SqlUnitOfWork unitOfWork, SqlOptions options, IClock clock)
        { this.unitOfWork = unitOfWork; this.options = options; this.clock = clock; }
        public Task<Guid> InitializeAsync(string code, string name, JObject layout, string actorId, CancellationToken ct)
        {
            DraftValidation.Validate(code, new SaveDraftRequest(0, null, 6, layout));
            if (string.IsNullOrWhiteSpace(name) || name.Length > 150 || string.IsNullOrWhiteSpace(actorId) || actorId.Length > 128)
                throw new MapException("INVALID_LAYOUT", "布局名称或操作人无效。");
            return unitOfWork.ExecuteAsync(async (connection, transaction, token) =>
            {
                var id = Guid.NewGuid();
                using (var command = SqlCommands.Create(connection, transaction, @"IF EXISTS(SELECT 1 FROM dbo.FG_MapLayout WITH(UPDLOCK,HOLDLOCK) WHERE LayoutCode=@Code)
                    SELECT CAST(0 AS bit);
                ELSE BEGIN
                    INSERT dbo.FG_MapLayout(Id,LayoutCode,LayoutName,IsActive,CreateBy,CreateDt)
                    VALUES(@Id,@Code,@Name,1,@Actor,@Now);
                    INSERT dbo.FG_MapLayoutDraft(LayoutId,DraftRevision,SchemaVersion,LayoutJson,SavedBy,SavedDt,CreateBy,CreateDt)
                    VALUES(@Id,0,6,@Json,@Actor,@Now,@Actor,@Now);
                    INSERT dbo.FG_MapOperationLog(LayoutId,Action,DraftRevision,RequestId,Summary,CreateBy,CreateDt)
                    VALUES(@Id,'INITIALIZE',0,@Request,N'显式初始化布局草稿',@Actor,@Now);
                    SELECT CAST(1 AS bit);
                END", options.CommandTimeoutSeconds))
                {
                    command.Value("@Id", SqlDbType.UniqueIdentifier, id);
                    command.Value("@Request", SqlDbType.UniqueIdentifier, Guid.NewGuid());
                    command.Value("@Now", SqlDbType.DateTimeOffset, clock.UtcNow);
                    command.Text("@Code", code, 64);
                    command.Text("@Name", name, 150);
                    command.Text("@Actor", actorId, 128);
                    command.Text("@Json", layout.ToString(Formatting.None), -1);
                    if (!Convert.ToBoolean(await command.ExecuteScalarAsync(token)))
                        throw new MapException("LAYOUT_ALREADY_EXISTS", "布局已存在，不允许覆盖初始化。", 409);
                }
                return id;
            }, ct);
        }
    }
}
