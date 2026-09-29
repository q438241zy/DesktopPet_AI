param([string]$DemoDirectory = '', [switch]$DesktopShortcut)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$source = Join-Path $projectRoot 'docs/demo/motion-study'
if (-not (Test-Path -LiteralPath (Join-Path $source 'assets.js'))) { throw 'Run python tools/prepare-motion-study.py first.' }
if (-not $DemoDirectory) { $DemoDirectory = Join-Path $projectRoot 'Release/win-x64/Demo' }
$target = Join-Path ([IO.Path]::GetFullPath($DemoDirectory)) 'MotionStudy'
New-Item -ItemType Directory -Path $target -Force | Out-Null
Get-ChildItem -LiteralPath $source | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $target -Recurse -Force }
$sourceFiles = Get-ChildItem -LiteralPath $source -File -Recurse
foreach ($file in $sourceFiles) {
    $relative = $file.FullName.Substring($source.Length).TrimStart([char[]]@('\','/'))
    $copied = Join-Path $target $relative
    if ((Get-FileHash -LiteralPath $file.FullName).Hash -ne (Get-FileHash -LiteralPath $copied).Hash) { throw "Demo copy mismatch: $relative" }
}
if ($DesktopShortcut) { & (Join-Path $PSScriptRoot 'desktop-demo.ps1') -DemoPath (Join-Path $target 'index.html') }
Write-Output "Motion study: $target/index.html ($($sourceFiles.Count) verified files)"
