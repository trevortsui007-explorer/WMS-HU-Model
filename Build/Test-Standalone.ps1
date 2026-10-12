param([ValidateSet('Debug','Release')][string]$Configuration = 'Debug')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$exe = Join-Path $root "WMS/Standalone/bin/$Configuration/WMS.Standalone.exe"
& $exe --verify-routes
if ($LASTEXITCODE -ne 0) { throw 'Route/assembly verification failed.' }
$denied = Start-Process -FilePath $exe -WindowStyle Hidden -PassThru -Wait
if ($denied.ExitCode -eq 0) { throw 'Host must require explicit development mode.' }
$probe = New-Object Net.Sockets.TcpListener([Net.IPAddress]::Loopback, 0)
$probe.Start()
$port = $probe.LocalEndpoint.Port
$probe.Stop()
$process = Start-Process -FilePath $exe -ArgumentList @('--development', '--port', $port) -WorkingDirectory (Split-Path $exe -Parent) -WindowStyle Hidden -PassThru
try {
    $response = $null
    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        if ($process.HasExited) { throw "Host exited: $($process.ExitCode). Check HTTP.sys URL permissions." }
        try { $response = Invoke-WebRequest "http://localhost:$port/api/wms-fg/health" -UseBasicParsing -TimeoutSec 2; break }
        catch { Start-Sleep -Milliseconds 250 }
    }
    if ($null -eq $response -or $response.StatusCode -ne 200) { throw 'Health endpoint unavailable.' }
    $body = $response.Content | ConvertFrom-Json
    if ($body.code -ne 200 -or $body.data.status -ne 'Running' -or $body.data.framework -ne '.NET Framework 4.6.1') { throw 'Health contract mismatch.' }
    if ($response.Headers['Cache-Control'] -notmatch 'no-store') { throw 'Missing no-store header.' }
    $pageResponse = Invoke-WebRequest "http://localhost:$port/" -UseBasicParsing
    if ($pageResponse.StatusCode -ne 200 -or $pageResponse.Headers['Content-Type'] -notmatch 'text/html' -or $pageResponse.Content -notmatch 'id="search"') { throw 'API portal HTML missing.' }
    $catalog = Invoke-RestMethod "http://localhost:$port/api-catalog"
    $expectedRoutes = 0
    foreach ($file in (Get-ChildItem -LiteralPath (Join-Path $root 'WMS/Host.Modules') -Filter '*.cs' -Recurse | Where-Object { $_.FullName -notmatch '[\\/]Portal[\\/]' })) {
        $expectedRoutes += [regex]::Matches([IO.File]::ReadAllText($file.FullName), '(Get|Post|Put|Delete|Patch|Options|Head)\["').Count
    }
    if ($catalog.routes.Count -ne $expectedRoutes -or $expectedRoutes -eq 0) { throw 'Catalog count differs from business module routes.' }
    if ($catalog.routes | Where-Object { [string]::IsNullOrWhiteSpace($_.category) }) { throw 'Missing category.' }
    $keys = @($catalog.routes | ForEach-Object { $_.method + ' ' + $_.path })
    if (@($keys | Sort-Object -Unique).Count -ne $keys.Count) { throw 'Duplicate catalog routes.' }
    if ($keys -notcontains 'GET /api/wms-fg/locations' -or $keys -notcontains 'POST /api/wms-fg/hus/{huCode}/hold') { throw 'Missing representative catalog routes.' }
    if ($catalog.routes | Where-Object { [string]::IsNullOrWhiteSpace($_.description) -or $_.description -match '待补充' }) { throw 'Chinese descriptions incomplete.' }
    if ($catalog.routes | Where-Object { $_.module -eq 'ApiPortalModule' }) { throw 'Portal routes should not appear as business APIs.' }
    Write-Host 'PASS: route isolation, development gate, HTTP health, API portal HTML, catalog coverage and Chinese descriptions, cache policy. No database or business operation called.'
} finally {
    if (!$process.HasExited) { Stop-Process -Id $process.Id }
}
