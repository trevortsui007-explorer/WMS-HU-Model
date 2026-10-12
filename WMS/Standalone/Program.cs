using System;
using System.Configuration;
using System.Linq;
using System.Threading;
using Nancy.Hosting.Self;

namespace WMS.Standalone
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            try
            {
                if (args.SequenceEqual(new[] { "--verify-routes" }))
                {
                    var assembly = typeof(Program).Assembly;
                    if (assembly.GetReferencedAssemblies().Any(a => a.Name.StartsWith("Learun", StringComparison.OrdinalIgnoreCase)))
                        throw new InvalidOperationException("Unexpected Learun reference.");
                    var types = assembly.GetTypes().Where(t => !t.IsAbstract && typeof(Nancy.INancyModule).IsAssignableFrom(t)).ToArray();
                    if (types.Length == 0 || types.Any(t => t.Namespace != "WMS.Host.Modules" && !(t.Namespace ?? "").StartsWith("WMS.Host.Modules.", StringComparison.Ordinal)))
                        throw new InvalidOperationException("Unexpected module registration.");
                    var routes = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var type in types)
                    {
                        var module = (Nancy.NancyModule)Activator.CreateInstance(type);
                        foreach (var route in module.Routes)
                        {
                            var key = route.Description.Method + " " + route.Description.Path;
                            if (!routes.Add(key)) throw new InvalidOperationException("Duplicate route: " + key);
                        }
                    }
                    Console.WriteLine("Verified " + types.Length + " independent modules, " + routes.Count + " unique routes; no Learun assembly reference.");
                    return 0;
                }
                if (!args.Contains("--development"))
                    throw new InvalidOperationException("Local development host only. Start with --development explicitly.");
                int port = int.Parse(ConfigurationManager.AppSettings["Host.Port"] ?? "31175");
                if (args.Length == 3 && args[1] == "--port") port = int.Parse(args[2]);
                else if (args.Length != 1) throw new ArgumentException("Usage: --development [--port 31175]");
                if (port < 1024 || port > 65535) throw new ArgumentOutOfRangeException("port");
                var uri = new Uri("http://localhost:" + port + "/");
                var configuration = new HostConfiguration { RewriteLocalhost = false };
                configuration.UrlReservations.CreateAutomatically = false;
                using (var host = new NancyHost(new Bootstrapper(), configuration, uri))
                using (var stop = new ManualResetEventSlim())
                {
                    host.Start();
                    Console.WriteLine("WMS standalone / Nancy 1.4.5 / API catalog: " + uri);
                    Console.WriteLine("Local development only; existing Operator fields remain client supplied. Ctrl+C to stop.");
                    ConsoleCancelEventHandler handler = (sender, e) => { e.Cancel = true; stop.Set(); };
                    Console.CancelKeyPress += handler;
                    try { stop.Wait(); } finally { Console.CancelKeyPress -= handler; }
                }
                return 0;
            }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
    }
}
