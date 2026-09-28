$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
dotnet run --project "$projectRoot/tests/DesktopPet.Tests" -c Release
if ($LASTEXITCODE -ne 0) { throw 'Behavior tests failed' }
dotnet build "$projectRoot/src/DesktopPet.App" -c Release
if ($LASTEXITCODE -ne 0) { throw 'Desktop build failed' }
$verifyRoot = Join-Path $projectRoot 'artifacts/asset-verification'
$exe = Join-Path $projectRoot 'src/DesktopPet.App/bin/Release/net10.0-windows/DesktopPet.exe'
$process = Start-Process -FilePath $exe -ArgumentList @('--verify-assets','--data-dir',('"' + $verifyRoot + '"')) -PassThru -Wait -WindowStyle Hidden
Get-Content -LiteralPath "$verifyRoot/asset-check.txt"
if ($process.ExitCode -ne 0) { throw 'Asset validation failed' }
$uiRoot = Join-Path $projectRoot ('artifacts/ui-smoke-' + [Guid]::NewGuid().ToString('N'))
$uiRoot | Set-Content -LiteralPath (Join-Path $projectRoot 'artifacts/current-ui-check.txt')
$process = Start-Process -FilePath $exe -ArgumentList @('--verify-ui','--data-dir',('"' + $uiRoot + '"')) -PassThru -Wait -WindowStyle Hidden
if ($process.ExitCode -ne 0) { Get-Content -LiteralPath "$uiRoot/ui-check.txt"; throw 'WPF integration checks failed' }
Get-Content -LiteralPath "$uiRoot/ui-check.txt" -Tail 1
Get-Content -LiteralPath "$uiRoot/interaction-check.txt" -Tail 1
Get-Content -LiteralPath "$uiRoot/detail-check.txt" -Tail 1
Get-Content -LiteralPath "$uiRoot/placement-check.txt" -Tail 1
Get-Content -LiteralPath "$uiRoot/contact-check.txt" -Tail 1
Get-Content -LiteralPath "$uiRoot/choreography-check.txt" -Tail 1
Get-Content -LiteralPath "$uiRoot/polish-check.txt" -Tail 1
Get-Content -LiteralPath "$uiRoot/chibi-check.txt" -Tail 1
Get-Content -LiteralPath "$uiRoot/shake-check.txt" -Tail 1
Get-Content -LiteralPath "$uiRoot/walk-check.txt" -Tail 1
Get-Content -LiteralPath "$uiRoot/dance-check.txt" -Tail 1
Write-Output "UI verification artifacts: $uiRoot"
