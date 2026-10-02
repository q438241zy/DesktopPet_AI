param([string]$DemoPath = '')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not $DemoPath) {
    $studyEntry = Join-Path $projectRoot 'Release/win-x64/Demo/MotionStudy/index.html'
    $DemoPath = if (Test-Path -LiteralPath $studyEntry) { $studyEntry } else { Join-Path $projectRoot 'Release/win-x64/Demo/DeepSeek-demo.html' }
}
$target = (Resolve-Path -LiteralPath $DemoPath).Path
$frames = Join-Path (Split-Path -Parent $target) 'frames'
$studyBundle = Join-Path (Split-Path -Parent $target) 'assets.js'
if (-not (Test-Path -LiteralPath $frames -PathType Container) -and -not (Test-Path -LiteralPath $studyBundle -PathType Leaf)) { throw 'The Demo frames or motion-study bundle is missing.' }
$desktop = [Environment]::GetFolderPath('DesktopDirectory')
$shortcutPath = Join-Path $desktop 'DeepSeek-動作Demo.lnk'
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $target
$shortcut.WorkingDirectory = Split-Path -Parent $target
$shortcut.Description = 'DeepSeek 動作 Demo · 新動作樣例與全風格常態 Demo'
$icon = Join-Path $projectRoot 'Release/win-x64/Assets/cloud.ico'
if (Test-Path -LiteralPath $icon) { $shortcut.IconLocation = $icon }
$shortcut.Save()
Write-Output "Desktop Demo: $shortcutPath -> $target"
