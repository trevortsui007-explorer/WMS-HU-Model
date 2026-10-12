using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Nancy;
using Nancy.Bootstrapper;
using Nancy.TinyIoc;
using Newtonsoft.Json;

namespace WMS.Standalone
{
    public sealed class Bootstrapper : DefaultNancyBootstrapper
    {
        protected override IEnumerable<ModuleRegistration> Modules
        {
            get
            {
                return typeof(Bootstrapper).Assembly.GetTypes()
                    .Where(t => (t.Namespace == "WMS.Host.Modules" || (t.Namespace ?? "").StartsWith("WMS.Host.Modules.", StringComparison.Ordinal)) && !t.IsAbstract && typeof(INancyModule).IsAssignableFrom(t))
                    .Select(t => new ModuleRegistration(t));
            }
        }
        protected override void ApplicationStartup(TinyIoCContainer container, IPipelines pipelines)
        {
            base.ApplicationStartup(container, pipelines);
            global::Map.Application.MapIdentity.Resolve = context => System.Security.Principal.WindowsIdentity.GetCurrent().Name;
            WMS.Host.Modules.Portal.ApiPortalModule.ReadCache = () => container.Resolve<Nancy.Routing.IRouteCache>();
            pipelines.OnError += (context, error) =>
            {
                var id = Guid.NewGuid().ToString("D");
                Trace.TraceError("WMS request {0}: {1}", id, error);
                // Preserve the legacy HTTP 200/code=500/info error envelope without exposing SQL details.
                Response response = JsonConvert.SerializeObject(new { code = 500, info = "Internal server error. Trace: " + id, data = (object)null });
                response.ContentType = "application/json; charset=utf-8";
                return response;
            };
            pipelines.AfterRequest += context =>
            {
                if (context.Response != null) context.Response.Headers["Cache-Control"] = "no-store";
            };
        }
    }
}
