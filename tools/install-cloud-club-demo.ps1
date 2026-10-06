param([switch]$DesktopShortcut, [string]$DemoDirectory = '')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$source = Join-Path $projectRoot 'docs/demo/cloud-club'
$demoRoot = if ($DemoDirectory) { [IO.Path]::GetFullPath($DemoDirectory) } else { Join-Path $projectRoot 'Release/win-x64/Demo' }
$target = Join-Path $demoRoot 'CloudClub'
New-Item -ItemType Directory -Path $target -Force | Out-Null
foreach ($name in @('index.html','style.css','model.js','icons.js','app.js','pose-renderer.js','pose-motion.js','sports-gallery.js')) {
    Copy-Item -LiteralPath (Join-Path $source $name) -Destination (Join-Path $target $name) -Force
    if ((Get-FileHash -LiteralPath (Join-Path $source $name)).Hash -ne (Get-FileHash -LiteralPath (Join-Path $target $name)).Hash) { throw "Demo copy mismatch: $name" }
}
python (Join-Path $PSScriptRoot 'build-sports-roster.py') (Join-Path $target 'sports-roster.js')
if ($LASTEXITCODE -ne 0) { throw 'Sports wardrobe roster export failed.' }
node (Join-Path $PSScriptRoot 'build-cloud-club-data.cjs') (Join-Path $demoRoot 'DeepSeek-demo.html') (Join-Path $target 'assets.js')
if ($LASTEXITCODE -ne 0) { throw 'Cloud Club data export failed.' }
$poses = Join-Path $target 'poses.js'
python (Join-Path $PSScriptRoot 'build-cloud-club-poses.py') $poses
if ($LASTEXITCODE -ne 0) { throw 'Cloud Club pose export failed.' }
New-Item -ItemType Directory -Path (Join-Path $target 'art') -Force | Out-Null
foreach ($name in @('school-shorts-chibi.png','school-shorts-realistic.png','school-chibi.png','school-realistic.png')) {
    Copy-Item -LiteralPath (Join-Path $source "art/$name") -Destination (Join-Path $target "art/$name") -Force
}
& (Join-Path $PSScriptRoot 'install-interaction-five-demo.ps1') -DemoDirectory $demoRoot
if ($DesktopShortcut) { & (Join-Path $PSScriptRoot 'desktop-demo.ps1') -DemoPath (Join-Path $target 'index.html') }
Write-Output (Join-Path $target 'index.html')
