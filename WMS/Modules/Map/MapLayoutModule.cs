using System;
using System.Configuration;
using System.IO;
using System.Text;
using Learun.Application.WebApi;
using Nancy;
using Map.Application;
using Map.Infrastructure.Persistence;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace WMS.Modules.Map
{
    public sealed class MapLayoutModule : BaseApi
    {
        public MapLayoutModule() : base("/api/wms-fg/map/layouts")
        {
            Get["/"] = _ => Run((repo, actor) => repo.List());
            Get["/{code}"] = args => Run((repo, actor) => repo.Published(Convert.ToString((object)args.code)));
            Get["/{code}/draft"] = args => Run((repo, actor) => repo.Draft(Convert.ToString((object)args.code)));
            Post["/{code}/initialize"] = args => Run((repo, actor) => repo.Initialize(Convert.ToString((object)args.code), Body(), actor));
            Put["/{code}/draft"] = args => Run((repo, actor) => repo.Save(Convert.ToString((object)args.code), Body(), actor));
            Post["/{code}/publish"] = args => Run((repo, actor) => repo.Publish(Convert.ToString((object)args.code), Body(), actor));
            Get["/{code}/versions"] = args => Run((repo, actor) => repo.Versions(Convert.ToString((object)args.code)));
            Get["/{code}/versions/{version:int}"] = args => Run((repo, actor) => repo.Published(Convert.ToString((object)args.code), (int)args.version));
        }
        private JObject Body()
        {
            using (var buffer = new MemoryStream())
            {
                var bytes = new byte[8192]; int count;
                while ((count = Request.Body.Read(bytes, 0, bytes.Length)) > 0)
                {
                    if (buffer.Length + count > 6 * 1024 * 1024) throw new MapException("LAYOUT_TOO_LARGE", "请求超过 6 MB。", 413);
                    buffer.Write(bytes, 0, count);
                }
                var value = MapJson.Parse(new UTF8Encoding(false, true).GetString(buffer.ToArray())) as JObject;
                if (value == null) throw new MapException("INVALID_REQUEST", "请求必须是 JSON 对象。");
                return value;
            }
        }
        private Response Run(Func<MapLayoutRepository, string, object> action)
        {
            var trace = Guid.NewGuid().ToString("D");
            try
            {
                var actor = MapIdentity.Resolve == null ? null : MapIdentity.Resolve(Context);
                if (string.IsNullOrWhiteSpace(actor) || actor.Length > 128) throw new MapException("UNAUTHORIZED", "地图身份尚未接入，请先登录或使用独立开发宿主。", 401);
                return Json(action(new MapLayoutRepository(ConfigurationManager.ConnectionStrings["BaseDb"]?.ConnectionString), actor), 200, trace);
            }
            catch (Exception error)
            {
                System.Diagnostics.Trace.TraceError("Map {0}: {1}", trace, error);
                var known = error as MapException;
                if (known == null && (error is JsonException || error is DecoderFallbackException || error is FormatException || error is OverflowException)) known = new MapException("INVALID_REQUEST", "请求字段或 JSON 格式无效。");
                if (known == null && error is System.Data.SqlClient.SqlException) known = new MapException("DATABASE_ERROR", "数据库操作失败，请凭追踪号检查日志。", 503);
                return Json(new { code = known?.Code ?? "INTERNAL_ERROR", message = known?.Message ?? "服务处理失败。", traceId = trace }, known?.Status ?? 500, trace);
            }
        }
        private static Response Json(object value, int status, string trace)
        {
            Response response = JsonConvert.SerializeObject(value);
            response.ContentType = "application/json; charset=utf-8";
            response.StatusCode = (HttpStatusCode)status;
            response.Headers["Cache-Control"] = "no-store";
            response.Headers["X-Trace-Id"] = trace;
            return response;
        }
    }
}
