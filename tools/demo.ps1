param([string]$Executable = '')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not $Executable) {
    dotnet build "$projectRoot/src/DesktopPet.App" -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
    $Executable = Join-Path $projectRoot 'src/DesktopPet.App/bin/Release/net10.0-windows/DesktopPet.exe'
}
$outputRoot = Join-Path $projectRoot ('artifacts/demo-' + [Guid]::NewGuid().ToString('N'))
$process = Start-Process -FilePath $Executable -ArgumentList @('--export-demo','--data-dir',('"' + $outputRoot + '"')) -PassThru -WindowStyle Hidden -Wait
if ($process.ExitCode -ne 0) { Get-Content -LiteralPath (Join-Path $outputRoot 'demo-error.txt'); throw 'Demo export failed' }
$outputRoot | Set-Content -LiteralPath (Join-Path $projectRoot 'artifacts/current-deepseek-demo.txt')
$runtimeDemo = Join-Path (Split-Path -Parent ([IO.Path]::GetFullPath($Executable))) 'Demo'
New-Item -ItemType Directory -Path $runtimeDemo -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $outputRoot 'DeepSeek-demo.html'),(Join-Path $outputRoot 'action-coverage.json'),(Join-Path $outputRoot '動作清單.md') -Destination $runtimeDemo -Force
Copy-Item -LiteralPath (Join-Path $outputRoot 'frames') -Destination $runtimeDemo -Recurse -Force
& (Join-Path $PSScriptRoot 'trim-runtime-content.ps1') -RuntimeRoot (Split-Path -Parent ([IO.Path]::GetFullPath($Executable))) -DemoSource $outputRoot
& (Join-Path $PSScriptRoot 'install-taskbar-lift-demo.ps1') -DemoDirectory $runtimeDemo
Get-Content -LiteralPath (Join-Path $outputRoot 'demo-export.json')
Write-Output (Join-Path $outputRoot 'DeepSeek-demo.html')
