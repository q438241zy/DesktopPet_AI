param([string]$DemoDirectory = '')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$demoRoot = if ($DemoDirectory) { [IO.Path]::GetFullPath($DemoDirectory) } else { Join-Path $projectRoot 'Release/win-x64/Demo' }
$source = Join-Path $projectRoot 'docs/demo/interaction-five'
$target = Join-Path $demoRoot 'InteractionFive'
New-Item -ItemType Directory -Path $demoRoot -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs/demo/sprite-cells.js') -Destination $demoRoot -Force
$names = @('index.html','style.css','review.css','model.js','items.js','app.js','art.js','roster.js','README.md',
    'art/deepseek-chibi-v1.png','art/geometry.json',
    'art/provenance.json','art/chibi-prompt.txt','art/realistic-prompt.txt','art/realistic-framing-repair.txt',
    'art/interaction-chibi-v2.png',
    'art/interaction-chibi-prompt.txt','art/interaction-chibi-framing.txt',
    'art/deepseek-original-reference.png','art/adult-contact-anchors.json',
    'art/adult-v5-prompts.json','art/adult-v5-provenance.json','art/adult-selected.json','art/handoff-alpha-replacement.txt',
    'art/real-adult-highfive-final.png','art/real-adult-rps-final.png',
    'art/real-adult-handoff-final.png','art/real-adult-book-final.png','art/real-adult-photo-final.png')
foreach ($name in $names) {
    $inputPath = Join-Path $source $name
    if (-not (Test-Path -LiteralPath $inputPath -PathType Leaf)) { throw "Missing Demo source: $name" }
}
$total = 0
foreach ($name in $names) {
    $inputPath = Join-Path $source $name
    $outputPath = Join-Path $target $name
    New-Item -ItemType Directory -Path (Split-Path -Parent $outputPath) -Force | Out-Null
    Copy-Item -LiteralPath $inputPath -Destination $outputPath -Force
    if ((Get-FileHash -LiteralPath $inputPath).Hash -ne (Get-FileHash -LiteralPath $outputPath).Hash) { throw "Demo copy mismatch: $name" }
    $total += (Get-Item -LiteralPath $outputPath).Length
}
$rosterPath = Join-Path $target 'roster.js'
$roster = [IO.File]::ReadAllText($rosterPath)
$roster = [Regex]::Replace($roster, '"assetRoot":"[^"]+"', '"assetRoot":"../../Assets/Characters"', 1)
[IO.File]::WriteAllText($rosterPath, $roster, [Text.UTF8Encoding]::new($false))
$packTarget = Join-Path $target 'packs'
New-Item -ItemType Directory -Path $packTarget -Force | Out-Null
$packIndex = $roster.Substring('globalThis.FIVE_ROSTER='.Length).Trim().TrimEnd(';') | ConvertFrom-Json
foreach ($appearance in $packIndex.appearances) {
    $inputPath = Join-Path $source $appearance.module
    $outputPath = Join-Path $target $appearance.module
    Copy-Item -LiteralPath $inputPath -Destination $outputPath -Force
    if ((Get-FileHash -LiteralPath $inputPath).Hash -ne (Get-FileHash -LiteralPath $outputPath).Hash) { throw "Demo pack copy mismatch: $($appearance.id)" }
    $total += (Get-Item -LiteralPath $outputPath).Length
}
Write-Output "Five interactions Demo: $target ($([Math]::Round($total / 1MB, 2)) MiB)"
