[CmdletBinding(DefaultParameterSetName='Submission')]
param(
    [Parameter(Mandatory=$true)][string]$MakeAppx,
    [Parameter(Mandatory=$true,ParameterSetName='Preflight')][switch]$Preflight,
    [Parameter(Mandatory=$true,ParameterSetName='Submission')][string]$IdentityFile,
    [ValidatePattern('^\d+\.\d+\.\d+\.0$')][string]$PackageVersion = '0.5.0.0'
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$workspace = Split-Path -Parent $repo
$makeAppxPath = (Resolve-Path -LiteralPath $MakeAppx).Path
$signature = Get-AuthenticodeSignature -LiteralPath $makeAppxPath
if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation') { throw 'Use an officially signed Microsoft MakeAppx tool.' }
foreach($component in $PackageVersion.Split('.')) { if([long]$component -gt 65535) { throw 'MSIX version components must be between 0 and 65535.' } }
if (!$Preflight) {
    $identity = Get-Content -LiteralPath $IdentityFile -Raw | ConvertFrom-Json
    if ($identity.IdentityName -notmatch '^[A-Za-z0-9.-]{3,50}$' -or $identity.Publisher -notmatch '^CN=.+' -or [string]::IsNullOrWhiteSpace($identity.PublisherDisplayName)) { throw 'Copy all three exact package identity fields from Partner Center.' }
    if (($identity | ConvertTo-Json) -match '(?i)COPY-|preflight|placeholder|example') { throw 'Placeholder identity values cannot be used for a submission build.' }
}
$work = Join-Path $workspace ('work\store-build-' + [Guid]::NewGuid().ToString('N'))
$payload = Join-Path $work 'payload'
$app = Join-Path $payload 'app'
$output = Join-Path $workspace 'outputs\microsoft-store'
New-Item -ItemType Directory -Force -Path $app,$output | Out-Null
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
dotnet publish (Join-Path $repo 'src\Incranalytic.App') -c Release -r win-x64 --self-contained true -p:StoreBuild=true -p:DebugSymbols=false -p:DebugType=None -o $app
if ($LASTEXITCODE -ne 0) { throw 'Store edition publish failed.' }
& (Join-Path $PSScriptRoot 'Copy-RuntimeNotices.ps1') -PayloadDirectory $app
Copy-Item -LiteralPath (Join-Path $repo 'store\AppxManifest.xml') -Destination (Join-Path $payload 'AppxManifest.xml')
[xml]$manifest = Get-Content -LiteralPath (Join-Path $payload 'AppxManifest.xml') -Raw
$manifest.Package.Identity.Version = $PackageVersion
if (!$Preflight) {
    $manifest.Package.Identity.Name = [string]$identity.IdentityName
    $manifest.Package.Identity.Publisher = [string]$identity.Publisher
    $manifest.Package.Properties.PublisherDisplayName = [string]$identity.PublisherDisplayName
}
$manifest.Save((Join-Path $payload 'AppxManifest.xml'))
& (Join-Path $PSScriptRoot 'New-StoreAssets.ps1') -OutputDirectory (Join-Path $payload 'Assets')
$copies = @{
    'store\Terms.txt'='Terms.txt'; 'store\privacy.md'='privacy.md'; 'LICENSE'='Studio-Source-Terms.txt';
    'EXPORT-LICENSE.txt'='EXPORT-LICENSE.txt'; 'THIRD-PARTY-NOTICES.md'='THIRD-PARTY-NOTICES.md';
    'docs\AI-DEVELOPMENT.md'='AI-DEVELOPMENT.md'; 'docs\studio-guide.md'='studio-guide.md'; 'docs\IFCLOUD.md'='IFCLOUD.md'
}
foreach($source in $copies.Keys) { Copy-Item -LiteralPath (Join-Path $repo $source) -Destination (Join-Path $app $copies[$source]) }
New-Item -ItemType Directory -Path (Join-Path $app 'examples') | Out-Null
Copy-Item -LiteralPath (Join-Path $repo 'samples\conditional-counter.incr') -Destination (Join-Path $app 'examples\conditional-counter.incr')
$report = Join-Path $work 'studio-verification.txt'
$process = Start-Process -FilePath (Join-Path $app 'Incranalytic.exe') -ArgumentList ('--verify-studio "' + $report + '"') -PassThru -WindowStyle Hidden
if (!$process.WaitForExit(60000)) { $process.Kill(); throw 'Store studio checks timed out. Review the test report.' }
if ($process.ExitCode -ne 0 -or !(Test-Path -LiteralPath $report)) { throw 'Store studio checks failed.' }
$checks = @(Get-Content -LiteralPath $report)
if (@($checks | Where-Object { $_ -like 'PASS:*' }).Count -ne 22 -or @($checks | Where-Object { $_ -like 'FAIL:*' }).Count -ne 0) { throw 'Expected 22 successful Store studio checks.' }
if (Test-Path -LiteralPath (Join-Path $app 'manager')) { throw 'The Store package must not include the GitHub Version Manager.' }
$suffix = if ($Preflight) { 'PREFLIGHT-NOT-FOR-SUBMISSION' } else { 'Store-Submission' }
$package = Join-Path $output "Incranalytic-$PackageVersion-x64-$suffix.msix"
& $makeAppxPath pack /d $payload /p $package /o
if ($LASTEXITCODE -ne 0) { throw 'Microsoft MakeAppx schema/packaging validation failed.' }
$hash = (Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath ($package + '.sha256') -Value ($hash + '  ' + [IO.Path]::GetFileName($package)) -Encoding ascii
Copy-Item -LiteralPath $report -Destination (Join-Path $output 'store-studio-verification.txt')
Copy-Item -LiteralPath (Join-Path $payload 'Assets\ListingIcon300.png') -Destination (Join-Path $output 'ListingIcon300.png')
Write-Output "Store studio checks: 22 passed. MakeAppx validation passed. Package: $package"
Write-Output "Package is unsigned. Local packaged install/activation and WACK certification remain required."
if($Preflight) { Write-Output 'PREFLIGHT ONLY: replace the identity using Partner Center before submission. This package cannot be submitted under the placeholder identity.' }
Write-Output "Staging folder: $work"
