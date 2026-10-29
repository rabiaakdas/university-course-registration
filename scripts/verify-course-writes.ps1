param([ValidateSet('full','unit','core','preview','factories','specifications','eligibility','state')][string]$Mode = 'full', [switch]$Reverse, [switch]$ProbeFailure)
# Real LocalDB + HTTP checks, no test framework or permanent test project.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$work = Join-Path $root 'obj/day5-checks'
New-Item -ItemType Directory -Path $work -Force | Out-Null
$project = [System.Security.SecurityElement]::Escape((Join-Path $root 'UniversityRegistration.csproj'))
$related = [System.Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'RelatedWriteChecks.cs'))
$catalog = [System.Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'CatalogChecks.cs'))
$enrollment = [System.Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'EnrollmentChecks.cs'))
$batch = [System.Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'BatchEnrollmentChecks.cs'))
$core = [System.Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'CoreBehaviorChecks.cs'))
$priority = [System.Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'PriorityChecks.cs'))
$factories = [System.Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'FactoryExperiments.cs'))
$specifications = [System.Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'SpecificationChecks.cs'))
$eligibility = [System.Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'EligibilityChecks.cs'))
$state = [System.Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'StateChecks.cs'))
$stateExperiment = [System.Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'EnrollmentStateExperiment.cs'))
$source = [System.Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'CourseWriteChecks.cs'))
@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup>
  <ItemGroup><ProjectReference Include="$project" /><Compile Include="$source" /><Compile Include="$core" /><Compile Include="$priority" /><Compile Include="$factories" /><Compile Include="$specifications" /><Compile Include="$eligibility" /><Compile Include="$state" /><Compile Include="$stateExperiment" /><Compile Include="$related" /><Compile Include="$catalog" /><Compile Include="$enrollment" /><Compile Include="$batch" /><FrameworkReference Include="Microsoft.AspNetCore.App" /></ItemGroup>
</Project>
"@ | Set-Content (Join-Path $work 'Checks.csproj') -Encoding utf8
dotnet run --project (Join-Path $work 'Checks.csproj') -- $root $Mode $Reverse.IsPresent $ProbeFailure.IsPresent
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
