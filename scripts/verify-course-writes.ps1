# Real LocalDB + HTTP checks, no test framework or permanent test project.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$work = Join-Path $root 'obj/day5-checks'
New-Item -ItemType Directory -Path $work -Force | Out-Null
$project = [System.Security.SecurityElement]::Escape((Join-Path $root 'UniversityRegistration.csproj'))
$source = [System.Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'CourseWriteChecks.cs'))
@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup>
  <ItemGroup><ProjectReference Include="$project" /><Compile Include="$source" /><FrameworkReference Include="Microsoft.AspNetCore.App" /></ItemGroup>
</Project>
"@ | Set-Content (Join-Path $work 'Checks.csproj') -Encoding utf8
dotnet run --project (Join-Path $work 'Checks.csproj') -- $root
if ($LASTEXITCODE -ne 0) { throw 'Ders yazma kontrolleri başarısız.' }
