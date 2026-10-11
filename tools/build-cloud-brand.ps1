param([string]$PreviewDirectory = '.artifacts/cloud-brand-v20')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$preview = [IO.Path]::GetFullPath((Join-Path $projectRoot $PreviewDirectory))
$buildDirectory = Join-Path $projectRoot '.artifacts/cloud-brand-builder'
New-Item -ItemType Directory -Path $preview, $buildDirectory -Force | Out-Null
$mascotSource = [Security.SecurityElement]::Escape((Join-Path $projectRoot 'src/DesktopPet.App/CloudMascot.cs'))
$project = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0-windows</TargetFramework><UseWPF>true</UseWPF><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup>
  <ItemGroup><Compile Include="$mascotSource" Link="CloudMascot.cs"/><Compile Include="Program.cs"/></ItemGroup>
</Project>
"@
Set-Content -LiteralPath (Join-Path $buildDirectory 'CloudBrand.csproj') -Value $project -Encoding utf8
$program = @'
using DesktopPet.App;
using System.IO;
internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        CloudMascot.WriteIcon(args[0]);
        foreach (var size in new[] { 16, 20, 24, 32, 40, 48, 64, 88, 96, 128, 256 })
            File.WriteAllBytes(Path.Combine(args[1], $"cloud-pet-{size}.png"), CloudMascot.Png(size));
        Console.WriteLine("Cloud pet vector and nine-size ICO rendered.");
    }
}
'@
Set-Content -LiteralPath (Join-Path $buildDirectory 'Program.cs') -Value $program -Encoding utf8
dotnet build (Join-Path $buildDirectory 'CloudBrand.csproj') -c Release --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw 'Cloud mascot rendering utility failed to build.' }
& (Join-Path $buildDirectory 'bin/Release/net10.0-windows/CloudBrand.exe') (Join-Path $projectRoot 'src/DesktopPet.App/Assets/cloud.ico') $preview
if ($LASTEXITCODE -ne 0) { throw 'Cloud mascot rendering failed.' }
Write-Output "Native previews: $preview"
