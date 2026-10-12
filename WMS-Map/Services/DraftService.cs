using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Map.Application
{
    public sealed class DraftService
    {
        private readonly ILayoutDraftRepository repository;
        private readonly IActor actor;
        public DraftService(ILayoutDraftRepository repository, IActor actor) { this.repository = repository; this.actor = actor; }
        public Task<Draft> GetAsync(string code, CancellationToken ct)
        { actor.RequireId(); return repository.GetAsync(code, ct); }
        public Task<SavedDraft> SaveAsync(string code, SaveDraftRequest request, Guid requestId, CancellationToken ct)
        {
            var actorId = actor.RequireId();
            DraftValidation.Validate(code, request);
            return repository.SaveAsync(code, request, actorId, requestId, ct);
        }
    }
    public static class MapJson
    {
        // Preserve business strings that look like dates, reject duplicate keys and trailing content.
        public static JToken Parse(string json)
        {
            using (var text = new StringReader(json))
            using (var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None, MaxDepth = 64 })
            {
                var value = JToken.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (reader.Read()) throw new JsonReaderException("Unexpected trailing JSON content.");
                return value;
            }
        }
    }
    public static class DraftValidation
    {
        public const long MaxSafeRevision = 9007199254740991;
        public static void Validate(string code, SaveDraftRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(code) || code.Length > 64 || request.ExpectedDraftRevision < 0
                || request.ExpectedDraftRevision >= MaxSafeRevision || request.BasedOnPublishedVersion < 1)
                throw new MapException("INVALID_DRAFT", "布局身份或草稿基线无效。");
            var layout = request.Layout;
            if (layout == null || request.SchemaVersion != 6 || layout["schemaVersion"]?.Type != JTokenType.Integer
                || (long)layout["schemaVersion"] != 6 || layout["layoutCode"]?.Type != JTokenType.String
                || (string)layout["layoutCode"] != code)
                throw new MapException("INVALID_DRAFT", "布局身份或 schemaVersion 不匹配。");
            if (System.Text.Encoding.UTF8.GetByteCount(layout.ToString(Formatting.None)) > 5 * 1024 * 1024)
                throw new MapException("LAYOUT_TOO_LARGE", "布局不能超过 5 MB。", 413);
            RejectRuntime(layout);
            foreach (var name in new[] { "racks", "areas", "rooms", "fixtures", "areaBindings" })
                if (!(layout[name] is JArray)) throw new MapException("INVALID_DRAFT", "布局缺少必要的对象集合。");
            if (!(layout["boundary"] is JObject)) throw new MapException("INVALID_DRAFT", "布局缺少边界。");
        }
        private static void RejectRuntime(JToken value)
        {
            if (value is JObject obj)
                foreach (var property in obj.Properties())
                {
                    if (property.Name == "__proto__" || property.Name == "constructor" || property.Name == "prototype")
                        throw new MapException("INVALID_DRAFT", "布局包含不支持的字段。");
                    if (property.Name == "runtime" || property.Name == "cartons" || property.Name == "huCount"
                        || property.Name == "outerBoxCount" || property.Name == "virtualAreas")
                        throw new MapException("RUNTIME_IN_LAYOUT", "布局中不能保存运行态库存。");
                    RejectRuntime(property.Value);
                }
            else if (value is JArray array)
                foreach (var item in array) RejectRuntime(item);
        }
    }
}
