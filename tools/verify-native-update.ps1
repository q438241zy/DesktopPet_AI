param(
    [string]$Executable = '',
    [string]$OutputRoot = '',
    [string[]]$Checks = @('agenda','companion','walk-interrupt','interactions','idle-hide','assets')
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not $Executable) { $Executable = Join-Path $projectRoot 'src/DesktopPet.App/bin/Release/net10.0-windows/DesktopPet.exe' }
if (-not $OutputRoot) { $OutputRoot = Join-Path $projectRoot ('.artifacts/native-update-' + (Get-Date -Format 'yyyyMMdd-HHmmss')) }
$Executable = [IO.Path]::GetFullPath($Executable)
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null
$results = @()
foreach ($check in $Checks) {
    if ($check -notmatch '^[a-z-]+$') { throw "Invalid check name: $check" }
    $directory = Join-Path $OutputRoot $check
    if (Test-Path -LiteralPath $directory) { throw "Use a fresh verification directory: $directory" }
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    if ($check -eq 'idle-postures') {
        & node (Join-Path $PSScriptRoot 'export-idle-contract.cjs') (Join-Path $directory 'approved-demo.json')
        if ($LASTEXITCODE -ne 0) { throw 'Approved artwork contract export failed.' }
    }
    Write-Output "START $check"
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $process = Start-Process -FilePath $Executable -ArgumentList @('--ui-test',"--verify-$check",'--data-dir',('"'+$directory+'"')) -WindowStyle Hidden -PassThru
    $process.WaitForExit()
    $result = [pscustomobject]@{check=$check;exitCode=$process.ExitCode;seconds=[math]::Round($watch.Elapsed.TotalSeconds,2);path=$directory}
    $results += $result
    $results | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $OutputRoot 'results.json') -Encoding utf8
    if ($process.ExitCode -ne 0) { throw "$check failed. See $directory" }
    Write-Output "PASS $check ($($result.seconds)s)"
}
