using Nancy;
using Nancy.Configuration;

namespace WMS
{
    public class Bootstrapper : DefaultNancyBootstrapper
    {
        public override void Configure(INancyEnvironment environment)
        {
            base.Configure(environment);

            environment.Tracing(
                enabled: false,
                displayErrorTraces: true);
        }
    }
}
