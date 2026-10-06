param([string]$Runtime = 'win-x64', [string]$OutputDirectory = '', [switch]$RefreshDemo)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$releaseRoot = if ($OutputDirectory) { [IO.Path]::GetFullPath($OutputDirectory) } else { Join-Path $projectRoot "Release/$Runtime" }
dotnet publish "$projectRoot/src/DesktopPet.App/DesktopPet.App.csproj" -c Release -r $Runtime --self-contained true -o $releaseRoot -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
if ($RefreshDemo) {
    & (Join-Path $PSScriptRoot 'demo.ps1') -Executable (Join-Path $releaseRoot 'DesktopPet.exe')
}
Copy-Item -LiteralPath "$projectRoot/LICENSE","$projectRoot/README.md","$projectRoot/THIRD_PARTY_NOTICES.md" -Destination $releaseRoot
$docsTarget = Join-Path $releaseRoot 'docs'
New-Item -ItemType Directory -Path $docsTarget -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $projectRoot 'docs') | Where-Object Name -ne 'demo' | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $docsTarget -Recurse -Force }
# Keep the notices for the exact self-contained runtime restored by the SDK.
$restore = Get-Content -LiteralPath "$projectRoot/src/DesktopPet.App/obj/project.assets.json" -Raw | ConvertFrom-Json
$runtimePacks = $restore.project.frameworks.'net10.0-windows'.downloadDependencies | Where-Object name -match '^Microsoft\.(NETCore|WindowsDesktop)\.App\.Runtime\.'
foreach ($pack in $runtimePacks) {
    $packVersion = ($pack.version.Trim('[]') -split ',')[0].Trim()
    $packPath = $restore.packageFolders.PSObject.Properties.Name | ForEach-Object { Join-Path $_ ($pack.name.ToLowerInvariant() + '/' + $packVersion) } | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if (-not $packPath) { throw "Runtime notices unavailable for $($pack.name) $packVersion." }
    foreach ($notice in Get-ChildItem -LiteralPath $packPath -File | Where-Object Name -in @('LICENSE','LICENSE.TXT','THIRD-PARTY-NOTICES.TXT')) {
        Copy-Item -LiteralPath $notice.FullName -Destination (Join-Path $docsTarget "licenses/$($pack.name)-$($notice.Name)") -Force
    }
}
$styleDocs = Join-Path $releaseRoot 'artwork/style-demo'
New-Item -ItemType Directory -Path $styleDocs -Force | Out-Null
Copy-Item -LiteralPath "$projectRoot/artwork/style-demo/prompts.md" -Destination $styleDocs
$wardrobeDocs = Join-Path $releaseRoot 'artwork/wardrobe-expansion'
New-Item -ItemType Directory -Path $wardrobeDocs -Force | Out-Null
Copy-Item -LiteralPath "$projectRoot/artwork/wardrobe-expansion/prompts.md" -Destination $wardrobeDocs
$motionDocs = Join-Path $releaseRoot 'artwork/motion-continuity'
New-Item -ItemType Directory -Path $motionDocs -Force | Out-Null
Copy-Item -LiteralPath "$projectRoot/artwork/motion-continuity/prompts.md" -Destination $motionDocs
$contactDocs = Join-Path $releaseRoot 'artwork/contact-motion'
New-Item -ItemType Directory -Path $contactDocs -Force | Out-Null
Copy-Item -LiteralPath "$projectRoot/artwork/contact-motion/prompts.md","$projectRoot/artwork/contact-motion/manifest.json","$projectRoot/artwork/contact-motion/calibration.json" -Destination $contactDocs
Copy-Item -LiteralPath "$projectRoot/artwork/contact-motion/results" -Destination $contactDocs -Recurse -Force
$interactionDocs = Join-Path $releaseRoot 'artwork/interaction-poses'
New-Item -ItemType Directory -Path $interactionDocs -Force | Out-Null
Copy-Item -LiteralPath "$projectRoot/artwork/interaction-poses/prompts.md","$projectRoot/artwork/interaction-poses/manifest.json" -Destination $interactionDocs
Copy-Item -LiteralPath "$projectRoot/artwork/interaction-poses/results" -Destination $interactionDocs -Recurse -Force
$polishDocs = Join-Path $releaseRoot 'artwork/motion-polish'
New-Item -ItemType Directory -Path $polishDocs -Force | Out-Null
Copy-Item -LiteralPath "$projectRoot/artwork/motion-polish/prompts.md","$projectRoot/artwork/motion-polish/manifest.json" -Destination $polishDocs
Copy-Item -LiteralPath "$projectRoot/artwork/motion-polish/prompts","$projectRoot/artwork/motion-polish/results" -Destination $polishDocs -Recurse -Force
Write-Output "Ready: $releaseRoot/DesktopPet.exe"
$chibiDocs = Join-Path $releaseRoot 'artwork/chibi-continuity'
New-Item -ItemType Directory -Path $chibiDocs -Force | Out-Null
Copy-Item -LiteralPath "$projectRoot/artwork/chibi-continuity/prompts.md","$projectRoot/artwork/chibi-continuity/manifest.json" -Destination $chibiDocs
Copy-Item -LiteralPath "$projectRoot/artwork/chibi-continuity/prompts","$projectRoot/artwork/chibi-continuity/results" -Destination $chibiDocs -Recurse -Force
$demoPointer = Join-Path $projectRoot 'artifacts/current-deepseek-demo.txt'
$scaleDocs = Join-Path $releaseRoot 'artwork/scale-consistency'
New-Item -ItemType Directory -Path $scaleDocs -Force | Out-Null
Copy-Item -LiteralPath "$projectRoot/artwork/scale-consistency/README.md","$projectRoot/artwork/scale-consistency/calibration.json" -Destination $scaleDocs -Force
$snackDocs = Join-Path $releaseRoot 'artwork/snack-correction'
New-Item -ItemType Directory -Path $snackDocs -Force | Out-Null
Copy-Item -LiteralPath "$projectRoot/artwork/snack-correction/README.md","$projectRoot/artwork/snack-correction/manifest.json" -Destination $snackDocs -Force
Copy-Item -LiteralPath "$projectRoot/artwork/snack-correction/prompts","$projectRoot/artwork/snack-correction/results" -Destination $snackDocs -Recurse -Force
if (Test-Path -LiteralPath $demoPointer) {
    $demoSource = (Get-Content -LiteralPath $demoPointer -Raw).Trim()
    $demoTarget = Join-Path $releaseRoot 'Demo'
    New-Item -ItemType Directory -Path $demoTarget -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $demoSource 'DeepSeek-demo.html'),(Join-Path $demoSource 'action-coverage.json'),(Join-Path $demoSource '動作清單.md') -Destination $demoTarget -Force
    Copy-Item -LiteralPath (Join-Path $demoSource 'frames') -Destination $demoTarget -Recurse -Force
    & (Join-Path $PSScriptRoot 'install-taskbar-lift-demo.ps1') -DemoDirectory $demoTarget
    & (Join-Path $PSScriptRoot 'install-cloud-club-demo.ps1') -DemoDirectory $demoTarget
}
$clubDocs = Join-Path $releaseRoot 'artwork/cloud-club'
New-Item -ItemType Directory -Path $clubDocs -Force | Out-Null
Copy-Item -LiteralPath "$projectRoot/artwork/cloud-club/README.md","$projectRoot/artwork/cloud-club/generation-results.json","$projectRoot/artwork/cloud-club/short-sleeve-prompts.json","$projectRoot/artwork/cloud-club/coverage.json" -Destination $clubDocs -Force
$sportsDocs = Join-Path $releaseRoot 'artwork/sports-shorts'
New-Item -ItemType Directory -Path $sportsDocs -Force | Out-Null
Copy-Item -LiteralPath "$projectRoot/artwork/sports-shorts/README.md","$projectRoot/artwork/sports-shorts/coverage.json" -Destination $sportsDocs -Force
foreach ($name in @('REALISTIC-README.md','realistic-final-validation.json','realistic-manual-measurements.json')) {
    if (Test-Path -LiteralPath "$projectRoot/artwork/sports-shorts/$name") { Copy-Item -LiteralPath "$projectRoot/artwork/sports-shorts/$name" -Destination $sportsDocs -Force }
}
Copy-Item -LiteralPath "$projectRoot/artwork/sports-shorts/results" -Destination $sportsDocs -Recurse -Force
$careDocs = Join-Path $releaseRoot 'artwork/care-contact'
if (Test-Path -LiteralPath "$projectRoot/artwork/care-contact/coverage.json") {
    New-Item -ItemType Directory -Path $careDocs -Force | Out-Null
    Copy-Item -LiteralPath "$projectRoot/artwork/care-contact/coverage.json" -Destination $careDocs -Force
    if (Test-Path -LiteralPath "$projectRoot/artwork/care-contact/README.md") { Copy-Item -LiteralPath "$projectRoot/artwork/care-contact/README.md" -Destination $careDocs -Force }
    Copy-Item -LiteralPath "$projectRoot/artwork/care-contact/results" -Destination $careDocs -Recurse -Force
}
& (Join-Path $PSScriptRoot 'trim-runtime-content.ps1') -RuntimeRoot $releaseRoot -DemoSource $(if (Test-Path -LiteralPath $demoPointer) { $demoSource } else { '' })
$fiveDocs = Join-Path $releaseRoot 'artwork/interaction-five'
New-Item -ItemType Directory -Path $fiveDocs -Force | Out-Null
Copy-Item -LiteralPath "$projectRoot/artwork/interaction-five/README.md","$projectRoot/artwork/interaction-five/selected-expansion.json","$projectRoot/artwork/interaction-five/installation.json" -Destination $fiveDocs -Force
