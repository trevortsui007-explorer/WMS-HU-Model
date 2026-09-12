# WMS API Runtime Route Alignment

## Scope

This gate only verifies how the Learun browser origin reaches the independent WMS Nancy HTTP API. It does not inspect or change SQL, repositories, connection strings, MesDb/WmsDb, business logic, or the approved WeekCode page visuals. No Recalculate POST was executed.

## Learun Origin

The active Visual Studio/IIS Express project settings select:

```text
LEARUN_RUNTIME_ORIGIN=http://localhost:20471
scheme=http
host=localhost
port=20471
applicationPath=/
```

Evidence:

- `Learun.Application.Web.csproj` sets `IISUrl` to `http://localhost:20471/`.
- `.vs/LeaRun.Framework V7.05/config/applicationhost.config` site id 6 maps `/` to `D:\com.delton.gz\Learun.Application.Web` and binds `*:20471:localhost`.
- The site was started from that exact applicationhost configuration. `GET /` returned HTTP 200; `GET /WMS/FG_WeekCodeAudit/Index` returned the expected unauthenticated HTTP 302.
- The project also retains older 20472 hints (`DevelopmentServerPort`, another applicationhost site, and `Serverurl`). That port was not listening during this test and is not treated as the active origin.

## WMS Origin

```text
WMS_RUNTIME_ORIGIN=http://localhost:52602
```

Evidence:

- `WMS.csproj` declares development port 52602.
- `.vs/WMS-HU-Model/config/applicationhost.config` binds the WMS application root to HTTP 52602 and HTTPS 44375.
- Runtime recheck: `GET http://localhost:52602/api/health` returned HTTP 200 and JSON.
- Runtime recheck: `GET http://localhost:52602/api/wms/weekcode/audit?plantCode=1020&page=1&pageSize=1` returned HTTP 200 and JSON with `Code=200`.

## Current Route Mode

```text
WMS_API_ROUTE_MODE=LEARUN_SERVER_PROXY
```

The WeekCode API wrapper still builds `Wms.Util.rootUrl() + "/api/wms/weekcode"`, so the browser contract remains same-origin. Learun now registers `api/wms/{*path}` before the default MVC route and forwards only that route to the server-side `WmsServiceBaseUrl`. The browser never receives the downstream origin.

## Relative Route Test

Request made against the running Learun site:

```text
GET http://localhost:20471/api/wms/weekcode/audit?plantCode=1020&page=1&pageSize=1
HTTP 404
```

The same path on WMS port 52602 returned HTTP 200 and `Code=200`. This was the pre-implementation evidence that caused the server proxy to be added.

After implementation, Learun runtime restart approval was declined, so the updated route could not be exercised through port 20471 in this run. Separately, the configured `https://localhost:44375` downstream failed the system-default TLS handshake with a local IIS Express certificate/credential error. No certificate bypass and no automatic HTTP downgrade were added. The relative Audit GET therefore remains runtime-unverified.

## Proxy / Rewrite Evidence

- Learun `Web.config` and IIS Express applicationhost still contain no ARR/rewrite rule.
- `RouteConfig` now registers only `api/wms/{*path}` to `WmsApiProxyController.Forward` before the default route.
- `WmsApiProxyController` inherits `MvcControllerBase`, reusing `[HandlerLogin(FilterMode.Enforce)]`.
- `WmsProxyClient` forwards GET/POST to the configured origin without BLL, Service, Repository, database access or business DTO mapping.
- `XmlConfig/system.config` contains the single downstream source `WmsServiceBaseUrl=https://localhost:44375`.
- `FG_WeekCodeAuditController` remains view-only and business JavaScript remains unchanged.
- No business JavaScript hard-codes port 52602.

```text
REVERSE_PROXY_FOUND=False
SERVER_PROXY_FOUND=True
```

## HTTP 200 Handling

The client copies downstream HTTP status, content type and raw body to the Learun response and sets `TrySkipIisCustomErrors=true`. This is statically ready, but proxy HTTP 200 passthrough is not runtime-verified because the configured HTTPS downstream did not complete TLS and Learun restart approval was declined.

## HTTP 400 Handling

Static WMS evidence is correct at the source boundary:

- `WeekCodeRecalculateModule` returns `HttpStatusCode.BadRequest` with JSON for business failures.
- WMS `Web.config` uses `<httpErrors existingResponse="PassThrough" />`.

Protocol-error responses are read from `WebException.Response`, preserving their status, content type and raw body, and the controller sets `TrySkipIisCustomErrors=true`. HTTP 400/500 passthrough is statically ready but was not runtime-tested. No Recalculate POST was sent.

## JSON POST Compatibility

The proxy supports GET and POST. POST reads `Request.InputStream` as raw bytes without deserializing or transcoding, then forwards those bytes with the allowlisted `Content-Type`; query string and `Accept`/`X-Requested-With` are also preserved. End-to-end JSON POST remains runtime-unverified because no real Recalculate POST was allowed.

## Minimal Solution Comparison

1. **Same-origin reverse proxy — recommended.** Configure the Learun deployment so `/api/wms/{R}` forwards to `http://localhost:52602/api/wms/{R}` in Development. Keep the target in one environment-level deployment setting, not business JavaScript. ARR/proxy must be enabled at the server level, methods and bodies must be preserved, and custom errors must not replace WMS 400/500 JSON.
2. **Learun server proxy.** Use only if deployment cannot provide a reverse proxy. A thin allowlisted proxy may forward requests, but it must not copy WMS business logic and must stream status/body/content type unchanged.
3. **Direct cross-origin.** Last choice. It requires explicit WMS CORS policy and a single environment-specific service BaseUrl. Port 52602 must not be embedded in `Index.js`, `WeekCodeAuditApi.js`, or the shared API wrapper.

V1 implements option 2, the Learun server proxy, because no infrastructure reverse proxy is currently available. A future ARR/Nginx/API Gateway route can replace the application bridge without changing the browser `/api/wms/*` contract.

## Build Verification

`Learun.Application.Web.csproj` Debug build passed with `BUILD_EXIT=0`. Existing warnings remain, including two non-blocking `MSB3101` cache access warnings; `Learun.Application.Web.dll` was generated successfully.

## Final Decision

```text
WMS_API_ROUTE_ALIGNMENT_COMPLETE=False
WMS_API_ROUTE_ALIGNMENT_PASS=False

LEARUN_RELATIVE_WMS_ROUTE_PASS=False
HTTP_200_PASSTHROUGH_VERIFIED=False
HTTP_400_JSON_PASSTHROUGH_VERIFIED=False
HTTP_400_PASSTHROUGH_STATIC_READY=True
JSON_POST_ROUTE_COMPATIBLE=True

DATABASE_CONFIG_ALIGNMENT_PASS=True
WMS_RUNTIME_WMSDB_READ_PASS=True
WMS_RUNTIME_MESDB_READ_PASS=True
LEARUN_DIRECT_DB_CONNECTION_REQUIRED=False
LEARUN_FACTORY_DB_PREFLIGHT_BLOCKING=False
```

The application route is implemented, but the route gate can pass only after Human fixes/trusts the localhost IIS Express HTTPS certificate (or explicitly selects the documented Development-only HTTP config), starts Learun, and confirms the Learun-origin Audit GET returns HTTP 200/`Code=200`. A later controlled test must verify HTTP 400 JSON preservation. The Learun Factory database preflight is not part of that acceptance path.
