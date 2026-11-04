param(
    [Parameter(Mandatory=$true)][ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version,
    [Parameter(Mandatory=$true)][string]$NotesFile,
    [string]$Commit = ''
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$workspace = Split-Path -Parent $repo
$outputs = Join-Path $workspace 'outputs'
if (!$Commit) { $Commit = (git -C $repo rev-parse HEAD).Trim() }
if ($Commit -notmatch '^[a-fA-F0-9]{40}$') { throw 'Release requires an exact tested commit.' }
$tag = 'v' + $Version
$api = 'https://api.github.com/repos/lucjon7722-web/Incranalytic'
$assets = @("Incranalytic-$Version-Setup.exe", "Incranalytic-$Version-Setup.sha256", 'Incranalytic-Installer.exe', 'Incranalytic-Installer.sha256', 'Version-Manager-Guide.md', "Incranalytic-$Version-verification.txt")
foreach ($name in $assets) { if (!(Test-Path -LiteralPath (Join-Path $outputs $name))) { throw "Missing release asset: $name" } }
$notes = Get-Content -LiteralPath $NotesFile -Raw
$env:GIT_TERMINAL_PROMPT = '0'
try {
    # Use the normal developer Git credential helper. Never print or package credentials.
    $lines = "protocol=https`nhost=github.com`n`n" | git credential fill 2>$null
    if ($LASTEXITCODE -ne 0) { throw 'GitHub authentication unavailable.' }
    $passwordLine = $lines | Where-Object { $_.StartsWith('password=') }
    if (!$passwordLine) { throw 'No GitHub authentication returned.' }
    $headers = @{ Authorization=('Bearer ' + $passwordLine.Substring(9)); Accept='application/vnd.github+json'; 'X-GitHub-Api-Version'='2022-11-28'; 'User-Agent'='Incranalytic-Release' }
    $release = @(Invoke-RestMethod -Uri ($api + '/releases') -Headers $headers) | Where-Object tag_name -EQ $tag | Select-Object -First 1
    if (!$release) {
        $body = @{ tag_name=$tag; target_commitish=$Commit; name="Incranalytic Studio $Version"; body=$notes; draft=$true; prerelease=$false } | ConvertTo-Json
        $release = Invoke-RestMethod -Uri ($api + '/releases') -Method Post -Headers $headers -ContentType 'application/json' -Body $body
    }
    try { $reference = Invoke-RestMethod -Uri ($api + '/git/ref/tags/' + $tag) -Headers $headers }
    catch {
        if ([int]$_.Exception.Response.StatusCode -ne 404) { throw }
        $body = @{ ref=('refs/tags/' + $tag); sha=$Commit } | ConvertTo-Json
        $reference = Invoke-RestMethod -Uri ($api + '/git/refs') -Method Post -Headers $headers -ContentType 'application/json' -Body $body
    }
    $tagCommit = $reference.object.sha
    if ($reference.object.type -eq 'tag') { $tagCommit = (Invoke-RestMethod -Uri ($api + '/git/tags/' + $tagCommit) -Headers $headers).object.sha }
    if ($tagCommit -ne $Commit) { throw 'Release tag does not point at the tested source.' }
    foreach ($name in $assets) {
        $path = Join-Path $outputs $name
        $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
        $existing = @(Invoke-RestMethod -Uri ($api + '/releases/' + $release.id + '/assets') -Headers $headers) | Where-Object name -EQ $name
        if (!$existing) {
            $uri = $release.upload_url.Split('{')[0] + '?name=' + [uri]::EscapeDataString($name)
            $existing = Invoke-RestMethod -Uri $uri -Method Post -Headers $headers -ContentType 'application/octet-stream' -InFile $path
        }
        if ($existing.size -ne (Get-Item -LiteralPath $path).Length) { throw "Uploaded size mismatch: $name" }
        if ($existing.digest -ne ('sha256:' + $hash)) { throw "Uploaded SHA-256 mismatch: $name" }
        Write-Output "Verified release asset: $name"
    }
    $publish = @{ draft=$false; prerelease=$false; make_latest='true'; body=$notes } | ConvertTo-Json
    Invoke-RestMethod -Uri ($api + '/releases/' + $release.id) -Method Patch -Headers $headers -ContentType 'application/json' -Body $publish | Out-Null
    $verified = Invoke-RestMethod -Uri ($api + '/releases/tags/' + $tag) -Headers $headers
    if ($verified.draft -or $verified.assets.Count -lt $assets.Count) { throw 'Published release verification failed.' }
    Write-Output $verified.html_url
} finally {
    if ($headers) { $headers.Clear() }
    $lines=$null; $passwordLine=$null
}
