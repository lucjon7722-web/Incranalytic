param([Parameter(Mandatory=$true)][string]$MakeAppx,[Parameter(Mandatory=$true)][string]$Package)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$workspace = Split-Path -Parent $repo
$tool = (Resolve-Path -LiteralPath $MakeAppx).Path
$signature = Get-AuthenticodeSignature -LiteralPath $tool
if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation') { throw 'Use an officially signed Microsoft MakeAppx tool.' }
$unpacked = Join-Path $workspace ('work\store-unpack-' + [Guid]::NewGuid().ToString('N'))
& $tool unpack /p (Resolve-Path -LiteralPath $Package).Path /d $unpacked /o
if ($LASTEXITCODE -ne 0) { throw 'Microsoft package unpack/integrity checks failed.' }
$checks = [Collections.Generic.List[string]]::new()
function Check([bool]$condition,[string]$message) { if(!$condition){throw $message}; $checks.Add('PASS: '+$message) }
[xml]$manifest = Get-Content -LiteralPath (Join-Path $unpacked 'AppxManifest.xml') -Raw
Check ($manifest.Package.Identity.ProcessorArchitecture -eq 'x64') 'Package declares the architecture of its self-contained runtime'
Check ($manifest.Package.Applications.Application.Executable -eq 'app\Incranalytic.exe') 'Manifest targets the packaged studio executable'
Check ($manifest.Package.Applications.Application.EntryPoint -eq 'Windows.FullTrustApplication') 'WinForms studio uses the desktop full-trust entry point'
$capabilities = @($manifest.Package.Capabilities.ChildNodes | Where-Object { $_.NodeType -eq 'Element' })
Check ($capabilities.Count -eq 1 -and $capabilities[0].Name -eq 'runFullTrust') 'Only the required desktop full-trust capability is requested'
$app = Join-Path $unpacked 'app'
Check ((Test-Path -LiteralPath (Join-Path $app 'Incranalytic.exe')) -and (Test-Path -LiteralPath (Join-Path $app 'coreclr.dll')) -and (Test-Path -LiteralPath (Join-Path $app 'hostfxr.dll'))) 'Unpacked studio includes its executable and private .NET runtime'
Check (@(Get-ChildItem -LiteralPath $unpacked -Recurse -File | Where-Object { $_.Name -match 'Incranalytic-Installer|unins[0-9]+|Setup\.exe$|\.pdb$' }).Count -eq 0) 'No GitHub updater, EXE setup, uninstaller or debug symbols are bundled'
foreach($name in @('Terms.txt','privacy.md','Studio-Source-Terms.txt','EXPORT-LICENSE.txt','AI-DEVELOPMENT.md','THIRD-PARTY-NOTICES.md','LICENSE.txt','ThirdPartyNotices.txt','WindowsDesktop-LICENSE.txt','examples\conditional-counter.incr')) {
    Check (Test-Path -LiteralPath (Join-Path $app $name)) ('Package includes '+$name)
}
Add-Type -AssemblyName System.Drawing
foreach($asset in @(@('Square44x44Logo.png',44),@('Square150x150Logo.png',150),@('StoreLogo.png',50))) {
    $image = [Drawing.Image]::FromFile((Join-Path $unpacked ('Assets\'+$asset[0])))
    try { Check ($image.Width -eq $asset[1] -and $image.Height -eq $asset[1]) ('Manifest asset has correct dimensions: '+$asset[0]) } finally { $image.Dispose() }
}
$uiReport = Join-Path $workspace ('work\unpacked-store-ui-' + [Guid]::NewGuid().ToString('N') + '.txt')
$process = Start-Process -FilePath (Join-Path $app 'Incranalytic.exe') -ArgumentList ('--verify-studio "' + $uiReport + '"') -PassThru -WindowStyle Hidden
if(!$process.WaitForExit(60000)){ $process.Kill(); throw 'Unpacked studio checks timed out.' }
Check ($process.ExitCode -eq 0 -and @(Get-Content -LiteralPath $uiReport | Where-Object { $_ -like 'PASS:*' }).Count -eq 22) 'Studio extracted from MSIX passes all 22 Store UI/integration checks'
$checks.Add('LIMIT: This tests package contents and the extracted executable. It does not install or activate the app with MSIX package identity.')
$checks.Add('LIMIT: WACK, clean-machine installation, Store identity validation, and Microsoft certification are pending.')
$report = Join-Path $workspace 'outputs\microsoft-store\store-package-verification.txt'
$checks | Set-Content -LiteralPath $report
$checks | Write-Output
Write-Output ('Report: '+$report)
