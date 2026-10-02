param([string]$Runtime = 'win-x64', [string]$OutputDirectory = '')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$releaseRoot = if ($OutputDirectory) { [IO.Path]::GetFullPath($OutputDirectory) } else { Join-Path $projectRoot "Release/$Runtime" }
dotnet publish "$projectRoot/src/DesktopPet.App/DesktopPet.App.csproj" -c Release -r $Runtime --self-contained true -o $releaseRoot -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
Copy-Item -LiteralPath "$projectRoot/LICENSE","$projectRoot/README.md","$projectRoot/THIRD_PARTY_NOTICES.md" -Destination $releaseRoot
$docsTarget = Join-Path $releaseRoot 'docs'
New-Item -ItemType Directory -Path $docsTarget -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $projectRoot 'docs') | Where-Object Name -ne 'demo' | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $docsTarget -Recurse -Force }
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
$danceDocs = Join-Path $releaseRoot 'artwork/dance-repair'
New-Item -ItemType Directory -Path $danceDocs -Force | Out-Null
Copy-Item -LiteralPath "$projectRoot/artwork/dance-repair/prompts.md","$projectRoot/artwork/dance-repair/manifest.json","$projectRoot/artwork/dance-repair/calibration.json" -Destination $danceDocs
Copy-Item -LiteralPath "$projectRoot/artwork/dance-repair/prompts","$projectRoot/artwork/dance-repair/results" -Destination $danceDocs -Recurse -Force
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
}
if (Test-Path -LiteralPath (Join-Path $projectRoot 'docs/demo/motion-study/assets.js')) {
    & (Join-Path $PSScriptRoot 'install-motion-study.ps1') -DemoDirectory (Join-Path $releaseRoot 'Demo')
}
& (Join-Path $PSScriptRoot 'install-taskbar-lift-demo.ps1') -DemoDirectory (Join-Path $releaseRoot 'Demo')
& (Join-Path $PSScriptRoot 'install-cloud-club-demo.ps1') -DemoDirectory (Join-Path $releaseRoot 'Demo')
$clubDocs = Join-Path $releaseRoot 'artwork/cloud-club'
New-Item -ItemType Directory -Path $clubDocs -Force | Out-Null
Copy-Item -LiteralPath "$projectRoot/artwork/cloud-club/README.md","$projectRoot/artwork/cloud-club/generation-results.json","$projectRoot/artwork/cloud-club/short-sleeve-prompts.json","$projectRoot/artwork/cloud-club/coverage.json" -Destination $clubDocs -Force
& (Join-Path $PSScriptRoot 'trim-runtime-content.ps1') -RuntimeRoot $releaseRoot -DemoSource $(if (Test-Path -LiteralPath $demoPointer) { $demoSource } else { '' })
