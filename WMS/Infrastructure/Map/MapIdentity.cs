using System;
using Nancy;
namespace Map.Application
{
    // Host adapter only. Learun must supply an authenticated actor; default is deny.
    public static class MapIdentity
    {
        public static Func<NancyContext, string> Resolve { get; set; }
    }
}
