param([string]$Runtime = 'win-x64', [string]$OutputDirectory = '')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$releaseRoot = if ($OutputDirectory) { [IO.Path]::GetFullPath($OutputDirectory) } else { Join-Path $projectRoot "Release/$Runtime" }
dotnet publish "$projectRoot/src/DesktopPet.App/DesktopPet.App.csproj" -c Release -r $Runtime --self-contained true -o $releaseRoot -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
Copy-Item -LiteralPath "$projectRoot/LICENSE","$projectRoot/README.md","$projectRoot/THIRD_PARTY_NOTICES.md" -Destination $releaseRoot
Copy-Item -LiteralPath "$projectRoot/docs" -Destination $releaseRoot -Recurse -Force
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
Write-Output "Ready: $releaseRoot/DesktopPet.exe"
