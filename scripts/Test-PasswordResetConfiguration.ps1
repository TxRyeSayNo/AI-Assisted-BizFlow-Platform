param(
    [string]$EnvFile = (Join-Path $PSScriptRoot '..\deploy\.env'),
    [switch]$CheckConnection
)
$ErrorActionPreference = 'Stop'
$settings = @{}
if (Test-Path -LiteralPath $EnvFile) {
    foreach ($line in Get-Content -LiteralPath $EnvFile) {
        if ($line.Trim().StartsWith('#') -or -not $line.Contains('=')) { continue }
        $pair = $line -split '=', 2
        $settings[$pair[0].Trim()] = $pair[1].Trim()
    }
}
function Setting([string]$Name) {
    $value = [Environment]::GetEnvironmentVariable($Name)
    if ([string]::IsNullOrEmpty($value)) { return $settings[$Name] }
    return $value
}
$problems = [System.Collections.Generic.List[string]]::new()
if ((Setting 'ResetEmail__Enabled') -ne 'true') { $problems.Add('ResetEmail__Enabled is not true: delivery remains disabled.') }
foreach ($name in @('ResetEmail__Host', 'ResetEmail__FromAddress', 'ResetEmail__FrontendOrigin')) {
    if ([string]::IsNullOrWhiteSpace((Setting $name))) { $problems.Add("Missing $name") }
}
if ((Setting 'ResetEmail__Username') -and -not (Setting 'ResetEmail__Password')) {
    $problems.Add('Missing ResetEmail__Password for the configured SMTP username.')
}
$port = 587
$configuredPort = Setting 'ResetEmail__Port'
if ($configuredPort -and (-not [int]::TryParse($configuredPort, [ref]$port) -or $port -lt 1 -or $port -gt 65535)) {
    $problems.Add('ResetEmail__Port must be between 1 and 65535.')
}
$origin = $null
$originText = Setting 'ResetEmail__FrontendOrigin'
if ($originText -and (-not [Uri]::TryCreate($originText, [UriKind]::Absolute, [ref]$origin) -or
    $origin.AbsolutePath -ne '/' -or $origin.Query -or $origin.Fragment -or $origin.UserInfo -or
    ($origin.Scheme -ne 'https' -and -not ($origin.Scheme -eq 'http' -and $origin.IsLoopback)))) {
    $problems.Add('FrontendOrigin must be an HTTPS origin without a path, query or fragment; loopback HTTP is permitted locally.')
}
$sender = Setting 'ResetEmail__FromAddress'
if ($sender) {
    try { $null = [System.Net.Mail.MailAddress]::new($sender) }
    catch { $problems.Add('ResetEmail__FromAddress is not a valid sender address.') }
}
if ($problems.Count) {
    $problems | ForEach-Object { Write-Output "[NEEDS CONFIGURATION] $_" }
    Write-Output 'No email was sent. See docs/02-traceability/password-reset-setup.vi.md.'
    exit 1
}
Write-Output 'Required configuration is present. SMTP credentials and inbox delivery have not been verified.'
if ($CheckConnection) {
    $connection = [System.Net.Sockets.TcpClient]::new()
    try {
        $pending = $connection.ConnectAsync((Setting 'ResetEmail__Host'), $port)
        if (-not $pending.Wait(10000) -or -not $connection.Connected) { throw 'Connection unavailable' }
        Write-Output 'SMTP TCP connection succeeded. This does not validate TLS, authentication or message delivery.'
    } catch { Write-Output 'SMTP TCP connection failed. Check host, port and outbound firewall rules.'; exit 1 }
    finally { $connection.Dispose() }
}
Write-Output 'No email was sent. Secret values are not printed by this check.'
