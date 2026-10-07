# 正式安装包（解压即用 ZIP）：仅在用户要求制作时执行，不随普通源码或 Demo 更新自动运行。
param([string]$Runtime = 'win-x64', [string]$Version = '', [string]$SourceDirectory = '', [switch]$RuntimeOnly)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$releaseRoot = (Resolve-Path -LiteralPath $(if ($SourceDirectory) { $SourceDirectory } else { "$projectRoot/Release/$Runtime" })).Path
$exe = Join-Path $releaseRoot 'DesktopPet.exe'
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw 'Expected a published DesktopPet.exe.' }
$builtVersion = ([Diagnostics.FileVersionInfo]::GetVersionInfo($exe).ProductVersion -split '\+')[0]
if (-not $Version) { $Version = $builtVersion }
if ($Version -ne $builtVersion) { throw "Package version $Version differs from executable $builtVersion." }
if ($Version -notmatch '^[0-9A-Za-z.-]+$' -or $Runtime -notmatch '^[0-9A-Za-z-]+$') { throw 'Invalid package name.' }
$folderName = "DesktopPet-v$Version-$Runtime"
$suffix = if ($RuntimeOnly) { '-portable' } else { '' }
New-Item -ItemType Directory -Path (Join-Path $projectRoot 'Release') -Force | Out-Null
$archivePath = Join-Path $projectRoot "Release/$folderName$suffix.zip"
$entries = [Collections.Generic.List[object]]::new()
if ($RuntimeOnly) {
    # Explicit runtime inputs keep local accounts, logs, previews and developer files out of distribution packages.
    foreach ($name in @('DesktopPet.exe','Assets','LICENSE','THIRD_PARTY_NOTICES.md','docs/DeepSeek-LICENSE.txt','docs/licenses')) {
        $source = Join-Path $releaseRoot $name
        if (-not (Test-Path -LiteralPath $source)) { throw "Missing package input: $source" }
        $files = if (Test-Path -LiteralPath $source -PathType Container) { Get-ChildItem -LiteralPath $source -Recurse -File } else { Get-Item -LiteralPath $source }
        foreach ($file in $files) {
            $relative = [IO.Path]::GetRelativePath($releaseRoot, $file.FullName).Replace('\','/')
            $entries.Add(@{ Source=$file.FullName; Name="$folderName/$relative" })
        }
    }
    $guide = Join-Path $releaseRoot 'docs/运行说明.txt'
    if (-not (Test-Path -LiteralPath $guide -PathType Leaf)) { throw 'Missing portable user guide.' }
    $entries.Add(@{ Source=$guide; Name="$folderName/运行说明.txt" })
} else {
    foreach ($file in Get-ChildItem -LiteralPath $releaseRoot -Recurse -File) {
        if ($file.Extension -eq '.pdb') { continue }
        $relative = [IO.Path]::GetRelativePath($releaseRoot, $file.FullName).Replace('\','/')
        $entries.Add(@{ Source=$file.FullName; Name=$relative })
    }
}
$stream = [IO.File]::Open($archivePath, [IO.FileMode]::CreateNew)
$archive = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($entry in $entries) {
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $entry.Source, $entry.Name, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally { $archive.Dispose(); $stream.Dispose() }
$digest = Get-FileHash -LiteralPath $archivePath -Algorithm SHA256
"$($digest.Hash)  $([IO.Path]::GetFileName($archivePath))" | Set-Content -LiteralPath "$archivePath.sha256" -Encoding utf8
[pscustomobject]@{ Path=$archivePath; Files=$entries.Count; Bytes=(Get-Item -LiteralPath $archivePath).Length; SHA256=$digest.Hash }
