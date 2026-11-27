#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Init','Setup','Up','Logs','Stop','Down','Verify')][string]$Action,
    [string]$EnvFile = '.local/compose/local.env',
    [switch]$AcceptSqlServerLicense
)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$settings=@{}
$owned=$false
$testDirectory=$null
$exitCode=0

# Capture output; do not echo resolved configuration, SQL input or raw DB errors.
function Native([string]$exe,[string[]]$arguments,[string]$inputText='',[int]$seconds=600) {
    $si=[Diagnostics.ProcessStartInfo]::new($exe)
    $si.WorkingDirectory=$repo; $si.UseShellExecute=$false
    $si.RedirectStandardOutput=$true; $si.RedirectStandardError=$true; $si.RedirectStandardInput=$true
    foreach($arg in $arguments){$si.ArgumentList.Add($arg)}
    foreach($key in $settings.Keys){$si.Environment[$key]=$settings[$key]}
    $p=[Diagnostics.Process]::new(); $p.StartInfo=$si
    try {
        if(-not $p.Start()){throw 'Process did not start.'}
        $stdout=$p.StandardOutput.ReadToEndAsync(); $stderr=$p.StandardError.ReadToEndAsync()
        $writing=$p.StandardInput.WriteAsync($inputText)
        if(-not $writing.Wait([TimeSpan]::FromSeconds($seconds))){$p.Kill($true); throw "$exe input timed out."}
        $p.StandardInput.Close()
        if(-not $p.WaitForExit($seconds*1000)){$p.Kill($true); $p.WaitForExit(); throw "$exe timed out."}
        $output=$stdout.GetAwaiter().GetResult(); $null=$stderr.GetAwaiter().GetResult()
        if($p.ExitCode -ne 0){throw "$exe failed (exit $($p.ExitCode)); raw output suppressed to protect credentials."}
        return $output
    } finally {$p.Dispose()}
}
function Compose([string[]]$arguments,[string]$inputText='',[int]$seconds=600) {
    Native docker (@('compose','--project-name',$settings.COMPOSE_PROJECT_NAME,'--env-file',$EnvFile,'-f',(Join-Path $repo 'compose.yaml'))+$arguments) $inputText $seconds
}
function Sql([string]$text,[switch]$Runtime,[string]$Database='university_compose',[int]$Seconds=135) {
    if($Database -notin @('master','university_compose')){throw 'Unexpected database.'}
    if($Runtime){
        $command='IFS= read -r SQLCMDPASSWORD; export SQLCMDPASSWORD; exec /opt/mssql-tools18/bin/sqlcmd -S localhost -U university_runtime -C -b -r1 -l 5 -t 120 -d university_compose'
        $text=$settings.SQL_RUNTIME_PASSWORD+"`n"+$text
    } else {
        $command='export SQLCMDPASSWORD="$MSSQL_SA_PASSWORD"; exec /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -r1 -l 5 -t 120 -d '+$Database
    }
    Compose @('exec','-T','db','bash','-c',$command) ($text+"`n") $Seconds
}
function Ready {
    $until=[DateTime]::UtcNow.AddSeconds(120)
    do {
        try {$null=Sql 'SET NOCOUNT ON; SELECT 1;' -Database master -Seconds 10; return} catch {
            if([DateTime]::UtcNow -ge $until){throw 'SQL readiness failed within 120 seconds; no API was started.'}
            Start-Sleep -Milliseconds 750
        }
    } while($true)
}
function Prepare {
    $null=Compose @('config','--quiet')
    $null=Compose @('up','-d','db')
    Ready
    $null=Sql "IF DB_ID(N'university_compose') IS NULL CREATE DATABASE university_compose;" -Database master
    $migration=Join-Path ([IO.Path]::GetDirectoryName($EnvFile)) ('migration-'+[Guid]::NewGuid().ToString('N')+'.sql')
    try {
        $null=Native dotnet @('tool','restore','--tool-manifest','dotnet-tools.json')
        $null=Native dotnet @('ef','migrations','has-pending-model-changes','--project','UniversityRegistration.csproj')
        $null=Native dotnet @('ef','migrations','script','--idempotent','--project','UniversityRegistration.csproj','--output',$migration)
        $null=Sql ([IO.File]::ReadAllText($migration))
    } finally {if(Test-Path -LiteralPath $migration){Remove-Item -LiteralPath $migration}}
    # Password alphabet is validated below; SQL identifiers are fixed.
    $password=$settings.SQL_RUNTIME_PASSWORD
    $null=Sql "IF SUSER_ID(N'university_runtime') IS NULL CREATE LOGIN university_runtime WITH PASSWORD=N'$password', CHECK_POLICY=ON;" -Database master
    $null=Sql "IF USER_ID(N'university_runtime') IS NULL CREATE USER university_runtime FOR LOGIN university_runtime;"
    $null=Sql ([IO.File]::ReadAllText((Join-Path $repo 'sql/app/compose-runtime-permissions.sql')))
    # Existing passwords are not reset on repeated setup. This real login must succeed.
    $null=Sql 'SELECT TOP (1) Id FROM dbo.Courses;' -Runtime
    Write-Host 'PASS setup/migrations and runtime SQL login (existing data retained)'
}
function NewEnvironment([string]$project,[int]$port) {
    if(-not $AcceptSqlServerLicense){throw 'Use -AcceptSqlServerLicense after accepting SQL Server Developer development/test terms.'}
    if(Test-Path -LiteralPath $EnvFile){throw 'Environment file already exists; it was not replaced.'}
    $null=New-Item -ItemType Directory -Force -Path ([IO.Path]::GetDirectoryName($EnvFile))
    function Password { 'Aa1!'+[Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(24)) }
    $values=@("COMPOSE_PROJECT_NAME=$project","API_PORT=$port","API_IMAGE=university-registration:${project}",
        'SQL_IMAGE=mcr.microsoft.com/mssql/server:2022-latest','ACCEPT_EULA=Y',"SQL_SA_PASSWORD=$(Password)","SQL_RUNTIME_PASSWORD=$(Password)")
    [IO.File]::WriteAllLines($EnvFile,$values)
}
function LoadEnvironment {
    foreach($line in [IO.File]::ReadAllLines($EnvFile)) {
        if($line.Trim().Length -eq 0 -or $line.StartsWith('#')){continue}
        $pair=$line.Split('=',2); if($pair.Length -ne 2){throw 'Invalid environment file.'}
        if($pair[0] -notin @('COMPOSE_PROJECT_NAME','API_PORT','API_IMAGE','SQL_IMAGE','ACCEPT_EULA','SQL_SA_PASSWORD','SQL_RUNTIME_PASSWORD')){throw 'Unexpected environment key.'}
        $settings[$pair[0]]=$pair[1]
    }
    if($settings.COMPOSE_PROJECT_NAME -notmatch '^university-[a-z0-9-]{1,55}$'){throw 'Invalid Compose project name.'}
    foreach($key in @('SQL_SA_PASSWORD','SQL_RUNTIME_PASSWORD')) {
        if($settings[$key] -cnotmatch '^[A-Za-z0-9!_-]{16,128}$'){throw 'Password must use 16..128 characters from letters, digits, !, _ and -.'}
    }
    if($settings.SQL_SA_PASSWORD -ceq $settings.SQL_RUNTIME_PASSWORD){throw 'Setup and runtime passwords must differ.'}
    if($settings.ACCEPT_EULA -ne 'Y'){throw 'Explicit license acceptance required.'}
    $port=0
    if(-not [int]::TryParse($settings.API_PORT,[ref]$port) -or $port -lt 1024 -or $port -gt 65535){throw 'Invalid loopback API port.'}
}
function ReadCourses { Invoke-RestMethod "http://127.0.0.1:$($settings.API_PORT)/courses" -TimeoutSec 15 }
function WaitApi {
    $until=[DateTime]::UtcNow.AddSeconds(60)
    do {try {$null=ReadCourses; return} catch {if([DateTime]::UtcNow -ge $until){throw 'API DB-backed GET failed.'}; Start-Sleep -Milliseconds 500}} while($true)
}
Push-Location $repo
try {
    $EnvFile=[IO.Path]::GetFullPath($EnvFile,$repo)
    if($Action -eq 'Init') {NewEnvironment 'university-local' 18080; Write-Host 'Local environment created; values were not printed.'}
    else {
        if((Native docker @('info','--format','{{.OSType}}')).Trim() -ne 'linux'){throw 'A running Linux Docker engine is required.'}
        $null=Native docker @('compose','version')
        if((Native docker @('info','--format','{{.Architecture}}')).Trim() -notin @('x86_64','amd64')){throw 'SQL Server containers require a supported x86-64 Linux engine.'}
        if($Action -eq 'Verify') {
            $id=[Guid]::NewGuid().ToString('N')
            $testDirectory=Join-Path $repo ".local/compose/test-$id"
            $EnvFile=Join-Path $testDirectory 'test.env'
            $listener=[Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,0)
            try {$listener.Start();$port=$listener.LocalEndpoint.Port} finally {$listener.Stop()}
            NewEnvironment "university-test-$id" $port
        }
        LoadEnvironment
        if($Action -eq 'Verify') {
            foreach($kind in @('container','network','volume')) {
                if(-not [string]::IsNullOrWhiteSpace((Native docker @($kind,'ls','-q','--filter',"label=com.docker.compose.project=$($settings.COMPOSE_PROJECT_NAME)")))){throw 'Unexpected pre-existing test resources.'}
            }
            $owned=$true
            Write-Host "Isolated test project: $($settings.COMPOSE_PROJECT_NAME)"
            Prepare
            $null=Compose @('build','api')
            $null=Sql "INSERT dbo.Courses(Code,Name,Credits) VALUES(N'COMPOSE52',N'Compose persistence fixture',3); INSERT dbo.AcademicTerms(Name,StartsOn,EndsOn) VALUES(N'COMPOSE52','2026-01-01','2026-06-01'); INSERT dbo.CourseOfferings(CourseId,AcademicTermId,SectionCode,Capacity,OccupiedSeats) SELECT c.Id,t.Id,N'A',2,0 FROM dbo.Courses c CROSS JOIN dbo.AcademicTerms t WHERE c.Code=N'COMPOSE52' AND t.Name=N'COMPOSE52';" -Runtime
            $null=Sql "SET XACT_ABORT ON; IF IS_ROLEMEMBER('db_owner')=1 OR IS_ROLEMEMBER('db_ddladmin')=1 THROW 51000,'Excess privilege',1; BEGIN TRANSACTION; SELECT Id FROM dbo.CourseOfferings WITH (UPDLOCK,HOLDLOCK); SELECT NEXT VALUE FOR dbo.WaitlistQueueNumbers; UPDATE dbo.Courses SET Name=Name WHERE Code=N'COMPOSE52'; ROLLBACK; BEGIN TRY EXEC(N'CREATE TABLE dbo.Forbidden52(Id int)'); THROW 51000,'DDL unexpectedly allowed',1; END TRY BEGIN CATCH IF ERROR_NUMBER() NOT IN (229,262) THROW; END CATCH;" -Runtime
            $null=Compose @('up','-d','api'); WaitApi
            $rows=@(ReadCourses)
            if($rows.Count -ne 1 -or $rows[0].code -ne 'COMPOSE52' -or $rows[0].name -ne 'Compose persistence fixture'){throw 'Unexpected public API fixture.'}
            $courseId=$rows[0].id
            $dbBefore=(Compose @('ps','-q','db')).Trim()
            $historyBefore=Sql 'SET NOCOUNT ON; SELECT MigrationId FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;'
            $null=Compose @('stop','db')
            $response=Invoke-WebRequest "http://127.0.0.1:$($settings.API_PORT)/courses" -SkipHttpErrorCheck -TimeoutSec 20
            if($response.StatusCode -ne 500 -or $response.Headers['Content-Type'] -notmatch 'application/problem\+json' -or $response.Content -match 'SqlException|Password=|Server=db'){throw 'Unsafe/unexpected outage response.'}
            $null=Compose @('up','-d','--force-recreate','db'); Ready; WaitApi
            if((Compose @('ps','-q','db')).Trim() -eq $dbBefore){throw 'DB container was not recreated.'}
            Prepare
            if((Sql 'SET NOCOUNT ON; SELECT MigrationId FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;') -ne $historyBefore){throw 'Second migration changed history.'}
            $after=@(ReadCourses)
            if($after.Count -ne 1 -or $after[0].id -ne $courseId -or $after[0].name -ne $rows[0].name){throw 'Persistence/setup replay changed data.'}
            $null=Sql "IF (SELECT COUNT_BIG(*) FROM dbo.CourseOfferings o JOIN dbo.Courses c ON c.Id=o.CourseId JOIN dbo.AcademicTerms t ON t.Id=o.AcademicTermId WHERE c.Code=N'COMPOSE52' AND t.Name=N'COMPOSE52' AND o.Capacity=2)<>1 THROW 51000,'Relationship not preserved',1;" -Runtime
            Write-Host 'PASS API->db DNS, runtime DML/sequence/locks/DDL denial, volume recreation, outage recovery, second setup and relationships'
        } elseif($Action -eq 'Setup'){Prepare}
        elseif($Action -eq 'Up'){Prepare; $null=Compose @('up','-d','--build','api'); WaitApi; Write-Host 'API ready (DB-backed GET).'}
        elseif($Action -eq 'Down'){$null=Compose @('down'); Write-Host 'Stopped; named data volume retained.'}
        elseif($Action -eq 'Stop'){$null=Compose @('stop')}
        elseif($Action -eq 'Logs'){$output=Compose @('logs','--tail','40','api'); foreach($key in @('SQL_SA_PASSWORD','SQL_RUNTIME_PASSWORD')){$output=$output.Replace($settings[$key],'[redacted]')}; Write-Host $output}
    }
} catch {[Console]::Error.WriteLine("FAIL compose: $($_.Exception.Message)"); $exitCode=1}
finally {
    if($owned) {
        try {
            $volumes=Native docker @('volume','ls','-q','--filter',"label=com.docker.compose.project=$($settings.COMPOSE_PROJECT_NAME)")
            foreach($volume in ($volumes -split '\r?\n' | Where-Object {$_})) {
                if($volume -ne "$($settings.COMPOSE_PROJECT_NAME)_sql-data"){throw 'Unexpected volume; cleanup refused.'}
            }
            $null=Compose @('down','--volumes')
            $images=Native docker @('image','ls','-q',$settings.API_IMAGE)
            if($images.Trim()){$null=Native docker @('image','rm',$settings.API_IMAGE)}
        } catch {[Console]::Error.WriteLine('Test Docker cleanup failed; original failure preserved.');$exitCode=1}
    }
    if($testDirectory -and (Test-Path -LiteralPath $testDirectory)) {
        try {
            $resolved=(Resolve-Path -LiteralPath $testDirectory).Path
            if($resolved -ne [IO.Path]::GetFullPath($testDirectory) -or (Split-Path $resolved -Leaf) -notmatch '^test-[a-f0-9]{32}$'){throw 'Cleanup path rejected.'}
            Remove-Item -LiteralPath $resolved -Recurse -Force
        } catch {[Console]::Error.WriteLine('Test file cleanup failed.');$exitCode=1}
    }
    Pop-Location
}
exit $exitCode
