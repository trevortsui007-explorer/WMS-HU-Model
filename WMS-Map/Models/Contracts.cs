using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Map.Application
{
    public sealed class MapException : Exception
    {
        public string Code { get; }
        public int Status { get; }
        public MapException(string code, string message, int status = 400) : base(message)
        { Code = code; Status = status; }
    }

    public interface IActor { string RequireId(); }
    public interface IClock { DateTimeOffset UtcNow { get; } }
    public sealed class SaveDraftRequest
    {
        public long ExpectedDraftRevision { get; set; }
        public int? BasedOnPublishedVersion { get; set; }
        public int SchemaVersion { get; set; }
        public JObject Layout { get; set; }
        public SaveDraftRequest() { }
        public SaveDraftRequest(long revision, int? published, int schema, JObject layout)
        { ExpectedDraftRevision = revision; BasedOnPublishedVersion = published; SchemaVersion = schema; Layout = layout; }
    }
    public class SavedDraft
    {
        public string LayoutCode { get; }
        public long DraftRevision { get; }
        public int? BasedOnPublishedVersion { get; }
        public DateTimeOffset SavedDt { get; }
        public string SavedBy { get; }
        public SavedDraft(string code, long revision, int? published, DateTimeOffset savedDt, string savedBy)
        { LayoutCode = code; DraftRevision = revision; BasedOnPublishedVersion = published; SavedDt = savedDt; SavedBy = savedBy; }
    }
    public sealed class Draft : SavedDraft
    {
        public JObject Layout { get; }
        public Draft(string code, long revision, int? published, JObject layout, DateTimeOffset savedDt, string savedBy)
            : base(code, revision, published, savedDt, savedBy) { Layout = layout; }
    }
    public interface ILayoutDraftRepository
    {
        Task<Draft> GetAsync(string layoutCode, CancellationToken cancellationToken);
        Task<SavedDraft> SaveAsync(string layoutCode, SaveDraftRequest request, string actorId, Guid requestId, CancellationToken cancellationToken);
    }
    public interface IWmsClient
    {
        Task<JToken> GetWarehousesAsync(CancellationToken cancellationToken);
        Task<JToken> GetLocationsAsync(string warehouseCode, CancellationToken cancellationToken);
        Task<JToken> GetHuDetailAsync(string huCode, CancellationToken cancellationToken);
    }
}
