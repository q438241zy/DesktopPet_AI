param([string]$Runtime = 'win-x64', [string]$Version = '1.2.0-preview.1')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$releaseRoot = (Resolve-Path -LiteralPath "$projectRoot/Release/$Runtime").Path
$archivePath = Join-Path $projectRoot "Release/DesktopPet-v$Version-$Runtime.zip"
$stream = [IO.File]::Open($archivePath, [IO.FileMode]::Create)
$archive = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in Get-ChildItem -LiteralPath $releaseRoot -Recurse -File) {
        if ($file.Extension -eq '.pdb') { continue }
        $relative = [IO.Path]::GetRelativePath($releaseRoot, $file.FullName).Replace('\','/')
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, $relative, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally { $archive.Dispose(); $stream.Dispose() }
Get-FileHash -LiteralPath $archivePath -Algorithm SHA256
