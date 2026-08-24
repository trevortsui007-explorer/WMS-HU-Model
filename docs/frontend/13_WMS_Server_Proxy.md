# WMS Learun Server Proxy V1

## Purpose

Provide a minimal application-level same-origin bridge from the Learun browser contract `/api/wms/*` to the independent WMS Nancy host. Learun remains a UI host and HTTP adapter; it does not contain WMS business logic or access MesDb/WmsDb.

## Architecture

```text
Browser
-> Learun http://localhost:20471
-> /api/wms/*
-> WmsApiProxyController
-> WmsProxyClient
-> WmsServiceBaseUrl
-> Independent WMS Nancy Host
-> Module -> Service -> Repository -> WmsDb/MesDb
```

No BLL, IBLL, Service, Repository, business DTO, CORS policy, gateway package or frontend BaseUrl was added.

## Route

`RouteConfig` registers this route before the default `{controller}/{action}/{id}` route:

```text
api/wms/{*path} -> WmsApiProxyController.Forward
```

It does not match `/api/health`, other `/api/*` paths, or existing Areas. Only GET and POST are accepted; other methods receive HTTP 405 with an allow header.

## WmsServiceBaseUrl

The only downstream URL source is the existing external appSettings file:

```text
XmlConfig/system.config
WmsServiceBaseUrl=https://localhost:44375
```

The client reads it through `ConfigurationManager.AppSettings`. It must be an absolute HTTP/HTTPS origin without credentials, query, fragment or non-root application path. The value is never returned to JavaScript.

## Authentication Boundary

`WmsApiProxyController` inherits `MvcControllerBase`, which is decorated with `[HandlerLogin(FilterMode.Enforce)]`. Anonymous callers therefore cannot turn the route into an unauthenticated bridge. The proxy does not invent a WMS token and does not forward browser cookies or Authorization headers downstream.

## GET Passthrough

GET preserves the WMS-relative path, raw query string, and allowlisted `Accept` and `X-Requested-With` headers. The target is always constructed as:

```text
WmsServiceBaseUrl + /api/wms/ + validated path + original query string
```

## POST JSON Passthrough

POST reads `Request.InputStream` into bytes and forwards the exact bytes. It does not deserialize or reserialize any WMS request. `Content-Type`, including any charset, is allowlisted and forwarded; therefore JSON encoding is not converted to ANSI/GBK and the proxy has no knowledge of WeekCode/HU fields.

## HTTP Status Passthrough

The controller sets the downstream numeric status, content type and raw response body on the Learun response. Redirect following is disabled. Hop-by-hop headers, Host, Content-Length, Connection and Transfer-Encoding are not copied from the browser.

## HTTP 400 / 500

`HttpWebRequest` exposes downstream 4xx/5xx as `WebException`. When a protocol response exists, `WmsProxyClient` reads that response rather than converting it to a proxy exception. The controller uses `Response.TrySkipIisCustomErrors = true`, so ASP.NET/IIS is instructed not to replace WMS JSON with an HTML error page.

Static readiness is true. Runtime 400 was intentionally not tested because no Recalculate POST was executed.

## 502 Handling

Connection, TLS, timeout and response-stream failures without a downstream HTTP response are mapped to:

```json
{"code":502,"message":"WMS service unavailable.","data":null}
```

The response is HTTP 502 JSON with IIS custom errors bypassed. No exception, stack trace, local path, body, cookie or target query is returned.

## Timeout

Connect/request and read/write timeouts are both 30 seconds. There is no retry and no HTTPS-to-HTTP fallback.

## HTTPS Certificate Policy

The proxy uses the operating system/.NET default certificate validation. It does not use `ServicePointManager.ServerCertificateValidationCallback` and does not disable TLS validation globally or locally.

Current runtime check of `https://localhost:44375` failed during the TLS handshake with a local IIS Express certificate/credential error. Safe options require Human Review:

1. Trust/repair the current IIS Express localhost development certificate — preferred.
2. Explicitly set the Development-only `WmsServiceBaseUrl` to `http://localhost:52602`. This is a config-only choice, never an automatic code fallback.

## Security / SSRF

- No client `url`, target, host, port or scheme input is accepted.
- The target origin comes only from `WmsServiceBaseUrl`.
- Paths beginning with `/`, absolute/protocol-like paths, backslashes, query/fragment characters, residual percent-encoding, and `.`/`..` segments are rejected.
- Only the `/api/wms/*` prefix is appended.
- Request headers use a small allowlist; cookies, Authorization and hop-by-hop headers are excluded.

## Development Setup

1. Ensure the WMS HTTPS binding is available at `https://localhost:44375` and the IIS Express localhost certificate is trusted by the Learun process identity.
2. Start/restart Learun at `http://localhost:20471` so route/config changes load into a new AppDomain.
3. Sign in normally through Learun.
4. Run only the safe Audit GET through `/api/wms/weekcode/audit?plantCode=1020&page=1&pageSize=1`.
5. Compare its status, content type, total count and first-item key facts with the direct WMS GET.

Do not execute Recalculate during this verification.

## Production Deployment

This V1 is an application-level same-origin bridge. If production provides IIS ARR, Nginx or an API Gateway, infrastructure may route `/api/wms/*` directly to the WMS host. Because the browser contract stays unchanged, no WeekCode JavaScript or visual changes are required.

## Runtime Verification

```text
BUILD_PASS=True
STATIC_CHECK_PASS=True

DIRECT_WMS_AUDIT_GET_STATUS=TLS_HANDSHAKE_FAILED
PROXY_WMS_AUDIT_GET_STATUS=NOT_EXECUTED
SAFE_AUDIT_GET_RUNTIME_PASS=False
DIRECT_PROXY_AUDIT_SEMANTICS_MATCH=False

HTTPS_CERTIFICATE_TRUST_FAILED=True
REAL_RECALCULATE_POST_EXECUTED=False
DATABASE_WRITE_EXECUTED=False
```

Learun runtime start approval was declined, so the new route was not exercised on port 20471. Runtime PASS is intentionally not claimed. The next verification must occur after Human fixes/trusts the HTTPS development certificate and starts Learun.
