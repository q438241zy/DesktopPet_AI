param([Parameter(Mandatory)][string]$RuntimeRoot, [string]$DemoSource = '')
$ErrorActionPreference = 'Stop'
$runtime = [IO.Path]::GetFullPath($RuntimeRoot).TrimEnd([char[]]@('\','/'))
if (-not (Test-Path -LiteralPath (Join-Path $runtime 'DesktopPet.exe'))) { throw 'Expected a built DesktopPet runtime.' }
$managedRoot = $runtime + [IO.Path]::DirectorySeparatorChar
function Confirm-ManagedPath([string]$candidate) {
    $absolute = [IO.Path]::GetFullPath($candidate)
    if (-not $absolute.StartsWith($managedRoot, [StringComparison]::OrdinalIgnoreCase)) { throw "Outside runtime: $absolute" }
    # Generated output must never traverse a directory junction into user files.
    $item = Get-Item -LiteralPath $absolute -ErrorAction SilentlyContinue
    while ($item -and $item.FullName -ne $runtime) {
        if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Linked output: $($item.FullName)" }
        $item = if ($item -is [IO.FileInfo]) { $item.Directory } else { $item.Parent }
    }
}
$retired = @('deepseek','gpt','claude','gemini','grok','qwen','zhipu','kimi')
$studio = [IO.Path]::GetFullPath((Join-Path $runtime 'Studio'))
if (Test-Path -LiteralPath $studio) { Confirm-ManagedPath $studio; Remove-Item -LiteralPath $studio -Recurse -Force }
foreach ($family in $retired) {
    $folder = [IO.Path]::GetFullPath((Join-Path $runtime "Assets/Characters/$family-3d"))
    if (Test-Path -LiteralPath $folder) {
        Confirm-ManagedPath $folder
        Remove-Item -LiteralPath $folder -Recurse -Force
    }
}
# Ship only images referenced by the sixteen built-in manifests. Keep original
# and superseded drawings in src; remove generated-output copies only.
$activeIds = @('whale','gpt','claude','gemini','grok','qwen','zhipu','kimi') + @($retired | ForEach-Object { "$_-adult" })
foreach ($id in $activeIds) {
    $pack = [IO.Path]::GetFullPath((Join-Path $runtime "Assets/Characters/$id"))
    Confirm-ManagedPath $pack
    $manifest = Get-Content -LiteralPath (Join-Path $pack 'pet.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $sprites = @($manifest.atlas)
    if ($manifest.interactionFive) { $sprites += @($manifest.interactionFive.atlases.PSObject.Properties | ForEach-Object { $_.Value }) }
    if ($manifest.dizzy) { $sprites += $manifest.dizzy }
    $sprites += @($manifest.motions.PSObject.Properties | ForEach-Object { $_.Value })
    foreach ($outfit in $manifest.outfits.PSObject.Properties) {
        if ($outfit.Value.idle) { $sprites += $outfit.Value.idle }
        if ($outfit.Value.interactionFive) { $sprites += @($outfit.Value.interactionFive.atlases.PSObject.Properties | ForEach-Object { $_.Value }) }
        $sprites += @($outfit.Value.motions.PSObject.Properties | ForEach-Object { $_.Value })
    }
    $referenced = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($sprite in $sprites) {
        $image = [IO.Path]::GetFullPath((Join-Path $pack $sprite.file))
        if (-not $image.StartsWith($pack + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw "Invalid image reference: $image" }
        Confirm-ManagedPath $image
        if (-not (Test-Path -LiteralPath $image -PathType Leaf)) { throw "Missing runtime image: $image" }
        [void]$referenced.Add($image)
    }
    Get-ChildItem -LiteralPath $pack -Recurse -File | Where-Object { $_.Extension -in @('.png','.webp','.jpg','.jpeg') -and -not $referenced.Contains($_.FullName) } | ForEach-Object {
        Confirm-ManagedPath $_.FullName
        Remove-Item -LiteralPath $_.FullName -Force
    }
}
# This is a duplicate of Demo/review; source assets remain in the project.
$duplicate = [IO.Path]::GetFullPath((Join-Path $runtime 'docs/demo'))
if (Test-Path -LiteralPath $duplicate) { Confirm-ManagedPath $duplicate; Remove-Item -LiteralPath $duplicate -Recurse -Force }
if ($DemoSource) {
    $sourceFrames = Join-Path ([IO.Path]::GetFullPath($DemoSource)) 'frames'
    if (-not (Test-Path -LiteralPath (Join-Path $DemoSource 'demo-export.json'))) { throw 'Expected a completed Demo export.' }
    $names = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    Get-ChildItem -LiteralPath $sourceFrames -File | ForEach-Object { [void]$names.Add($_.Name) }
    if ($names.Count -eq 0) { throw 'Demo export has no frames.' }
    $frames = [IO.Path]::GetFullPath((Join-Path $runtime 'Demo/frames'))
    Confirm-ManagedPath $frames
    Get-ChildItem -LiteralPath $frames -File | Where-Object { $_.Name -match '^\d{5}\.webp$' -and -not $names.Contains($_.Name) } | ForEach-Object {
        Confirm-ManagedPath $_.FullName
        Remove-Item -LiteralPath $_.FullName -Force
    }
}
Write-Output "Runtime content trimmed: $runtime"
