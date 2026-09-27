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
$process = Start-Process -FilePath $exe -ArgumentList @('--verify-ui','--data-dir',('"' + $uiRoot + '"')) -PassThru -Wait -WindowStyle Hidden
Get-Content -LiteralPath "$uiRoot/ui-check.txt"
if ($process.ExitCode -ne 0) { throw 'WPF integration checks failed' }
Write-Output "UI verification artifacts: $uiRoot"
