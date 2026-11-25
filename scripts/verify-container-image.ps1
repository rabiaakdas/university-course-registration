# No database access. Requires a running Linux Docker engine.
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$suffix = [Guid]::NewGuid().ToString('N')
$name = "university-image-check-$suffix"
$tag = "university-image-check:$suffix"
$scratch = Join-Path $repo "obj/container-image-$suffix"
$exitCode = 0
$dockerReady = $false

function Invoke-Docker([string[]] $Arguments) {
    # Windows PowerShell can wrap native stderr in ErrorRecord, even on success.
    $previous = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $lines = @(& docker @Arguments 2>&1)
        $code = $LASTEXITCODE
    } finally { $ErrorActionPreference = $previous }
    $result = ($lines | ForEach-Object { $_.ToString() }) -join "`n"
    if ($code -ne 0) { throw "Docker command failed (exit $code): $result" }
    return $result
}

Push-Location $repo
try {
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { throw 'Docker CLI is unavailable.' }
    $os = Invoke-Docker @('info', '--format', '{{.OSType}}')
    if ($os.Trim() -ne 'linux') { throw 'A running Linux Docker engine is required; no engine settings were changed.' }
    $dockerReady = $true
    New-Item -ItemType Directory -Path $scratch | Out-Null

    $first = Invoke-Docker @('build', '--progress=plain', '--tag', $tag, '.')
    Write-Host 'PASS image build'
    $second = Invoke-Docker @('build', '--progress=plain', '--tag', $tag, '.')
    # Match the restore instruction's own BuildKit step, not an unrelated CACHED line.
    $restore = [regex]::Match($second, '(?m)^#(\d+) \[build [^\]]+\] RUN dotnet restore UniversityRegistration\.csproj')
    if (-not $restore.Success -or $second -notmatch ('(?m)^#' + $restore.Groups[1].Value + ' CACHED\s*$')) {
        throw 'The unchanged second build did not report a cached restore step.'
    }
    Write-Host 'PASS unchanged second build / restore cache'

    $config = (Invoke-Docker @('image', 'inspect', $tag) | ConvertFrom-Json)[0].Config
    if (($config.Entrypoint -join ' ') -ne 'dotnet UniversityRegistration.dll' -or
        $config.Env -notcontains 'ASPNETCORE_HTTP_PORTS=8080' -or
        $null -eq $config.ExposedPorts.'8080/tcp') { throw 'Unexpected image entrypoint or HTTP port configuration.' }

    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    try { $listener.Start(); $port = $listener.LocalEndpoint.Port } finally { $listener.Stop() }
    # No credentials, host keys, volumes, or Development switches are supplied.
    # Existing LocalDB configuration is NOT a Linux database connection.
    $null = Invoke-Docker @('run', '--detach', '--name', $name, '--publish', "127.0.0.1:${port}:8080",
        '--env', 'ASPNETCORE_ENVIRONMENT=Production', $tag)

    $deadline = [DateTime]::UtcNow.AddSeconds(45)
    $response = $null
    do {
        if ((Invoke-Docker @('inspect', '--format', '{{.State.Running}}', $name)).Trim() -ne 'true') {
            throw 'Application container stopped before HTTP readiness.'
        }
        try { $response = Invoke-WebRequest "http://127.0.0.1:$port/" -UseBasicParsing -TimeoutSec 3 } catch { $response = $null }
        if ($null -eq $response) { Start-Sleep -Milliseconds 250 }
    } while ($null -eq $response -and [DateTime]::UtcNow -lt $deadline)
    if ($null -eq $response -or $response.StatusCode -ne 200 -or
        $response.Headers['Content-Type'] -notmatch '^text/html' -or
        $response.Content -notmatch 'id="login-form"' -or $response.Content -notmatch '/site.js') {
        throw 'Static HTTP smoke failed (this check does not measure database health).'
    }
    foreach ($asset in @('site.js', 'site.css')) {
        $assetResponse = Invoke-WebRequest "http://127.0.0.1:$port/$asset" -UseBasicParsing -TimeoutSec 5
        $expectedType = if ($asset -eq 'site.css') { 'text/css' } else { '(javascript|ecmascript)' }
        if ($assetResponse.StatusCode -ne 200 -or $assetResponse.Headers['Content-Type'] -notmatch $expectedType -or
            $assetResponse.RawContentLength -le 0) { throw "Static asset failed: $asset" }
    }
    $runtimes = Invoke-Docker @('exec', $name, 'dotnet', '--list-runtimes')
    $sdks = Invoke-Docker @('exec', $name, 'dotnet', '--list-sdks')
    if ($runtimes -notmatch 'Microsoft.AspNetCore.App 10\.0\.' -or -not [string]::IsNullOrWhiteSpace($sdks)) {
        throw 'Final image must contain ASP.NET Core 10 runtime and no SDK.'
    }
    foreach ($file in @('UniversityRegistration.dll', 'UniversityRegistration.runtimeconfig.json', 'wwwroot/index.html')) {
        $null = Invoke-Docker @('cp', "${name}:/app/$file", $scratch)
    }
    Write-Host 'PASS runtime/no SDK, publish/static files, Production container and loopback HTTP (no DB check)'
} catch {
    [Console]::Error.WriteLine("FAIL container-image: $($_.Exception.Message)")
    $exitCode = 1
} finally {
    if ($dockerReady) {
        # Only the unique resources named by this invocation; never prune shared caches.
        foreach ($resource in @(@('container', 'ls', '-aq', '--filter', "name=^/$name$"), @('image', 'ls', '-q', $tag))) {
            try {
                $id = Invoke-Docker $resource
                if (-not [string]::IsNullOrWhiteSpace($id)) {
                    if ($resource[0] -eq 'container') { $null = Invoke-Docker @('rm', '--force', $name) }
                    else { $null = Invoke-Docker @('image', 'rm', $tag) }
                }
            } catch { [Console]::Error.WriteLine("Cleanup failed: $($_.Exception.Message)"); $exitCode = 1 }
        }
    }
    try {
        if (Test-Path -LiteralPath $scratch) {
            $resolved = (Resolve-Path -LiteralPath $scratch).Path
            if ($resolved -ne [IO.Path]::GetFullPath($scratch)) { throw 'Unexpected scratch path; cleanup refused.' }
            Remove-Item -LiteralPath $resolved -Recurse -Force
        }
    } catch { [Console]::Error.WriteLine("Scratch cleanup failed: $($_.Exception.Message)"); $exitCode = 1 }
    Pop-Location
}
exit $exitCode
