param([string]$DemoDirectory = '')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$demoRoot = if ($DemoDirectory) { [IO.Path]::GetFullPath($DemoDirectory) } else { Join-Path $projectRoot 'Release/win-x64/Demo' }
$fullDemo = Join-Path $demoRoot 'DeepSeek-demo.html'
if (-not (Test-Path -LiteralPath $fullDemo -PathType Leaf)) { throw 'Export the full-style Demo first.' }
$source = Join-Path $projectRoot 'docs/demo/taskbar-lift'
$target = Join-Path $demoRoot 'TaskbarLift'
New-Item -ItemType Directory -Path $target -Force | Out-Null
foreach ($name in @('index.html','style.css','lift-logic.js','app.js')) {
    $from = Join-Path $source $name
    $to = Join-Path $target $name
    Copy-Item -LiteralPath $from -Destination $to -Force
    if ((Get-FileHash -LiteralPath $from).Hash -ne (Get-FileHash -LiteralPath $to).Hash) { throw "Demo copy mismatch: $name" }
}
node (Join-Path $PSScriptRoot 'build-taskbar-lift-data.cjs') $fullDemo (Join-Path $target 'data.js')
if ($LASTEXITCODE -ne 0) { throw 'Taskbar lift Demo data generation failed.' }
Write-Output "Taskbar lift Demo: $target/index.html"
