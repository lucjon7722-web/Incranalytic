param([Parameter(Mandatory=$true)][string]$Compiler, [switch]$IncludeTestInstaller)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$workspace = Split-Path -Parent $repo
New-Item -ItemType Directory -Force -Path (Join-Path $workspace 'outputs') | Out-Null
$stage = Join-Path $workspace ('work\studio-publish-' + [Guid]::NewGuid().ToString('N'))
$managerStage = Join-Path $workspace ('work\manager-publish-' + [Guid]::NewGuid().ToString('N'))
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
dotnet publish (Join-Path $repo 'src\Incranalytic.Installer') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $managerStage
if ($LASTEXITCODE -ne 0) { throw 'Version Manager publish failed' }
$managerFile = Join-Path $managerStage 'Incranalytic-Installer.exe'
Copy-Item -LiteralPath $managerFile -Destination (Join-Path $workspace 'outputs\Incranalytic-Installer.exe')
dotnet publish (Join-Path $repo 'src\Incranalytic.App') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o $stage
if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
$runtime = @()
$entries = @()
foreach ($file in Get-ChildItem -LiteralPath $stage -Recurse -File) {
    if ($file.Extension -eq '.pdb') { continue }
    $relative = [IO.Path]::GetRelativePath($stage, $file.FullName)
    $dest = Split-Path -Parent $relative
    $flags = 'ignoreversion'
    if ($file.Name -notlike 'Incranalytic*') { $flags += ' uninsneveruninstall'; $runtime += $relative }
    $entries += 'Source: "' + $file.FullName + '"; DestDir: "{app}\' + $dest + '"; Flags: ' + $flags
}
$manifest = Join-Path $stage 'runtime-files.txt'
$runtime | Set-Content -LiteralPath $manifest -Encoding utf8
$entries += 'Source: "' + $manifest + '"; DestDir: "{app}"; Flags: ignoreversion uninsneveruninstall'
$entries | Set-Content -LiteralPath (Join-Path $repo 'installer\payload.iss') -Encoding utf8
& $Compiler ('/DManagerFile=' + $managerFile) (Join-Path $repo 'installer\Incranalytic.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed' }
if ($IncludeTestInstaller) {
    & $Compiler /DTestBuild=1 ('/DManagerFile=' + $managerFile) (Join-Path $repo 'installer\Incranalytic.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Isolated test installer compilation failed' }
}
Write-Output "Published $($runtime.Count) private runtime files. Stage: $stage"
