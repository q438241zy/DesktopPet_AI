param([string]$DemoPath = '')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not $DemoPath) { $DemoPath = Join-Path $projectRoot 'Release/win-x64/Demo/DeepSeek-demo.html' }
$target = (Resolve-Path -LiteralPath $DemoPath).Path
$frames = Join-Path (Split-Path -Parent $target) 'frames'
if (-not (Test-Path -LiteralPath $frames -PathType Container)) { throw 'The Demo frames folder is missing.' }
$desktop = [Environment]::GetFolderPath('DesktopDirectory')
$shortcutPath = Join-Path $desktop 'DeepSeek-動作Demo.lnk'
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $target
$shortcut.WorkingDirectory = Split-Path -Parent $target
$shortcut.Description = 'DeepSeek 長期動作 Demo · Q 版、3D 版、真人版'
$icon = Join-Path $projectRoot 'Release/win-x64/Assets/cloud.ico'
if (Test-Path -LiteralPath $icon) { $shortcut.IconLocation = $icon }
$shortcut.Save()
Write-Output "Desktop Demo: $shortcutPath -> $target"
