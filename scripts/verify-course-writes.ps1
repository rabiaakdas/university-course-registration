param([ValidateSet('full','unit','core','preview','factories','specifications','eligibility','state','decorator','adapter','summary','builder','observer','repository','cqrs','identity-model','accounts','cookie','jwt','refresh','refresh-family','password-reset','roles','resource-access','auth-abuse','web-client','cors-lab','oidc','concurrency-lab','seat-capacity','course-version')][string]$Mode = 'full', [switch]$Reverse, [switch]$ProbeFailure)
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
$decorator = [System.Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'DecoratorChecks.cs'))
$observer = [System.Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'EvaluationLogObserver.cs'))
$adapter = [System.Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'AdapterChecks.cs'))
$notificationDemo = [System.Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'NotificationAdapterDemo.cs'))
$summary = [System.Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'SummaryChecks.cs'))
$builder = [System.Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'EligibilityScenarioBuilder.cs'))
$builderChecks = [System.Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'BuilderChecks.cs'))
$approvalObserver = [System.Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'ObserverChecks.cs'))
$repository = [System.Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'EnrollmentRepositoryExperiment.cs'))
$repositoryChecks = [System.Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'RepositoryChecks.cs'))
$mediator = [System.Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'MediatorExperiment.cs'))
$cqrs = [System.Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'CqrsChecks.cs'))
$identityModel = [System.Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'IdentityModelChecks.cs'))
$accounts = [System.Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'AccountsChecks.cs'))
$cookie = [System.Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'CookieSessionChecks.cs'))
$jwt = [System.Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'JwtChecks.cs'))
$refresh = [System.Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'RefreshChecks.cs'))
$passwordReset = [System.Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'PasswordResetChecks.cs'))
$catalogIdentity = [System.Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'CatalogTestIdentity.cs'))
$courseVersion = [System.Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'CourseVersionChecks.cs'))
$seatCapacity = [System.Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'SeatCapacityChecks.cs'))
$concurrencyLab = [System.Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'ConcurrencyLabChecks.cs'))
$oidcChecks = [System.Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'OidcChecks.cs'))
$webChecks = [System.Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'WebClientChecks.cs'))
$abuseChecks = [System.Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'AuthAbuseChecks.cs'))
$abuseSettings = [System.Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'AuthAbuseTestSettings.cs'))
$resourceAccess = [System.Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'ResourceAccessChecks.cs'))
$roles = [System.Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'RolesChecks.cs'))
$source = [System.Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'CourseWriteChecks.cs'))
@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup>
  <ItemGroup><Compile Include="$catalogIdentity" /><Compile Include="$roles" /><Compile Include="$resourceAccess" /><Compile Include="$abuseSettings" /><Compile Include="$abuseChecks" /><Compile Include="$webChecks" /><Compile Include="$oidcChecks" /><Compile Include="$concurrencyLab" /><Compile Include="$seatCapacity" /><Compile Include="$courseVersion" /><Compile Include="$accounts" /><Compile Include="$cookie" /><Compile Include="$jwt" /><Compile Include="$refresh" /><Compile Include="$passwordReset" /></ItemGroup>
  <ItemGroup><ProjectReference Include="$project" /><Compile Include="$source" /><Compile Include="$identityModel" /><Compile Include="$mediator" /><Compile Include="$cqrs" /><Compile Include="$repository" /><Compile Include="$repositoryChecks" /><Compile Include="$approvalObserver" /><Compile Include="$builder" /><Compile Include="$builderChecks" /><Compile Include="$summary" /><Compile Include="$adapter" /><Compile Include="$notificationDemo" /><Compile Include="$decorator" /><Compile Include="$observer" /><Compile Include="$core" /><Compile Include="$priority" /><Compile Include="$factories" /><Compile Include="$specifications" /><Compile Include="$eligibility" /><Compile Include="$state" /><Compile Include="$stateExperiment" /><Compile Include="$related" /><Compile Include="$catalog" /><Compile Include="$enrollment" /><Compile Include="$batch" /><FrameworkReference Include="Microsoft.AspNetCore.App" /></ItemGroup>
</Project>
"@ | Set-Content (Join-Path $work 'Checks.csproj') -Encoding utf8
dotnet run --project (Join-Path $work 'Checks.csproj') -- $root $Mode $Reverse.IsPresent $ProbeFailure.IsPresent
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
