using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using Map.Application;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Map.Infrastructure.Persistence
{
    public sealed class MapLayoutRepository
    {
        private readonly string connectionString;
        public MapLayoutRepository(string connectionString) { this.connectionString = connectionString; }
        private SqlConnection Open()
        {
            if (string.IsNullOrWhiteSpace(connectionString)) throw new MapException("DATABASE_NOT_CONFIGURED", "尚未配置 BaseDb。", 503);
            var c = new SqlConnection(connectionString);
            try { c.Open(); return c; } catch { c.Dispose(); throw; }
        }
        private static SqlCommand Cmd(SqlConnection c, SqlTransaction tx, string sql, params object[] args)
        {
            var cmd = new SqlCommand(sql, c, tx) { CommandTimeout = 30 };
            for (int i = 0; i < args.Length; i += 2)
            {
                var value = args[i + 1];
                var type = value is Guid ? SqlDbType.UniqueIdentifier : value is long ? SqlDbType.BigInt : value is int ? SqlDbType.Int : value is DateTimeOffset ? SqlDbType.DateTimeOffset : SqlDbType.NVarChar;
                var p = cmd.Parameters.Add((string)args[i], type);
                if (type == SqlDbType.NVarChar) p.Size = -1;
                p.Value = value ?? DBNull.Value;
            }
            return cmd;
        }
        private static void Exec(SqlConnection c, SqlTransaction tx, string sql, params object[] args) { using (var cmd = Cmd(c, tx, sql, args)) cmd.ExecuteNonQuery(); }
        private sealed class State
        {
            public Guid Id; public int? Published, Base; public long Revision; public JObject Layout;
            public string SavedBy; public DateTimeOffset SavedDt;
        }
        private static State Load(SqlConnection c, SqlTransaction tx, string code, bool locked = false)
        {
            CheckCode(code);
            using (var cmd = Cmd(c, tx, "SELECT Id,CurrentPublishedVersion FROM dbo.FG_MapLayout " + (locked ? "WITH(UPDLOCK,HOLDLOCK) " : "") + "WHERE LayoutCode=@c AND IsActive=1", "@c", code))
            using (var reader = cmd.ExecuteReader())
            {
                if (!reader.Read()) return null;
                var state = new State { Id = reader.GetGuid(0), Published = reader.IsDBNull(1) ? (int?)null : reader.GetInt32(1) };
                reader.Close();
                using (var draft = Cmd(c, tx, "SELECT DraftRevision,BasedOnPublishedVersion,LayoutJson,SavedBy,SavedDt FROM dbo.FG_MapLayoutDraft WHERE LayoutId=@id", "@id", state.Id))
                using (var r = draft.ExecuteReader())
                {
                    if (!r.Read()) throw new MapException("DRAFT_MISSING", "布局草稿记录缺失。", 500);
                    state.Revision = r.GetInt64(0); state.Base = r.IsDBNull(1) ? (int?)null : r.GetInt32(1);
                    state.Layout = (JObject)MapJson.Parse(r.GetString(2)); state.SavedBy = r.GetString(3); state.SavedDt = r.GetFieldValue<DateTimeOffset>(4);
                }
                return state;
            }
        }
        private static void CheckCode(string code) { if (string.IsNullOrWhiteSpace(code) || code.Length > 64 || code.Trim() != code) throw new MapException("INVALID_LAYOUT", "布局编码无效。"); }
        private static State Required(State state) { if (state == null) throw new MapException("LAYOUT_NOT_FOUND", "布局尚未初始化。", 404); return state; }
        private static MapException Conflict() { return new MapException("LAYOUT_DRAFT_CONFLICT", "服务器草稿或正式版本已变化，请保留本机工作并重新载入。", 409); }
        private static long Revision(JObject request)
        {
            if (request["expectedDraftRevision"]?.Type != JTokenType.Integer) throw new MapException("INVALID_REQUEST", "缺少草稿修订号。");
            long value = (long)request["expectedDraftRevision"];
            if (value < 0 || value >= DraftValidation.MaxSafeRevision) throw new MapException("INVALID_REQUEST", "草稿修订号无效。");
            return value;
        }
        private static int? Base(JObject request)
        {
            if (!request.ContainsKey("basedOnPublishedVersion")) throw new MapException("INVALID_REQUEST", "缺少发布基线。");
            if (request["basedOnPublishedVersion"].Type == JTokenType.Null) return null;
            if (request["basedOnPublishedVersion"].Type != JTokenType.Integer || (long)request["basedOnPublishedVersion"] < 1 || (long)request["basedOnPublishedVersion"] > int.MaxValue) throw new MapException("INVALID_REQUEST", "发布基线无效。");
            return (int)request["basedOnPublishedVersion"];
        }
        private static object DraftDto(string code, State s) { return new { layoutCode = code, draftRevision = s.Revision, basedOnPublishedVersion = s.Base, savedBy = s.SavedBy, savedDt = s.SavedDt, layout = s.Layout }; }
        private static readonly object Capabilities = new { canEditLayout = true, canSaveDraft = true, canPublishLayout = true };
        public object List()
        {
            var result = new List<object>();
            using (var c = Open()) using (var cmd = Cmd(c, null, "SELECT LayoutCode,LayoutName,CurrentPublishedVersion FROM dbo.FG_MapLayout WHERE IsActive=1 ORDER BY LayoutCode")) using (var r = cmd.ExecuteReader())
                while (r.Read()) result.Add(new { layoutCode = r.GetString(0), layoutName = r.GetString(1), publishedVersion = r.IsDBNull(2) ? (int?)null : r.GetInt32(2) });
            return result;
        }
        public object Draft(string code) { using (var c = Open()) return DraftDto(code, Required(Load(c, null, code))); }
        public object Published(string code, int? version = null)
        {
            using (var c = Open())
            {
                var s = Load(c, null, code);
                if (s == null && version.HasValue) Required(s);
                int? target = version ?? s?.Published;
                if (target == null) return new { layoutCode = code, exists = s != null, publishedVersion = (int?)null, layout = (JObject)null, capabilities = Capabilities };
                using (var cmd = Cmd(c, null, "SELECT SnapshotJson,PublishedBy,PublishedDt FROM dbo.FG_MapLayoutVersion WHERE LayoutId=@id AND PublishedVersion=@v", "@id", s.Id, "@v", target.Value))
                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read()) throw new MapException("VERSION_NOT_FOUND", "正式版本不存在。", 404);
                    return new { layoutCode = code, exists = true, publishedVersion = target, layout = (JObject)MapJson.Parse(r.GetString(0)), publishedBy = r.GetString(1), publishedDt = r.GetFieldValue<DateTimeOffset>(2), capabilities = Capabilities, bindingStatus = "GEOMETRY_ONLY" };
                }
            }
        }
        public object Versions(string code)
        {
            using (var c = Open())
            {
                var s = Required(Load(c, null, code)); var list = new List<object>();
                using (var cmd = Cmd(c, null, "SELECT PublishedVersion,SourceDraftRevision,PublishedBy,PublishedDt,PublishNote FROM dbo.FG_MapLayoutVersion WHERE LayoutId=@id ORDER BY PublishedVersion DESC", "@id", s.Id))
                using (var r = cmd.ExecuteReader()) while (r.Read()) list.Add(new { publishedVersion = r.GetInt32(0), sourceDraftRevision = r.GetInt64(1), publishedBy = r.GetString(2), publishedDt = r.GetFieldValue<DateTimeOffset>(3), publishNote = r.IsDBNull(4) ? null : r.GetString(4) });
                return list;
            }
        }
        public object Initialize(string code, JObject request, string actor)
        {
            CheckCode(code); var layout = request["layout"] as JObject;
            LayoutRules.Validate(code, layout);
            string name = (string)request["layoutName"] ?? code;
            if (string.IsNullOrWhiteSpace(name) || name.Length > 150) throw new MapException("INVALID_LAYOUT", "布局名称无效。");
            using (var c = Open()) using (var tx = c.BeginTransaction())
            {
                if (Load(c, tx, code, true) != null) throw Conflict();
                var id = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
                Exec(c, tx, "INSERT dbo.FG_MapLayout(Id,LayoutCode,LayoutName,IsActive,CreateBy,CreateDt) VALUES(@id,@c,@n,1,@a,@t); INSERT dbo.FG_MapLayoutDraft(LayoutId,DraftRevision,SchemaVersion,LayoutJson,SavedBy,SavedDt,CreateBy,CreateDt) VALUES(@id,0,6,@j,@a,@t,@a,@t)", "@id", id, "@c", code, "@n", name, "@a", actor, "@t", now, "@j", layout.ToString(Formatting.None));
                Audit(c, tx, id, "INITIALIZE", 0, null, Guid.NewGuid(), actor, now);
                tx.Commit(); return new { layoutCode = code, draftRevision = 0, basedOnPublishedVersion = (int?)null, savedBy = actor, savedDt = now };
            }
        }
        public object Save(string code, JObject request, string actor)
        {
            var rev = Revision(request); var basis = Base(request); var layout = request["layout"] as JObject; LayoutRules.Validate(code, layout);
            using (var c = Open()) using (var tx = c.BeginTransaction())
            {
                var s = Required(Load(c, tx, code, true)); if (s.Revision != rev || s.Published != basis || s.Base != basis) throw Conflict();
                if ((string)s.Layout["layoutId"] != (string)layout["layoutId"]) throw new MapException("LAYOUT_IDENTITY_MISMATCH", "布局 ID 不能改变。", 409);
                var now = DateTimeOffset.UtcNow;
                Exec(c, tx, "UPDATE dbo.FG_MapLayoutDraft SET DraftRevision=@r,LayoutJson=@j,SavedBy=@a,SavedDt=@t,LastEditBy=@a,LastEditDt=@t WHERE LayoutId=@id; UPDATE dbo.FG_MapLayout SET LastEditBy=@a,LastEditDt=@t WHERE Id=@id", "@r", rev + 1, "@j", layout.ToString(Formatting.None), "@a", actor, "@t", now, "@id", s.Id);
                Audit(c, tx, s.Id, "SAVE_DRAFT", rev + 1, basis, Guid.NewGuid(), actor, now); tx.Commit();
                return new { layoutCode = code, draftRevision = rev + 1, basedOnPublishedVersion = basis, savedBy = actor, savedDt = now };
            }
        }
        public object Publish(string code, JObject request, string actor)
        {
            var rev = Revision(request); var basis = Base(request); Guid requestId;
            if (!Guid.TryParse((string)request["requestId"], out requestId) || requestId == Guid.Empty) throw new MapException("INVALID_REQUEST", "发布需要唯一请求号。");
            string note = (string)request["publishNote"]; if (note != null && note.Length > 500) throw new MapException("INVALID_REQUEST", "发布备注超过 500 字。");
            using (var c = Open()) using (var tx = c.BeginTransaction())
            {
                var s = Required(Load(c, tx, code, true));
                // 在同一布局锁内先查询请求号；响应丢失后的重试返回原版本，不重复发布。
                using (var replay = Cmd(c, tx, "SELECT PublishedVersion,SourceDraftRevision,BasedOnPublishedVersion,PublishedBy,PublishedDt FROM dbo.FG_MapLayoutVersion WHERE LayoutId=@id AND PublishRequestId=@q", "@id", s.Id, "@q", requestId))
                using (var r = replay.ExecuteReader()) if (r.Read())
                {
                    if (r.GetInt64(1) != rev || (r.IsDBNull(2) ? (int?)null : r.GetInt32(2)) != basis) throw new MapException("REQUEST_ID_REUSED", "发布请求号已用于其他内容。", 409);
                    return Publication(code, r.GetInt32(0), rev + 1, r.GetString(3), r.GetFieldValue<DateTimeOffset>(4));
                }
                if (s.Revision != rev || s.Base != basis || s.Published != basis) throw Conflict();
                LayoutRules.Validate(code, s.Layout);
                if (((JArray)s.Layout["areaBindings"]).Count != 0) throw new MapException("PUBLISH_BINDING_NOT_READY", "本阶段发布布局几何；请先移除尚未接入 WMS 校验的虚拟库位绑定。草稿已保留。", 422);
                if (s.Published.HasValue)
                    using (var previous = Cmd(c, tx, "SELECT SnapshotJson FROM dbo.FG_MapLayoutVersion WHERE LayoutId=@id AND PublishedVersion=@v", "@id", s.Id, "@v", s.Published.Value))
                        LayoutRules.CheckPublishedIdentity((JObject)MapJson.Parse((string)previous.ExecuteScalar()), s.Layout);
                if (s.Published == int.MaxValue) throw new MapException("VERSION_LIMIT", "正式版本号已达上限。", 409);
                int version = (s.Published ?? 0) + 1; var now = DateTimeOffset.UtcNow;
                Exec(c, tx, "INSERT dbo.FG_MapLayoutVersion(LayoutId,PublishedVersion,SchemaVersion,SnapshotJson,SourceDraftRevision,BasedOnPublishedVersion,PublishedBy,PublishedDt,PublishNote,PublishRequestId,CreateBy,CreateDt) VALUES(@id,@v,6,@j,@r,@b,@a,@t,@note,@q,@a,@t); UPDATE dbo.FG_MapLayout SET CurrentPublishedVersion=@v,LastEditBy=@a,LastEditDt=@t WHERE Id=@id; UPDATE dbo.FG_MapLayoutDraft SET BasedOnPublishedVersion=@v,DraftRevision=@next,LastEditBy=@a,LastEditDt=@t WHERE LayoutId=@id", "@id", s.Id, "@v", version, "@j", s.Layout.ToString(Formatting.None), "@r", rev, "@b", basis, "@a", actor, "@t", now, "@note", note, "@q", requestId, "@next", rev + 1);
                var warehouses = new HashSet<string>(s.Layout["racks"].Select(r => (string)r["fgCode"]), StringComparer.OrdinalIgnoreCase);
                if (!string.IsNullOrWhiteSpace((string)s.Layout["warehouseCode"])) warehouses.Add((string)s.Layout["warehouseCode"]);
                foreach (string warehouse in warehouses) Exec(c, tx, "INSERT dbo.FG_MapLayoutVersionWarehouse(LayoutId,PublishedVersion,WarehouseCode,CreateBy,CreateDt) VALUES(@id,@v,@w,@a,@t)", "@id", s.Id, "@v", version, "@w", warehouse, "@a", actor, "@t", now);
                Audit(c, tx, s.Id, "PUBLISH", rev, version, requestId, actor, now); tx.Commit();
                return Publication(code, version, rev + 1, actor, now);
            }
        }
        private static object Publication(string code, int version, long rev, string actor, DateTimeOffset now) { return new { layoutCode = code, status = "PUBLISHED", publishedVersion = version, draftRevision = rev, basedOnPublishedVersion = version, publishedBy = actor, publishedDt = now, bindingStatus = "GEOMETRY_ONLY" }; }
        private static void Audit(SqlConnection c, SqlTransaction tx, Guid id, string action, long rev, int? version, Guid request, string actor, DateTimeOffset now)
        { Exec(c, tx, "INSERT dbo.FG_MapOperationLog(LayoutId,Action,DraftRevision,PublishedVersion,RequestId,Summary,CreateBy,CreateDt) VALUES(@id,@action,@r,@v,@q,@action,@a,@t)", "@id", id, "@action", action, "@r", rev, "@v", version, "@q", request, "@a", actor, "@t", now); }
    }
}
