param([switch]$DesktopShortcut)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$source = Join-Path $projectRoot 'docs/demo/cloud-club'
$demoRoot = Join-Path $projectRoot 'Release/win-x64/Demo'
$target = Join-Path $demoRoot 'CloudClub'
New-Item -ItemType Directory -Path $target -Force | Out-Null
foreach ($name in @('index.html','style.css','model.js','icons.js','app.js')) {
    Copy-Item -LiteralPath (Join-Path $source $name) -Destination (Join-Path $target $name) -Force
    if ((Get-FileHash -LiteralPath (Join-Path $source $name)).Hash -ne (Get-FileHash -LiteralPath (Join-Path $target $name)).Hash) { throw "Demo copy mismatch: $name" }
}
node (Join-Path $PSScriptRoot 'build-cloud-club-data.cjs') (Join-Path $demoRoot 'DeepSeek-demo.html') (Join-Path $target 'assets.js')
if ($LASTEXITCODE -ne 0) { throw 'Cloud Club data export failed.' }
if ($DesktopShortcut) { & (Join-Path $PSScriptRoot 'desktop-demo.ps1') -DemoPath (Join-Path $target 'index.html') }
Write-Output (Join-Path $target 'index.html')
