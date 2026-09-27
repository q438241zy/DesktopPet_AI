param(
    [Parameter(Mandatory=$true)][string]$DsGoRoot
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$artRoot = Join-Path $DsGoRoot 'packages/client/pet/src/assets'
$sourceRoot = Join-Path $projectRoot 'src/DesktopPet.App/Assets/Characters'
$characters = @(
    @('gpt','GPT','#A895E8'), @('claude','Claude','#D59477'),
    @('gemini','Gemini','#799CD9'), @('grok','Grok','#A0A4B8'), @('whale','DeepSeek','#74B3E6'),
    @('qwen','Qwen','#A58BE4'), @('zhipu','GLM','#78BEA7'), @('kimi','Kimi','#D198BA')
)
$timings = @{
    eat=@(300,280,350,250,520,700); feed=@(180,210,330,320,320,440); chat=@(280,260,440,520,320,580)
    bonk=@(220,130,160,330,540,620); farewell=@(300,440,500,650,480,630); walk=@(160,160,160,160,160,160)
    angry=@(220,240,280,440,340,700); headpat=@(250,280,390,400,400,540); curl=@(350,350,400,450,500,650)
    'ball-hit'=@(150,170,350,420,360,600); 'ball-miss'=@(220,240,330,500,400,550); pickup=@(180,180,200,200,200,200)
    think=@(350,400,700,800,550,400); jump=@(260,280,400,420,330,460); peek=@(400,550,400,450,800,650)
    shaken=@(120,120,140,120,120,140); 'shaken-strong'=@(90,90,100,90,90,100)
}
foreach ($entry in $characters) {
    $id = $entry[0]
    $dest = Join-Path $sourceRoot $id
    New-Item -ItemType Directory -Path "$dest/motions","$dest/outfits" -Force | Out-Null
    Copy-Item -LiteralPath "$artRoot/companions/$id.png" -Destination "$dest/atlas.png"
    Copy-Item -LiteralPath "$artRoot/dizzy/$id.png" -Destination "$dest/dizzy.png"
    $motions = [ordered]@{}
    foreach ($motion in ($timings.Keys | Sort-Object)) {
        Copy-Item -LiteralPath "$artRoot/motions/$id/$motion.webp" -Destination "$dest/motions/$motion.webp"
        $motions[$motion] = @{ file="motions/$motion.webp"; columns=3; rows=2; frameMs=$timings[$motion] }
    }
    $outfits = [ordered]@{}
    foreach ($outfit in @('swim','wedding')) {
            Copy-Item -LiteralPath "$artRoot/outfits/$id-$outfit.webp" -Destination "$dest/outfits/$outfit.webp"
            $outfitMotions = [ordered]@{}
            if ($outfit -eq 'wedding') {
                New-Item -ItemType Directory -Path "$dest/outfits/wedding" -Force | Out-Null
                foreach ($motion in @('walk','headpat')) {
                    Copy-Item -LiteralPath "$artRoot/outfits/$id/wedding/$motion.webp" -Destination "$dest/outfits/wedding/$motion.webp"
                    $outfitMotions[$motion] = @{file="outfits/wedding/$motion.webp";columns=3;rows=2;frameMs=$timings[$motion]}
                }
            }
            $outfits[$outfit] = @{ name=$(if($outfit -eq 'swim'){'泳装'}else{'婚纱'}); idle=@{file="outfits/$outfit.webp";columns=$(if($id -eq 'whale' -and $outfit -eq 'swim'){3}else{1});rows=$(if($id -eq 'whale' -and $outfit -eq 'swim'){2}else{1})}; motions=$outfitMotions }
    }
    $manifest = [ordered]@{version=1;id=$id;name=$entry[1];accent=$entry[2];category='chibi';atlas=@{file='atlas.png';columns=3;rows=2};dizzy=@{file='dizzy.png';columns=1;rows=1};motions=$motions;outfits=$outfits}
    [IO.File]::WriteAllText("$dest/pet.json", ($manifest | ConvertTo-Json -Depth 12) + "`n", [Text.UTF8Encoding]::new($false))
}
Write-Output "Imported $($characters.Count) characters. No other dashboard characters were copied."
