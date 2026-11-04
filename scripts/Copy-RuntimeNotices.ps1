param([Parameter(Mandatory=$true)][string]$PayloadDirectory)
$ErrorActionPreference = 'Stop'
$config = Get-Content -LiteralPath (Join-Path $PayloadDirectory 'Incranalytic.runtimeconfig.json') -Raw | ConvertFrom-Json
$packages = if($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget\packages' }
foreach($framework in $config.runtimeOptions.includedFrameworks) {
    $packageName = $framework.name.ToLowerInvariant() + '.runtime.win-x64'
    $root = Join-Path (Join-Path $packages $packageName) $framework.version
    $licenseName = if($framework.name -eq 'Microsoft.NETCore.App') { 'LICENSE.TXT' } else { 'LICENSE' }
    $destination = if($framework.name -eq 'Microsoft.NETCore.App') { 'LICENSE.txt' } else { 'WindowsDesktop-LICENSE.txt' }
    Copy-Item -LiteralPath (Join-Path $root $licenseName) -Destination (Join-Path $PayloadDirectory $destination)
    if($framework.name -eq 'Microsoft.NETCore.App') {
        Copy-Item -LiteralPath (Join-Path $root 'THIRD-PARTY-NOTICES.TXT') -Destination (Join-Path $PayloadDirectory 'ThirdPartyNotices.txt')
    }
}
foreach($required in @('LICENSE.txt','ThirdPartyNotices.txt','WindowsDesktop-LICENSE.txt')) {
    if(!(Test-Path -LiteralPath (Join-Path $PayloadDirectory $required))) { throw "Runtime notice missing: $required" }
}
