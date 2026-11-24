using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using UniversityRegistration.Application;
using UniversityRegistration.Data;
using UniversityRegistration.Models;
using System.Text.RegularExpressions;

internal static class DatabaseOperationsChecks
{
    const string Latest="20261124111639_AddOfferingCapacityReportView";
    const string Previous="20261113125352_AddDroppedEnrollmentStatus";
    static void Check(bool ok,string message){if(!ok)throw new BehaviorFailure("Database operations: "+message);}
    public static async Task Run(string root,string connection)
    {
        var source=new SqlConnectionStringBuilder(connection){Pooling=false};
        connection=source.ConnectionString;
        Check(source.DataSource==@"(localdb)\MSSQLLocalDB"&&Regex.IsMatch(source.InitialCatalog,@"^university_registration_tests_day6_[a-f0-9]{32}$"),"isolated source");
        var restoredName="university_registration_tests_day50_"+Guid.NewGuid().ToString("N");
        var target=new SqlConnectionStringBuilder(connection){InitialCatalog=restoredName};
        var master=new SqlConnectionStringBuilder(connection){InitialCatalog="master"};
        var directory=Path.Combine(Path.GetTempPath(),"university-day50-"+Guid.NewGuid().ToString("N"));
        Check(!Directory.Exists(directory),"new external directory");Directory.CreateDirectory(directory);
        var backup=Path.Combine(directory,"source.bak");var ownedFiles=new List<string>{backup};
        var login="Day50_"+Guid.NewGuid().ToString("N");bool loginOwned=false;
        DateTime earliest=DateTime.Now.AddMinutes(-1);bool sourceOwned=false,targetOwned=false;Exception? failure=null;
        UniversityDbContext Db(string cs)=>new(new DbContextOptionsBuilder<UniversityDbContext>().UseSqlServer(cs).Options);
        async Task<int> Exec(string cs,string sql,params SqlParameter[] parameters)
        {await using var c=new SqlConnection(cs);await c.OpenAsync();using var cmd=c.CreateCommand();cmd.CommandText=sql;cmd.CommandTimeout=120;cmd.Parameters.AddRange(parameters);return await cmd.ExecuteNonQueryAsync();}
        async Task<object?> Scalar(string cs,string sql,params SqlParameter[] parameters)
        {await using var c=new SqlConnection(cs);await c.OpenAsync();using var cmd=c.CreateCommand();cmd.CommandText=sql;cmd.Parameters.AddRange(parameters);return await cmd.ExecuteScalarAsync();}
        try
        {
            Check(await Scalar(master.ConnectionString,"SELECT DB_ID(@n)",new SqlParameter("@n",source.InitialCatalog)) is DBNull,"source unused");
            Check(await Scalar(master.ConnectionString,"SELECT DB_ID(@n)",new SqlParameter("@n",restoredName)) is DBNull,"restore target unused");
            sourceOwned=true; // Existing DB has been ruled out; only this run may now create it.
            await using(var db=Db(connection))
            {
                await db.Database.MigrateAsync();Check(!await db.Set<OfferingCapacityView>().AnyAsync(),"clean install view empty");
                await db.GetService<IMigrator>().MigrateAsync(Previous);
            }
            Check(await Scalar(connection,"SELECT OBJECT_ID(N'dbo.OfferingCapacityReport')") is DBNull,"isolated Down drops view");
            var course=new Course{Code="DB50",Name="Before backup",Credits=3};
            var term=new AcademicTerm{Name="DB50",StartsOn=new(2026,1,1),EndsOn=new(2026,6,1)};
            var students=Enumerable.Range(0,5).Select(i=>new Student{StudentNumber="DB50_"+i,FullName="Synthetic"}).ToArray();
            CourseOffering[] offers;
            await using(var db=Db(connection))
            {
                db.AddRange(course,term);db.AddRange(students);await db.SaveChangesAsync();
                offers=Enumerable.Range(0,2).Select(i=>new CourseOffering{CourseId=course.Id,AcademicTermId=term.Id,SectionCode="S"+i,Capacity=8}).ToArray();db.AddRange(offers);await db.SaveChangesAsync();
                foreach(var (student,i) in students.Select((s,i)=>(s,i)))
                {var e=new Enrollment{StudentId=student.Id,CourseOfferingId=offers[0].Id,CreatedAtUtc=DateTimeOffset.UtcNow};if(i>0)e.TrySubmit();if(i==2)e.TryApprove();if(i==3)e.TryReject("fixture");if(i==4)e.TryDrop();db.Add(e);}
                db.AddRange(Enumerable.Range(0,2).Select(i=>new CourseOfferingTimeSlot{CourseOfferingId=offers[0].Id,Day=DayOfWeek.Monday,StartsAt=new(8+i,0),EndsAt=new(9+i,0)}));
                await SeatCapacityChecks.SaveFixtureAsync(db);
                await db.Database.MigrateAsync(Latest);
            }
            async Task Verify(string cs,string name)
            {
                await using var db=Db(cs);var rows=await db.Set<OfferingCapacityView>().OrderBy(x=>x.OfferingId).ToArrayAsync();
                Check(rows.Length==2&&rows[0].OfferingId==offers[0].Id&&rows[0].CalculatedActiveCount==2L&&rows[0].Capacity==8&&rows[0].StoredOccupiedSeats==2
                    &&rows[1].CalculatedActiveCount==0&&rows[1].StoredOccupiedSeats==0,"view exact/empty/count bigint");
                Check(await db.Enrollments.CountAsync()==5&&await db.Students.CountAsync()==5&&await db.CourseOfferingTimeSlots.CountAsync()==2,"preserved relations");
                Check(await db.Enrollments.AllAsync(e=>students.Select(s=>s.Id).Contains(e.StudentId)&&e.CourseOfferingId==offers[0].Id),"FK identity preserved");
                Check((await new CourseCatalogReader(db).ReadAsync(new("DB50"),default)).Items.Single().Name==name,"application DTO read");
                Check(!db.ChangeTracker.Entries().Any()&&!db.Database.HasPendingModelChanges(),"keyless untracked/model");
                Check((await db.Database.GetAppliedMigrationsAsync()).Last()==Latest&&!(await db.Database.GetPendingMigrationsAsync()).Any(),"migration history");
            }
            await Verify(connection,"Before backup");
            await using(var db=Db(connection)){var before=(await db.Database.GetAppliedMigrationsAsync()).ToArray();await db.Database.MigrateAsync(Latest);Check(before.SequenceEqual(await db.Database.GetAppliedMigrationsAsync()),"second update no-op");}
            // Deliberate test-only mismatch: calculated count must not silently become the counter.
            await Exec(connection,"UPDATE dbo.CourseOfferings SET OccupiedSeats=1 WHERE Id=@id",new SqlParameter("@id",offers[0].Id));
            await using(var db=Db(connection)){var row=await db.Set<OfferingCapacityView>().SingleAsync(x=>x.OfferingId==offers[0].Id);Check(row.CalculatedActiveCount==2&&row.StoredOccupiedSeats==1,"independent count vs counter");}
            await Exec(connection,"UPDATE dbo.CourseOfferings SET OccupiedSeats=2 WHERE Id=@id",new SqlParameter("@id",offers[0].Id));
            Console.WriteLine("PASS: clean/Down/old-data upgrade/view/second update/model; exact enums and distinct counter");
            await Exec(connection,"CREATE PROCEDURE dbo.Day50Report @term int AS BEGIN SET NOCOUNT ON; SELECT * FROM dbo.OfferingCapacityReport WHERE AcademicTermId=@term ORDER BY OfferingId; END");
            await Exec(connection,"CREATE FUNCTION dbo.Day50ReportFunction(@term int) RETURNS TABLE AS RETURN (SELECT * FROM dbo.OfferingCapacityReport WHERE AcademicTermId=@term)");
            foreach(var sql in new[]{"EXEC dbo.Day50Report @term","SELECT * FROM dbo.Day50ReportFunction(@term) ORDER BY OfferingId"})
            {await using var c=new SqlConnection(connection);await c.OpenAsync();using var cmd=c.CreateCommand();cmd.CommandText=sql;cmd.Parameters.AddWithValue("@term",term.Id);await using var r=await cmd.ExecuteReaderAsync();int n=0;while(await r.ReadAsync()){Check(r.GetInt32(r.GetOrdinal("OfferingId"))==offers[n].Id&&r.GetInt64(r.GetOrdinal("CalculatedActiveCount"))==(n==0?2:0),"procedure/function result");n++;}Check(n==2,"two report rows");}
            await Exec(connection,"CREATE TABLE dbo.Day50Input(Id int PRIMARY KEY,Value int NOT NULL); CREATE TABLE dbo.Day50Audit(InsertedRows bigint NOT NULL, Total bigint NOT NULL);");
            await Exec(connection,"CREATE TRIGGER dbo.Day50Trigger ON dbo.Day50Input AFTER INSERT AS BEGIN SET NOCOUNT ON; IF EXISTS(SELECT 1 FROM inserted WHERE Value<0) THROW 51050,'Synthetic negative value',1; INSERT dbo.Day50Audit SELECT COUNT_BIG(*),SUM(CONVERT(bigint,Value)) FROM inserted; END");
            await Exec(connection,"INSERT dbo.Day50Input VALUES(1,2),(2,3),(3,4)");
            Check(Convert.ToInt32(await Scalar(connection,"SELECT COUNT(*) FROM dbo.Day50Audit WHERE InsertedRows=3 AND Total=9"))==1,"one trigger invocation multirow sum");
            Check(Convert.ToInt32(await Scalar(connection,"BEGIN TRY BEGIN TRAN; INSERT dbo.Day50Input VALUES(4,5); INSERT dbo.Day50Input VALUES(5,-1),(6,7); COMMIT; SELECT 0; END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; SELECT ERROR_NUMBER(); END CATCH"))==51050,"actual trigger error");
            Check(Convert.ToInt32(await Scalar(connection,"SELECT COUNT(*) FROM dbo.Day50Input"))==3&&Convert.ToInt32(await Scalar(connection,"SELECT COUNT(*) FROM dbo.Day50Audit"))==1,"trigger rolls back whole explicit transaction");
            Console.WriteLine("PASS: procedure/inline TVF same report; set-based multirow trigger and full transaction rollback");
            await Exec(connection,await File.ReadAllTextAsync(Path.Combine(root,"sql","app","database-operations-permissions-lab.sql")));
            await using(var db=Db(connection))
            {
                await db.Database.OpenConnectionAsync();
                async Task Denied(string sql)
                {bool denied=false;try{await db.Database.ExecuteSqlRawAsync(sql);}catch(SqlException ex)when(ex.Number is 229 or 262 or 15151){denied=true;}Check(denied,"expected permission denial");}
                await db.Database.ExecuteSqlRawAsync("EXECUTE AS USER='Day50ReportUser'");
                try{Check((await db.Set<OfferingCapacityView>().ToArrayAsync()).Length==2,"report role view allowed");await Denied("SELECT * FROM dbo.Enrollments");await Denied("CREATE TABLE dbo.Day50Forbidden(Id int)");}
                finally{await db.Database.ExecuteSqlRawAsync("REVERT");}
                await db.Database.ExecuteSqlRawAsync("EXECUTE AS USER='Day50RuntimeUser'");
                try
                {
                    Check((await new CourseCatalogReader(db).ReadAsync(new("DB50"),default)).Items.Count==1,"runtime real catalog EF read");
                    await using var tx=await db.Database.BeginTransactionAsync();
                    var row=new Course{Code="DB50_RUNTIME",Name="Temporary",Credits=3};db.Add(row);await db.SaveChangesAsync();
                    row.Name="Updated";await db.SaveChangesAsync();Check(row.RowVersion.Length==8,"runtime EF insert/update rowversion");await tx.RollbackAsync();db.ChangeTracker.Clear();
                    await Denied("SELECT * FROM dbo.AspNetUsers");await Denied("CREATE TABLE dbo.Day50Forbidden(Id int)");await Denied("SELECT NEXT VALUE FOR dbo.WaitlistQueueNumbers");
                }
                finally{await db.Database.ExecuteSqlRawAsync("REVERT");}
            }
            Console.WriteLine("PASS: WITHOUT LOGIN report/runtime roles; view/table/DDL/sequence denials and actual EF catalog writes; REVERT in finally");
                        // Authentication mode was observed as mixed; try a temporary narrowly mapped SQL login.
            // Password is ephemeral memory only, passed as a parameter into safely quoted DDL.
            var password=Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))+"aA1!";
            Check(Convert.ToInt32(await Scalar(master.ConnectionString,"SELECT COUNT(*) FROM sys.server_principals WHERE name=@n",new SqlParameter("@n",login)))==0,"temporary login unused");
            await Exec(master.ConnectionString,"DECLARE @sql nvarchar(max)=N'CREATE LOGIN '+QUOTENAME(@name)+N' WITH PASSWORD='+QUOTENAME(@password,CHAR(39))+N', CHECK_POLICY=ON'; EXEC(@sql);",new SqlParameter("@name",login),new SqlParameter("@password",password));loginOwned=true;
            await Exec(connection,"DECLARE @sql nvarchar(max)=N'CREATE USER Day50Connected FOR LOGIN '+QUOTENAME(@name); EXEC(@sql); ALTER ROLE Day50CatalogRuntime ADD MEMBER Day50Connected;",new SqlParameter("@name",login));
            var limited=new SqlConnectionStringBuilder(connection){IntegratedSecurity=false,UserID=login,Password=password,Pooling=false};
            await using(var db=Db(limited.ConnectionString))
            {
                await db.Database.OpenConnectionAsync();
                Check((await new CourseCatalogReader(db).ReadAsync(new("DB50"),default)).Items.Count==1,"separate restricted connection read");
                await using var tx=await db.Database.BeginTransactionAsync();var row=new Course{Code="DB50_CONN",Name="Temporary",Credits=2};db.Add(row);await db.SaveChangesAsync();row.Name="Updated";await db.SaveChangesAsync();await tx.RollbackAsync();db.ChangeTracker.Clear();
                bool denied=false;try{await db.Database.ExecuteSqlRawAsync("SELECT * FROM dbo.Enrollments");}catch(SqlException ex)when(ex.Number==229){denied=true;}Check(denied,"restricted connection table denial");
            }
            await Exec(connection,"DROP USER Day50Connected");
            await Exec(master.ConnectionString,"DECLARE @sql nvarchar(max)=N'DROP LOGIN '+QUOTENAME(@name); EXEC(@sql);",new SqlParameter("@name",login));loginOwned=false;
            Console.WriteLine("PASS: separate SQL-auth restricted connection real EF read/insert/update/denial; temporary login removed; no authentication-mode change");
            // The backup contains production schema/view and known rows, not test-only objects/identities.
            await Exec(connection,"DROP PROCEDURE dbo.Day50Report; DROP FUNCTION dbo.Day50ReportFunction; DROP TABLE dbo.Day50Input; DROP TABLE dbo.Day50Audit; DROP USER Day50ReportUser; DROP USER Day50RuntimeUser; DROP ROLE Day50Report; DROP ROLE Day50CatalogRuntime;");
            await Exec(master.ConnectionString,$"BACKUP DATABASE [{source.InitialCatalog}] TO DISK=@file WITH COPY_ONLY,CHECKSUM",new SqlParameter("@file",backup));
            await Exec(master.ConnectionString,"RESTORE VERIFYONLY FROM DISK=@file WITH CHECKSUM",new SqlParameter("@file",backup));
            var logicals=new List<string>();
            await using(var c=new SqlConnection(master.ConnectionString))
            {
                await c.OpenAsync();using var cmd=c.CreateCommand();cmd.CommandText="RESTORE FILELISTONLY FROM DISK=@file";cmd.Parameters.AddWithValue("@file",backup);
                await using var r=await cmd.ExecuteReaderAsync();while(await r.ReadAsync()){Check(r.GetString(r.GetOrdinal("Type")) is "D" or "L","supported ordinary SQL file");logicals.Add(r.GetString(r.GetOrdinal("LogicalName")));}
            }
            Check(logicals.Count>=2,"all backup files enumerated");
            var parameters=new List<SqlParameter>{new SqlParameter("@file",backup)};var moves=new List<string>();
            for(int i=0;i<logicals.Count;i++){var path=Path.Combine(directory,$"restored-{i}.sqlfile");Check(!File.Exists(path),"new restore path");ownedFiles.Add(path);parameters.Add(new SqlParameter("@logical"+i,logicals[i]));parameters.Add(new SqlParameter("@path"+i,path));moves.Add($"MOVE @logical{i} TO @path{i}");}
            await Exec(connection,"UPDATE dbo.Courses SET Name=@name WHERE Id=@id",new SqlParameter("@name","After backup"),new SqlParameter("@id",course.Id));
            targetOwned=true;
            await Exec(master.ConnectionString,$"RESTORE DATABASE [{restoredName}] FROM DISK=@file WITH CHECKSUM, "+string.Join(",",moves),parameters.ToArray());
            await using(var c=new SqlConnection(target.ConnectionString))
            {await c.OpenAsync();using var cmd=c.CreateCommand();cmd.CommandText="DBCC CHECKDB WITH TABLERESULTS, NO_INFOMSGS";cmd.CommandTimeout=120;await using var r=await cmd.ExecuteReaderAsync();do{Check(!await r.ReadAsync(),"CHECKDB no errors");}while(await r.NextResultAsync());}
            await Verify(target.ConnectionString,"Before backup");await Verify(connection,"After backup");
            Console.WriteLine($"PASS: COPY_ONLY CHECKSUM / VERIFYONLY / real RESTORE all {logicals.Count} files MOVE / CHECKDB / history / view / application read; source newer, restored backup value");
        }
        catch(Exception ex){failure=ex;throw;}
        finally
        {
            bool cleanupFailed=false;
            if(loginOwned)try{await Exec(master.ConnectionString,"DECLARE @sql nvarchar(max)=N'DROP LOGIN '+QUOTENAME(@name); EXEC(@sql);",new SqlParameter("@name",login));}catch(Exception ex){cleanupFailed=true;Console.Error.WriteLine("CLEANUP owned login: "+ex.GetType().Name);}
            foreach(var pair in new[]{(source,sourceOwned),(target,targetOwned)})if(pair.Item2)
            {
                try
                {
                    using(var pool=new SqlConnection(pair.Item1.ConnectionString))SqlConnection.ClearPool(pool);
                    var name=pair.Item1.InitialCatalog;Check(Regex.IsMatch(name,@"^university_registration_tests_day(6|50)_[a-f0-9]{32}$"),"owned cleanup identifier");
                    await Exec(master.ConnectionString,$"IF DB_ID(@n) IS NOT NULL BEGIN IF NOT EXISTS(SELECT 1 FROM sys.databases WHERE name=@n AND create_date>=@start) THROW 51051,'Creation date mismatch',1; IF EXISTS(SELECT 1 FROM sys.dm_exec_sessions WHERE database_id=DB_ID(@n)) THROW 51052,'Active connection',1; DROP DATABASE [{name}]; END",new SqlParameter("@n",name),new SqlParameter("@start",earliest));
                }catch(Exception ex){cleanupFailed=true;Console.Error.WriteLine("CLEANUP owned DB: "+ex.GetType().Name+" "+ex.Message);}
            }
            if(!cleanupFailed)try{foreach(var file in ownedFiles)if(File.Exists(file))File.Delete(file);Directory.Delete(directory);}catch(Exception ex){cleanupFailed=true;Console.Error.WriteLine("CLEANUP owned files: "+ex.GetType().Name);}
            if(cleanupFailed){if(failure is null)throw new CleanupFailure();}else Console.WriteLine("PASS: both owned DBs and exact external backup/restore files removed; no forced disconnect");
        }
    }
}
