using System;
using System.Net;
using System.Net.Http;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Map.Application;
using Map.Infrastructure.Database;
using Map.Infrastructure.Wms;

namespace Map.Tests
{
    internal static class Program
    {
        private static int passed;
        private static int Main()
        {
            try { Run().GetAwaiter().GetResult(); return 0; }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
        private static async Task Run()
        {
            await Check("WMS success unwraps Data", () =>
            {
                Assert((string)WmsEnvelope.Parse(Bytes("{\"Code\":200,\"Data\":{\"HuCode\":\"B1\"}}"))["HuCode"] == "B1");
                return Task.CompletedTask;
            });
            await Check("HTTP 200 business failure", () => Expect("WMS_BUSINESS_ERROR", () =>
                WmsEnvelope.Parse(Bytes("{\"Code\":400,\"Message\":\"private SQL error\"}"))));
            await Check("legacy host failure", () => Expect("WMS_BUSINESS_ERROR", () =>
                WmsEnvelope.Parse(Bytes("{\"code\":500,\"info\":\"private\"}"))));
            await Check("HTML response rejected", () => Expect("WMS_INVALID_RESPONSE", () => WmsEnvelope.Parse(Bytes("<html>login</html>"))));
            await Check("ambiguous status rejected", () => Expect("WMS_INVALID_RESPONSE", () =>
                WmsEnvelope.Parse(Bytes("{\"Code\":200,\"code\":400,\"Data\":null}"))));
            await Check("runtime contamination rejected", () => Expect("RUNTIME_IN_LAYOUT", () =>
                DraftValidation.Validate("MAIN", Request("{\"schemaVersion\":6,\"layoutCode\":\"MAIN\",\"racks\":[{\"cartons\":2}]}"))));
            await Check("draft identity rejected", () => Expect("INVALID_DRAFT", () =>
                DraftValidation.Validate("OTHER", Request("{\"schemaVersion\":6,\"layoutCode\":\"MAIN\"}"))));
            await Check("duplicate JSON rejected at parse boundary", () =>
            {
                try { MapJson.Parse("{\"layoutCode\":\"OTHER\",\"layoutCode\":\"MAIN\"}"); throw new Exception("Duplicate accepted"); }
                catch (JsonException) { }
                return Task.CompletedTask;
            });
            await Check("missing SQL config does not connect", () => ExpectAsync("DATABASE_NOT_CONFIGURED", () =>
                new SqlConnectionFactory(new SqlOptions()).OpenAsync(CancellationToken.None)));
            await Check("unsafe revision rejected", () => Expect("INVALID_DRAFT", () =>
            {
                var request = Request("{\"schemaVersion\":6,\"layoutCode\":\"MAIN\"}");
                request.ExpectedDraftRevision = DraftValidation.MaxSafeRevision;
                DraftValidation.Validate("MAIN", request);
            }));
            await Check("IIS prefix and code escaping", async () =>
            {
                using (var http = new HttpClient(new FakeHandler((request, ct) =>
                {
                    Assert(request.RequestUri.AbsoluteUri == "http://localhost/Learun/api/wms-fg/hus/A%2FB%3FC/detail");
                    return Task.FromResult(Json("{\"Code\":200,\"Data\":null}"));
                }))) await Client(http).GetHuDetailAsync("A/B?C", CancellationToken.None);
            });
            await Check("oversized chunked response rejected", async () =>
            {
                using (var http = new HttpClient(new FakeHandler((request, ct) => Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.OK) { Content = new UnknownLengthContent(new string('a', 2000)) }))))
                    await ExpectAsync("WMS_RESPONSE_TOO_LARGE", () => Client(http, 1000).GetWarehousesAsync(CancellationToken.None));
            });
            await Check("timeout and cancellation distinct", async () =>
            {
                using (var http = new HttpClient(new FakeHandler((request, ct) => throw new TaskCanceledException())))
                {
                    await ExpectAsync("WMS_TIMEOUT", () => Client(http).GetWarehousesAsync(CancellationToken.None));
                    using (var cts = new CancellationTokenSource())
                    {
                        cts.Cancel();
                        try { await Client(http).GetWarehousesAsync(cts.Token); throw new Exception("Cancellation swallowed"); }
                        catch (OperationCanceledException) { }
                    }
                }
            });
            await Check("upstream authentication rejected", async () =>
            {
                using (var http = new HttpClient(new FakeHandler((request, ct) =>
                    Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)))))
                    await ExpectAsync("WMS_ACCESS_DENIED", () => Client(http).GetWarehousesAsync(CancellationToken.None));
            });
            await Check("JSON preserves date-like business codes", () =>
            {
                var value = MapJson.Parse("{\"code\":\"2026-10-10T00:00:00Z\"}");
                Assert(value["code"].Type == JTokenType.String);
                return Task.CompletedTask;
            });
            await Check("valid v6 draft accepted", () =>
            {
                DraftValidation.Validate("MAIN", Request("{\"schemaVersion\":6,\"layoutCode\":\"MAIN\",\"racks\":[],\"areas\":[],\"rooms\":[],\"fixtures\":[],\"areaBindings\":[],\"boundary\":{}}"));
                return Task.CompletedTask;
            });
            Console.WriteLine("PASS " + passed + " net461-targeted checks (no live SQL/WMS).");
        }
        private static async Task Check(string name, Func<Task> test) { await test(); passed++; Console.WriteLine("PASS " + name); }
        private static void Assert(bool condition) { if (!condition) throw new Exception("Assertion failed"); }
        private static byte[] Bytes(string value) => Encoding.UTF8.GetBytes(value);
        private static SaveDraftRequest Request(string json) => new SaveDraftRequest(0, null, 6, (JObject)MapJson.Parse(json));
        private static Task Expect(string code, Action action)
        {
            try { action(); throw new Exception("Expected " + code); }
            catch (MapException error) when (error.Code == code) { }
            return Task.CompletedTask;
        }
        private static async Task ExpectAsync(string code, Func<Task> action)
        {
            try { await action(); throw new Exception("Expected " + code); }
            catch (MapException error) when (error.Code == code) { }
        }
        private static WmsHttpClient Client(HttpClient http, int size = 5242880)
            => new WmsHttpClient(http, new WmsOptions { BaseUrl = "http://localhost/Learun", MaxResponseBytes = size });
        private static HttpResponseMessage Json(string text) => new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(text, Encoding.UTF8, "application/json") };
    }
    internal sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send;
        public FakeHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) { this.send = send; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => send(request, ct);
    }
    internal sealed class UnknownLengthContent : HttpContent
    {
        private readonly string value;
        public UnknownLengthContent(string value) { this.value = value; }
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
        { var bytes = Encoding.UTF8.GetBytes(value); return stream.WriteAsync(bytes, 0, bytes.Length); }
    }
}
