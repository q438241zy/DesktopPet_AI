param([string]$DemoDirectory = '')
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$demoRoot = if ($DemoDirectory) { [IO.Path]::GetFullPath($DemoDirectory) } else { Join-Path $projectRoot 'Release/win-x64/Demo' }
if (-not $demoRoot.StartsWith($projectRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Demo must stay inside this project.' }
$target = Join-Path $demoRoot 'CompanionV01'
python (Join-Path $PSScriptRoot 'build-companion-demo.py') --target $target
if ($LASTEXITCODE -ne 0) { throw 'Companion Demo export failed.' }
foreach ($name in @('index.html','style.css','icons.js','personas.js','providers.js','model.js','app.js')) {
    $source = Join-Path $projectRoot ('docs/demo/companion-v01/' + $name)
    if ((Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath (Join-Path $target $name)).Hash) { throw "Demo copy mismatch: $name" }
}
Write-Output (Join-Path $target 'index.html')
