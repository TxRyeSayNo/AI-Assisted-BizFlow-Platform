[CmdletBinding()]
param([switch]$Build, [switch]$Browser)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http
$project = 'bizflow-smoke-' + [Guid]::NewGuid().ToString('N')
$compose = @('compose', '--project-name', $project, '--env-file', (Join-Path $PSScriptRoot '.env.example'),
    '-f', (Join-Path $PSScriptRoot 'docker-compose.yml'), '-f', (Join-Path $PSScriptRoot 'docker-compose.app.yml'), '--profile', 'tools')
$names = @('POSTGRES_DB', 'POSTGRES_USER', 'POSTGRES_PASSWORD', 'POSTGRES_PORT', 'BIZFLOW_DATABASE_CONNECTION',
    'JWT_SIGNING_KEY_BASE64', 'JWT_ISSUER', 'JWT_AUDIENCE', 'WEB_PORT', 'BIZFLOW_IMAGE_PREFIX', 'BIZFLOW_SMOKE_URL')
$previous = @{}
foreach ($name in $names) { $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
$started = $false
$http = $null

function Invoke-Compose([string[]]$Arguments) {
    & docker @compose @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Container command failed (exit $LASTEXITCODE)." }
}
function Get-AvailablePort {
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    $listener.Start()
    try { return $listener.LocalEndpoint.Port } finally { $listener.Stop() }
}
function Assert-Http([string]$Path, [int]$Status, [string]$ContentType) {
    $response = $http.GetAsync($Path).GetAwaiter().GetResult()
    try {
        if ([int]$response.StatusCode -ne $Status -or $response.Content.Headers.ContentType.MediaType -ne $ContentType) {
            throw "Unexpected HTTP status/content type for $Path."
        }
        return $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    } finally { $response.Dispose() }
}

try {
    # Process-scoped, ephemeral secrets. Never write them to disk or print compose config.
    $env:POSTGRES_DB = 'bizflow_smoke'
    $env:POSTGRES_USER = 'bizflow_smoke'
    $env:POSTGRES_PASSWORD = [Guid]::NewGuid().ToString('N') + [Guid]::NewGuid().ToString('N')
    $env:POSTGRES_PORT = [string](Get-AvailablePort)
    $env:WEB_PORT = [string](Get-AvailablePort)
    $env:BIZFLOW_DATABASE_CONNECTION = "Host=postgres;Database=bizflow_smoke;Username=bizflow_smoke;Password=$env:POSTGRES_PASSWORD;GSS Encryption Mode=Disable"
    $keyBytes = New-Object byte[] 32
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($keyBytes) } finally { $rng.Dispose() }
    $env:JWT_SIGNING_KEY_BASE64 = [Convert]::ToBase64String($keyBytes)
    $env:JWT_ISSUER = 'bizflow-container-smoke'
    $env:JWT_AUDIENCE = 'bizflow-container-smoke'
    $env:BIZFLOW_IMAGE_PREFIX = 'bizflow'
    Invoke-Compose @('config', '--quiet')
    if ($Build) { Invoke-Compose @('build', 'api', 'web', 'migrate') }
    $started = $true
    Invoke-Compose @('up', '-d', '--wait', '--wait-timeout', '90', 'postgres')
    Invoke-Compose @('run', '--rm', 'migrate')
    Invoke-Compose @('run', '--rm', 'migrate') # Idempotent replay against the same isolated database.
    Invoke-Compose @('up', '-d', '--wait', '--wait-timeout', '90', 'api', 'web')

    $http = [Net.Http.HttpClient]::new()
    $http.BaseAddress = [Uri]"http://127.0.0.1:$env:WEB_PORT"
    $http.Timeout = [TimeSpan]::FromSeconds(15)
    $page = Assert-Http '/login' 200 'text/html'
    if (-not $page.Contains('<app-root>')) { throw 'The built Angular application was not served.' }
    $null = Assert-Http '/platform/companies' 200 'text/html'
    $health = (Assert-Http '/health/live' 200 'application/json') | ConvertFrom-Json
    if ($health.status -ne 'healthy') { throw 'API liveness failed.' }
    $readiness = (Assert-Http '/health/ready' 200 'application/json') | ConvertFrom-Json
    if ($readiness.status -ne 'ready') { throw 'Migrated API readiness failed.' }
    # Exercise outage/recovery only in this invocation's disposable project.
    Invoke-Compose @('stop', 'postgres')
    $notReady = (Assert-Http '/health/ready' 503 'application/json') | ConvertFrom-Json
    if ($notReady.code -ne 'SERVICE.NOT_READY') { throw 'Database outage was not reported safely.' }
    $null = Assert-Http '/health/live' 200 'application/json'
    Invoke-Compose @('up', '-d', '--wait', '--wait-timeout', '90', 'postgres')
    $null = Assert-Http '/health/ready' 200 'application/json'
    # Check the deployed authorization boundary for every currently implemented read endpoint.
    # The fixed UUID is an absent test resource, not a seeded production identity.
    $resourceId = '00000000-0000-7000-8000-000000000001'
    foreach ($path in @('/api/v1/departments', '/api/v1/users', '/api/v1/roles', '/api/v1/services',
        '/api/v1/tasks', '/api/v1/notifications', '/api/v1/audit-logs', '/api/v1/workflows',
        "/api/v1/workflow-versions/$resourceId", '/api/v1/sla-profiles',
        "/api/v1/sla-profiles/$resourceId/versions", '/api/v1/platform/company-registrations')) {
        $denied = (Assert-Http $path 401 'application/json') | ConvertFrom-Json
        if ($denied.code -ne 'AUTH.REQUIRED') { throw "Unauthorized API response lost its error contract at $path." }
    }
    $missing = (Assert-Http '/api/v1/missing' 404 'application/json') | ConvertFrom-Json
    if ($missing.code -ne 'RESOURCE.NOT_FOUND') { throw 'API 404 was replaced by the SPA.' }
    $content = [Net.Http.StringContent]::new('{"identifier":"missing-smoke-account","password":"not-a-real-password"}', [Text.Encoding]::UTF8, 'application/json')
    try {
        $login = $http.PostAsync('/api/v1/auth/login', $content).GetAwaiter().GetResult()
        try { if ([int]$login.StatusCode -ne 401) { throw 'Login did not reach the migrated database safely.' } }
        finally { $login.Dispose() }
    } finally { $content.Dispose() }

    foreach ($service in @('api', 'web')) {
        $container = Invoke-Compose @('ps', '-q', $service)
        $runtimeUser = & docker inspect --format '{{.Config.User}}' $container
        if ($LASTEXITCODE -ne 0 -or $runtimeUser -in @('', '0', 'root')) { throw "$service is not configured as a non-root container." }
        $readOnly = & docker inspect --format '{{.HostConfig.ReadonlyRootfs}}' $container
        if ($LASTEXITCODE -ne 0 -or $readOnly -ne 'true') { throw "$service does not have a read-only root filesystem." }
    }
    if ($Browser) {
        $env:BIZFLOW_SMOKE_URL = $http.BaseAddress.AbsoluteUri
        & node (Join-Path $PSScriptRoot '../tests/BizFlow.E2ETests/container-smoke.cjs')
        if ($LASTEXITCODE -ne 0) { throw 'Packaged application browser check failed.' }
    }
    Write-Output 'PASS: built SPA routing, API proxy/error contracts, explicit repeatable migrations, readiness outage/recovery, database login path, non-root and read-only runtimes.'
} finally {
    if ($null -ne $http) { $http.Dispose() }
    if ($started) {
        # Delete only resources owned by this invocation's generated test project, never local/demo data.
        if ($project -notmatch '^bizflow-smoke-[0-9a-f]{32}$') { throw 'Refusing cleanup of an unexpected project name.' }
        try {
            Invoke-Compose @('down', '--volumes', '--remove-orphans')
            Write-Output 'Removed the isolated smoke-test containers, network and disposable database volume; built images/cache remain.'
        } finally {
            foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process') }
        }
    } else {
        foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process') }
    }
}
