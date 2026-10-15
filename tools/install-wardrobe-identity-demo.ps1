param([string]$DemoDirectory = '')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$demoRoot = if ($DemoDirectory) { [IO.Path]::GetFullPath($DemoDirectory) } else { Join-Path $projectRoot 'Release/win-x64/Demo' }
$source = Join-Path $projectRoot 'docs/demo/wardrobe-identity'
$target = Join-Path $demoRoot 'WardrobeIdentity'
if (-not (Test-Path -LiteralPath (Join-Path $source 'roster.js')) -or
    -not (Test-Path -LiteralPath (Join-Path $source 'art/roster/provenance.json'))) {
    python (Join-Path $PSScriptRoot 'build-identity-roster.py')
    if ($LASTEXITCODE -ne 0) { throw 'Wardrobe comparison roster export failed.' }
}
foreach ($file in Get-ChildItem -LiteralPath $source -File -Recurse) {
    $relative = [IO.Path]::GetRelativePath($source, $file.FullName)
    $destination = Join-Path $target $relative
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $destination -Force
    if ((Get-FileHash -LiteralPath $file.FullName).Hash -ne (Get-FileHash -LiteralPath $destination).Hash) { throw "Wardrobe Demo mismatch: $relative" }
}
Write-Output (Join-Path $target 'index.html')
