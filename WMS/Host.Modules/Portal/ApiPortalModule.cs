using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nancy;
using Nancy.Routing;
using Newtonsoft.Json;

namespace WMS.Host.Modules.Portal
{
    // Host-only UI. It is deliberately outside the generated business modules.
    public sealed class ApiPortalModule : NancyModule
    {
        internal static Func<IRouteCache> ReadCache;
        private static readonly Dictionary<string, string> Categories = new Dictionary<string, string>
        {
            { "MapLayoutModule", "地图布局与发布" }, { "HealthModule", "系统健康" }, { "MonitoringModule", "监控与审计" }, { "AuditModule", "监控与审计" },
            { "MasterDataMaintenanceModule", "主数据与货位" }, { "LocationModule", "主数据与货位" },
            { "HuModule", "HU 管理" }, { "HuDetailModule", "HU 管理" }, { "HuLocationModule", "HU 管理" },
            { "FinishedGoodsInventoryModule", "库存查询" }, { "InventoryQueryModule", "库存查询" },
            { "FinishedGoodsInboundIntegrationModule", "入库与确认" }, { "FinishedGoodsInboundReadModule", "入库与确认" },
            { "MesFinishedGoodsInboundIntegrationModule", "入库与确认" }, { "InboundConfirmModule", "入库与确认" },
            { "PutawayModule", "上架与移库" }, { "RelocationModule", "上架与移库" },
            { "RepackModule", "拆包与重组" },
            { "OutboundOrderModule", "出库与拣货" }, { "OutboundPickingModule", "出库与拣货" }, { "OutboundExceptionModule", "出库与拣货" },
            { "OutboundSapPostingModule", "SAP 集成与过账" }, { "SapOutboundIntegrationModule", "SAP 集成与过账" },
            { "WeekCodeAuditModule", "周码管理" }, { "WeekCodeRecalculateModule", "周码管理" }
        };
        private static readonly Dictionary<string, string> Descriptions = LoadDescriptions();
        private static Dictionary<string, string> LoadDescriptions()
        {
            using (var stream = typeof(ApiPortalModule).Assembly.GetManifestResourceStream("WMS.Standalone.Portal.ApiDescriptions.zh-CN.json"))
            using (var reader = new StreamReader(stream))
                return JsonConvert.DeserializeObject<Dictionary<string, string>>(reader.ReadToEnd());
        }
        private static string Describe(string module, string method, string path)
        {
            string description;
            return Descriptions.TryGetValue(module + "|" + method.ToUpperInvariant() + "|" + path.TrimEnd('/'), out description)
                ? description : "用途说明待补充（新接口已自动收录）。";
        }
        public ApiPortalModule()
        {
            Get["/"] = _ =>
            {
                using (var stream = typeof(ApiPortalModule).Assembly.GetManifestResourceStream("WMS.Standalone.Portal.index.html"))
                using (var reader = new StreamReader(stream))
                {
                    Response response = reader.ReadToEnd();
                    response.ContentType = "text/html; charset=utf-8";
                    return response;
                }
            };
            Get["/api-catalog"] = _ =>
            {
                var routes = ReadCache().Where(pair => (pair.Key.Namespace == "WMS.Host.Modules" || (pair.Key.Namespace ?? "").StartsWith("WMS.Host.Modules.", StringComparison.Ordinal)) && pair.Key != typeof(ApiPortalModule))
                    .SelectMany(pair => pair.Value.Select(entry => new
                    {
                        category = Categories.ContainsKey(pair.Key.Name) ? Categories[pair.Key.Name] : "其他接口",
                        module = pair.Key.Name,
                        method = entry.Item2.Method,
                        path = entry.Item2.Path,
                        description = Describe(pair.Key.Name, entry.Item2.Method, entry.Item2.Path)
                    }))
                    .OrderBy(r => r.category).ThenBy(r => r.module).ThenBy(r => r.path).ThenBy(r => r.method).ToArray();
                var framework = ((System.Runtime.Versioning.TargetFrameworkAttribute)Attribute.GetCustomAttribute(
                    typeof(ApiPortalModule).Assembly, typeof(System.Runtime.Versioning.TargetFrameworkAttribute))).FrameworkName;
                Response response = JsonConvert.SerializeObject(new { framework, nancy = typeof(NancyModule).Assembly.GetName().Version.ToString(), routes });
                response.ContentType = "application/json; charset=utf-8";
                return response;
            };
        }
    }
}
