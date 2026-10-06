param([string]$DemoPath = '')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not $DemoPath) {
    $clubEntry = Join-Path $projectRoot 'Release/win-x64/Demo/CloudClub/index.html'
    $DemoPath = if (Test-Path -LiteralPath $clubEntry) { $clubEntry } else { Join-Path $projectRoot 'Release/win-x64/Demo/DeepSeek-demo.html' }
}
$target = (Resolve-Path -LiteralPath $DemoPath).Path
$frames = Join-Path (Split-Path -Parent $target) 'frames'
$previewBundle = Join-Path (Split-Path -Parent $target) 'assets.js'
if (-not (Test-Path -LiteralPath $frames -PathType Container) -and -not (Test-Path -LiteralPath $previewBundle -PathType Leaf)) { throw 'The Demo frames or preview data bundle is missing.' }
$desktop = [Environment]::GetFolderPath('DesktopDirectory')
$shortcutPath = Join-Path $desktop 'DeepSeek-動作Demo.lnk'
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $target
$shortcut.WorkingDirectory = Split-Path -Parent $target
$shortcut.Description = '桌面宠物 Demo · 全角色运动服与 DeepSeek 长期动作预览'
$icon = Join-Path $projectRoot 'Release/win-x64/Assets/cloud.ico'
if (Test-Path -LiteralPath $icon) { $shortcut.IconLocation = $icon }
$shortcut.Save()
Write-Output "Desktop Demo: $shortcutPath -> $target"
