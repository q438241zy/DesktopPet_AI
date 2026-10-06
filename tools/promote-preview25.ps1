param([string]$Candidate='D:/VibeCoding/Character/.artifacts/preview25-candidate')
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$candidatePath=[IO.Path]::GetFullPath($Candidate)
$release=[IO.Path]::GetFullPath((Join-Path $root 'Release/win-x64'))
$stamp=Get-Date -Format 'yyyyMMdd-HHmmss'
$backup=[IO.Path]::GetFullPath((Join-Path $root ".artifacts/release-before-preview25-$stamp"))
foreach($path in @($candidatePath,$release,$backup)) {
 if(-not $path.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw "Path outside project: $path"}
}
if(-not (Test-Path -LiteralPath (Join-Path $candidatePath 'DesktopPet.exe'))){throw 'Missing candidate executable'}
$checks=Join-Path $root '.artifacts/preview25-candidate-checks'
if((Get-Content -LiteralPath (Join-Path $checks 'five/five-check.txt') -Tail 1) -ne '5902 checks; 64 appearances.'){throw 'Candidate five-interaction validation incomplete'}
if((Get-Content -LiteralPath (Join-Path $checks 'assets/asset-check.txt') -Raw).Trim() -ne 'PASS: 16 characters, 1857 image definitions decoded.'){throw 'Candidate assets validation incomplete'}
$roster=Get-Content -LiteralPath (Join-Path $root '.artifacts/five-roster-browser/report.json') -Raw | ConvertFrom-Json
if($roster.passed -ne 64 -or $roster.errors.Count -ne 0){throw 'Full browser roster validation incomplete'}
$palms=Get-Content -LiteralPath (Join-Path $root '.artifacts/five-palms-final/report.json') -Raw | ConvertFrom-Json
if($palms.passed -ne 32 -or $palms.errors.Count -ne 0){throw 'Final Q palm validation incomplete'}
$web=Get-Content -LiteralPath (Join-Path $root '.artifacts/interaction-five-review-I3/report.json') -Raw | ConvertFrom-Json
if($web.status -ne 'passed' -or $web.reports.Count -ne 22){throw 'Browser interaction validation incomplete'}
$sourceDemo=(Get-Content -LiteralPath (Join-Path $root 'artifacts/current-deepseek-demo.txt') -Raw).Trim()
if((Get-FileHash -LiteralPath (Join-Path $sourceDemo 'DeepSeek-demo.html')).Hash -ne (Get-FileHash -LiteralPath (Join-Path $candidatePath 'Demo/DeepSeek-demo.html')).Hash){throw 'Candidate permanent Demo is stale'}
$data=Join-Path $env:LOCALAPPDATA 'DesktopPetAI'
$dataBackup=Join-Path $root ".artifacts/user-data-before-preview25-$stamp"
$stateFile=Join-Path $data 'state.json'
$stateHash=(Get-FileHash -LiteralPath $stateFile).Hash
$oldHash=(Get-FileHash -LiteralPath (Join-Path $release 'DesktopPet.exe')).Hash
$newHash=(Get-FileHash -LiteralPath (Join-Path $candidatePath 'DesktopPet.exe')).Hash
Get-Process DesktopPet -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq (Join-Path $release 'DesktopPet.exe') } | Stop-Process -Force
Copy-Item -LiteralPath $data -Destination $dataBackup -Recurse
Move-Item -LiteralPath $release -Destination $backup
try {Move-Item -LiteralPath $candidatePath -Destination $release}
catch {Move-Item -LiteralPath $backup -Destination $release;throw}
if((Get-FileHash -LiteralPath $stateFile).Hash -ne $stateHash){throw 'Normal user state changed during installation'}
$shell=New-Object -ComObject WScript.Shell
$shortcutPath=Join-Path ([Environment]::GetFolderPath('Desktop')) 'DeepSeek-動作Demo.lnk'
$shortcut=$shell.CreateShortcut($shortcutPath)
if([IO.Path]::GetFullPath($shortcut.TargetPath) -ne [IO.Path]::GetFullPath((Join-Path $release 'Demo/CloudClub/index.html'))){throw 'Existing desktop Demo shortcut points elsewhere'}
$record=[ordered]@{version='1.2.0-preview.25';installedAt=(Get-Date).ToString('o');release=$release;backup=$backup;dataBackup=$dataBackup;oldExecutableSha256=$oldHash;newExecutableSha256=$newHash;stateSha256Before=$stateHash;stateSha256AfterInstall=(Get-FileHash -LiteralPath $stateFile).Hash;desktopShortcut=$shortcutPath;rosterAppearances=$roster.passed;startup='pending'}
$record | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $root '.artifacts/preview25-release-record.json') -Encoding utf8
Write-Output "Installed Preview25: $release; backup: $backup"
