using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Map.Application;
namespace Map.Infrastructure.Wms
{
    public sealed class WmsOptions
    {
        public string BaseUrl { get; set; } = "";
        public int TimeoutSeconds { get; set; } = 15;
        public int MaxResponseBytes { get; set; } = 5242880;
    }
    public sealed class WmsHttpClient : IWmsClient
    {
        private readonly HttpClient client;
        private readonly WmsOptions options;
        public WmsHttpClient(HttpClient client, WmsOptions options) { this.client = client; this.options = options; }
        public Task<JToken> GetWarehousesAsync(CancellationToken ct) => GetAsync("master-data/warehouses", ct);
        public Task<JToken> GetLocationsAsync(string warehouseCode, CancellationToken ct)
            => GetAsync("locations/?warehouseCode=" + Encode(warehouseCode), ct);
        public Task<JToken> GetHuDetailAsync(string huCode, CancellationToken ct)
            => GetAsync("hus/" + Encode(huCode) + "/detail", ct);
        private static string Encode(string code)
        {
            if (string.IsNullOrWhiteSpace(code) || code.Length > 150 || code.Any(char.IsControl))
                throw new MapException("INVALID_WMS_CODE", "业务编码无效。");
            return Uri.EscapeDataString(code.Trim());
        }
        private async Task<JToken> GetAsync(string relative, CancellationToken ct)
        {
            Uri origin;
            if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out origin)
                || (origin.Scheme != "http" && origin.Scheme != "https") || !string.IsNullOrEmpty(origin.UserInfo)
                || !string.IsNullOrEmpty(origin.Query) || !string.IsNullOrEmpty(origin.Fragment))
                throw new MapException("WMS_NOT_CONFIGURED", "尚未配置有效的 WMS 服务地址。", 503);
            var uri = new Uri(origin.AbsoluteUri.TrimEnd('/') + "/api/wms-fg/" + relative);
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));
                try
                {
                    using (var request = new HttpRequestMessage(HttpMethod.Get, uri))
                    {
                        request.Headers.Accept.ParseAdd("application/json");
                        using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token))
                        {
                            if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                                throw new MapException("WMS_ACCESS_DENIED", "WMS 拒绝服务访问，请检查服务端认证配置。", 502);
                            if (!response.IsSuccessStatusCode) throw new MapException("WMS_HTTP_ERROR", "WMS 查询未成功。", 502);
                            if (response.Content.Headers.ContentLength > options.MaxResponseBytes) throw TooLarge();
                            using (var stream = await response.Content.ReadAsStreamAsync())
                            using (var abort = timeout.Token.Register(() => stream.Dispose()))
                            using (var buffer = new MemoryStream())
                            {
                                var bytes = new byte[8192];
                                int read;
                                while ((read = await stream.ReadAsync(bytes, 0, bytes.Length, timeout.Token)) > 0)
                                {
                                    if (buffer.Length + read > options.MaxResponseBytes) throw TooLarge();
                                    buffer.Write(bytes, 0, read);
                                }
                                timeout.Token.ThrowIfCancellationRequested();
                                return WmsEnvelope.Parse(buffer.ToArray());
                            }
                        }
                    }
                }
                catch (Exception error) when (timeout.IsCancellationRequested &&
                    (error is OperationCanceledException || error is ObjectDisposedException || error is IOException || error is HttpRequestException))
                {
                    ct.ThrowIfCancellationRequested();
                    throw new MapException("WMS_TIMEOUT", "WMS 查询超时。", 504);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                { throw new MapException("WMS_TIMEOUT", "WMS 查询超时。", 504); }
                catch (HttpRequestException) { throw new MapException("WMS_UNAVAILABLE", "无法连接 WMS。", 503); }
                catch (IOException) { throw new MapException("WMS_UNAVAILABLE", "读取 WMS 响应失败。", 503); }
            }
        }
        private static MapException TooLarge() => new MapException("WMS_RESPONSE_TOO_LARGE", "WMS 响应超过大小限制。", 502);
    }
    public static class WmsEnvelope
    {
        public static JToken Parse(byte[] bytes)
        {
            try
            {
                var root = MapJson.Parse(new UTF8Encoding(false, true).GetString(bytes)) as JObject;
                if (root == null) throw Invalid();
                var properties = new Dictionary<string, JToken>(StringComparer.OrdinalIgnoreCase);
                foreach (var property in root.Properties())
                {
                    if (properties.ContainsKey(property.Name)) throw Invalid();
                    properties.Add(property.Name, property.Value);
                }
                JToken code, data;
                if (!properties.TryGetValue("Code", out code) || code.Type != JTokenType.Integer) throw Invalid();
                if ((long)code != 200) throw new MapException("WMS_BUSINESS_ERROR", "WMS 返回业务失败。", 502);
                if (!properties.TryGetValue("Data", out data)) throw Invalid();
                return data.DeepClone();
            }
            catch (JsonException) { throw Invalid(); }
            catch (DecoderFallbackException) { throw Invalid(); }
            catch (OverflowException) { throw Invalid(); }
        }
        private static MapException Invalid() => new MapException("WMS_INVALID_RESPONSE", "WMS 响应格式不符合接口契约。", 502);
    }
}
