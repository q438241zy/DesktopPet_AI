param([string]$Runtime = 'win-x64')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
dotnet publish "$projectRoot/src/DesktopPet.App/DesktopPet.App.csproj" -c Release -r $Runtime --self-contained true -o "$projectRoot/Release/$Runtime" -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
Copy-Item -LiteralPath "$projectRoot/LICENSE","$projectRoot/README.md","$projectRoot/THIRD_PARTY_NOTICES.md" -Destination "$projectRoot/Release/$Runtime"
Copy-Item -LiteralPath "$projectRoot/docs" -Destination "$projectRoot/Release/$Runtime" -Recurse -Force
Write-Output "Ready: $projectRoot/Release/$Runtime/DesktopPet.exe"
