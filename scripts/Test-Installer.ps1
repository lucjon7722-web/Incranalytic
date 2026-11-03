$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$workspace = Split-Path -Parent $repo
$registry = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{9B32C142-B7D2-4635-A0D8-19D029D8D8C7}_is1'
if (Test-Path $registry) { throw 'A copy is already registered. Tests will not overwrite an existing installation.' }
$target = Join-Path $workspace ('work\installer-test-' + [Guid]::NewGuid().ToString('N'))
$setup = Join-Path $workspace 'work\test-installer\Incranalytic-0.3.0-TestSetup.exe'
$report = Join-Path $workspace 'outputs\installer-verification-0.3.txt'
Set-Content -LiteralPath $report -Value 'Isolated test AppId: existing user installation is preserved.'
function Check([bool]$ok, [string]$message) { if (!$ok) { throw $message }; "PASS: $message" | Tee-Object -FilePath $report -Append }
function Install {
    $arguments = '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /NOICONS /DIR="' + $target + '"'
    $p = Start-Process -FilePath $setup -ArgumentList $arguments -Wait -PassThru -WindowStyle Hidden
    Check ($p.ExitCode -eq 0) 'Installer completed without error'
}
function Uninstall([bool]$keep) {
    $arguments = '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART'
    if ($keep) { $arguments += ' /KEEPRUNTIME=1' }
    $p = Start-Process -FilePath (Join-Path $target 'unins000.exe') -ArgumentList $arguments -Wait -PassThru -WindowStyle Hidden
    Check ($p.ExitCode -eq 0) 'Uninstaller completed without error'
    Start-Sleep -Seconds 2
}
Install
Check ((Get-ItemProperty $registry).DisplayVersion -eq '0.3.0') 'Windows Installed Apps registration is correct'
Check (Test-Path (Join-Path $target 'coreclr.dll')) 'Private runtime is installed alongside the application'
Set-Content -LiteralPath (Join-Path $target 'keep-project.incr') -Value 'user-project-sentinel'
$uiReport = Join-Path $workspace 'outputs\installed-workspace-verification.txt'
$p = Start-Process -FilePath (Join-Path $target 'Incranalytic.exe') -ArgumentList ('--verify-studio "' + $uiReport + '"') -PassThru -Wait
Check ($p.ExitCode -eq 0) 'Installed application completes its studio integration checks'
Check ((Get-Content -LiteralPath $uiReport | Where-Object { $_ -like 'PASS:*' }).Count -eq 16) 'Values, Workspace gestures, IFCLOUD, simulation controls, views, undo and embedded Unity export work after installation'
Uninstall $true
Check (!(Test-Path (Join-Path $target 'Incranalytic.exe'))) 'Uninstall removes the application when dependencies are retained'
Check (Test-Path (Join-Path $target 'coreclr.dll')) 'Keep-dependencies choice retains the private runtime'
Check (!(Test-Path $registry)) 'Uninstall removes Windows Installed Apps registration'
Install
Uninstall $false
$remaining = @(Get-ChildItem -LiteralPath $target -Recurse -File)
Check ($remaining.Count -eq 1 -and $remaining[0].Name -eq 'keep-project.incr') 'Complete cleanup removes app and runtime while preserving the user project'
Check ((Get-Content -LiteralPath (Join-Path $target 'keep-project.incr')) -eq 'user-project-sentinel') 'Uninstall leaves project contents unchanged'
"Tested on current Windows host; clean VM and real Unity integration are not covered." | Add-Content -LiteralPath $report
