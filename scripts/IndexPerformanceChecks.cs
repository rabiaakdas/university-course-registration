using System.Data.Common;
using System.Xml.Linq;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using UniversityRegistration.Data;
using UniversityRegistration.Models;

internal static class IndexPerformanceChecks
{
    static void Check(bool ok,string message) { if(!ok) throw new BehaviorFailure("Indexes: "+message); }
    sealed class Capture : DbCommandInterceptor
    {
        public string Sql="";
        public SqlParameter[] Parameters=[];
        public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command,CommandExecutedEventData data,DbDataReader result,CancellationToken ct=default)
        {
            Sql=command.CommandText;
            Parameters=command.Parameters.Cast<SqlParameter>().Select(p=>(SqlParameter)((ICloneable)p).Clone()).ToArray();
            return ValueTask.FromResult(result);
        }
    }
    public static async Task Run(string root,Func<UniversityDbContext> factory,bool probe)
    {
        // Keep the bounded query aligned with the private placement page, without invoking writes.
        var source=await File.ReadAllTextAsync(Path.Combine(root,"Application","EnrollmentOperations.cs"));
        Check(source.Contains("w.Status == WaitlistStatus.Waiting && w.QueueNumber > after).OrderBy(w => w.QueueNumber)")
            && source.Contains(".Take(32).ToArrayAsync(ct)"),"production candidate query changed; review laboratory query");
        await using var setup=factory();await setup.Database.MigrateAsync();
        var connection=setup.Database.GetConnectionString()!;
        var course=new Course{Code="I48",Name="Index fixture",Credits=3};
        var term=new AcademicTerm{Name="Index48",StartsOn=new(2026,1,1),EndsOn=new(2026,6,1)};
        var students=Enumerable.Range(0,6000).Select(i=>new Student{StudentNumber="I48_"+i,FullName="Synthetic"}).ToArray();
        var offerings=new List<CourseOffering>();
        var expected=new List<(long Id,long Queue,int Offering,int Student,bool Waiting)>();
        var plans=Path.Combine(root,"obj","day48-plans",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(plans);
        Exception? failure=null;
        try
        {
            setup.AddRange(course,term);setup.AddRange(students);await setup.SaveChangesAsync();
            offerings.AddRange(Enumerable.Range(0,3).Select(i=>new CourseOffering{CourseId=course.Id,AcademicTermId=term.Id,SectionCode="S"+i,Capacity=1}));
            setup.AddRange(offerings);await setup.SaveChangesAsync();
            // Small partition, dense 6,000-row partition, mostly closed 6,000-row partition.
            for(int part=0;part<3;part++)
            {
                var rows=students.Take(part==0?4:6000).Select(s=>new WaitlistEntry{StudentId=s.Id,CourseOfferingId=offerings[part].Id,JoinedAtUtc=DateTimeOffset.UtcNow}).ToArray();
                setup.AddRange(rows);await setup.SaveChangesAsync();
                for(int i=0;i<rows.Length;i++) expected.Add((rows[i].Id,rows[i].QueueNumber,rows[i].CourseOfferingId,rows[i].StudentId,part!=2 || i%1000==0));
                if(part==2)
                {
                    var closed=rows.Where((_,i)=>i%1000!=0).Select(w=>w.Id).ToArray();
                    await setup.WaitlistEntries.Where(w=>closed.Contains(w.Id)).ExecuteUpdateAsync(s=>s.SetProperty(w=>w.Status,WaitlistStatus.Cancelled)
                        .SetProperty(w=>w.ClosedAtUtc,DateTimeOffset.UtcNow).SetProperty(w=>w.ClosureReason,"fixture"));
                }
                setup.ChangeTracker.Clear();
            }
            await setup.Database.ExecuteSqlRawAsync("UPDATE STATISTICS dbo.WaitlistEntries WITH FULLSCAN");
            await using(var c=new SqlConnection(connection))
            {
                await c.OpenAsync();using var cmd=c.CreateCommand();
                cmd.CommandText="SELECT i.name,i.is_unique,i.filter_definition,c.name,ic.key_ordinal,ic.is_included_column FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE i.object_id=OBJECT_ID(N'dbo.WaitlistEntries') ORDER BY i.index_id,ic.index_column_id";
                await using var r=await cmd.ExecuteReaderAsync();while(await r.ReadAsync())Console.WriteLine($"INDEX {r.GetString(0)} unique={r.GetBoolean(1)} filter={(r.IsDBNull(2)?"none":r.GetString(2))} column={r.GetString(3)} ordinal={r.GetByte(4)} include={r.GetBoolean(5)}");
            }
            var before=System.Text.Json.JsonSerializer.Serialize(await setup.WaitlistEntries.AsNoTracking().OrderBy(w=>w.Id).ToArrayAsync());
            foreach(var phase in new[]{"baseline","covering"})
            {
                if(phase=="covering")
                    await setup.Database.ExecuteSqlRawAsync("CREATE INDEX IX_WaitlistEntries_CourseOfferingId_Status_QueueNumber ON dbo.WaitlistEntries(CourseOfferingId,Status,QueueNumber) INCLUDE(StudentId,JoinedAtUtc,ClosedAtUtc,ClosureReason) WITH(DROP_EXISTING=ON)");
                foreach(var scenario in new[]{(Name:"small",Part:0,After:0L),(Name:"dense",Part:1,After:0L),(Name:"sparse",Part:2,After:0L),(Name:"tail",Part:1,After:expected.Where(e=>e.Offering==offerings[1].Id).OrderBy(e=>e.Queue).ElementAt(5997).Queue)})
                {
                    int offeringId=offerings[scenario.Part].Id;long after=scenario.After;
                    var capture=new Capture();
                    await using var db=new UniversityDbContext(new DbContextOptionsBuilder<UniversityDbContext>().UseSqlServer(connection).AddInterceptors(capture).Options);
                    var rows=await db.WaitlistEntries.AsNoTracking().Where(w=>w.CourseOfferingId==offeringId && w.Status==WaitlistStatus.Waiting && w.QueueNumber>after)
                        .OrderBy(w=>w.QueueNumber).Take(32).ToArrayAsync();
                    var wanted=expected.Where(e=>e.Offering==offeringId&&e.Waiting&&e.Queue>after).OrderBy(e=>e.Queue).Take(32).ToArray();
                    Check(rows.Select(w=>w.Id).SequenceEqual(wanted.Select(e=>e.Id))&&rows.Select(w=>w.StudentId).SequenceEqual(wanted.Select(e=>e.Student)),"independent FIFO/content "+scenario.Name);
                    Check(db.ChangeTracker.Entries().Count()==0,"untracked");
                    if(phase=="baseline"&&scenario.Name=="small")
                    {
                        Console.WriteLine("EF SQL: "+capture.Sql);
                        Console.WriteLine("PARAMETERS: "+string.Join(", ",capture.Parameters.Select(p=>$"{p.ParameterName} {p.SqlDbType}={p.Value}")));
                    }
                    // Warm up first; three measurements, no cache flushing or index hints.
                    for(int run=-1;run<3;run++)
                    {
                        await using var c=new SqlConnection(connection);await c.OpenAsync();
                        var messages=new List<string>();c.InfoMessage+=(_,e)=>messages.Add(e.Message);
                        using var cmd=c.CreateCommand();cmd.CommandTimeout=60;
                        cmd.CommandText="SET STATISTICS IO ON; SET STATISTICS TIME ON; SET STATISTICS XML ON;\n"+capture.Sql;
                        cmd.Parameters.AddRange(capture.Parameters.Select(p=>(SqlParameter)((ICloneable)p).Clone()).ToArray());
                        var ids=new List<long>();string? plan=null;
                        await using(var r=await cmd.ExecuteReaderAsync())
                        {
                            do
                            {
                                if(r.FieldCount==1 && r.GetName(0).Contains("Showplan",StringComparison.OrdinalIgnoreCase))
                                { if(await r.ReadAsync())plan=r.GetString(0); }
                                else while(await r.ReadAsync())ids.Add(r.GetInt64(r.GetOrdinal("Id")));
                            }while(await r.NextResultAsync());
                        }
                        Check(ids.SequenceEqual(wanted.Select(e=>e.Id)),"measured result");
                        Check(plan is not null,"actual plan captured");
                        var xml=XDocument.Parse(plan!);XNamespace ns="http://schemas.microsoft.com/sqlserver/2004/07/showplan";
                        Check(xml.Descendants(ns+"RunTimeCountersPerThread").Any(),"actual not estimated plan");
                        if(run<0)continue;
                        if(run==0)await File.WriteAllTextAsync(Path.Combine(plans,$"{phase}-{scenario.Name}.sqlplan"),plan);
                        Console.WriteLine($"MEASURE {phase}/{scenario.Name}/{run+1} rows={ids.Count}");
                        foreach(var msg in messages)Console.WriteLine(msg.Trim());
                        if(run==0)Console.WriteLine("PLAN INDEXES "+string.Join(",",xml.Descendants(ns+"Object").Select(o=>o.Attribute("Index")?.Value).Distinct()));
                        if(run==0)foreach(var op in xml.Descendants(ns+"RelOp"))
                            Console.WriteLine($"PLAN {op.Attribute("PhysicalOp")?.Value}/{op.Attribute("LogicalOp")?.Value} estimated={op.Attribute("EstimateRows")?.Value} actual={string.Join(",",op.Descendants(ns+"RunTimeCountersPerThread").Take(1).Select(x=>x.Attribute("ActualRows")?.Value))}");
                    }
                }
            }
            await using(var verify=factory())
            {
                Check(before==System.Text.Json.JsonSerializer.Serialize(await verify.WaitlistEntries.AsNoTracking().OrderBy(w=>w.Id).ToArrayAsync()),"fresh-context all fields unchanged");
                Check(await verify.WaitlistEntries.CountAsync()==expected.Count,"no read inserts/deletes");
                Check(await verify.WaitlistEntries.CountAsync(w=>w.Status==WaitlistStatus.Waiting)==expected.Count(e=>e.Waiting),"no state writes");
                Check(!await verify.Enrollments.AnyAsync()&&await verify.CourseOfferings.AllAsync(o=>o.OccupiedSeats==0),"no enrollment/seat writes");
                Check(!verify.Database.HasPendingModelChanges(),"model alignment");
            }
            if(probe)Check(false,"controlled failure");
            Console.WriteLine("PASS: parameterized EF candidate page; independent small/dense/sparse/tail FIFO, actual plan/IO/TIME; no production changes");
        }
        catch(Exception ex){failure=ex;throw;}
        finally
        {
            try
            {
                // This database is freshly created by the runner and belongs exclusively to this mode.
                await setup.Database.ExecuteSqlRawAsync("CREATE INDEX IX_WaitlistEntries_CourseOfferingId_Status_QueueNumber ON dbo.WaitlistEntries(CourseOfferingId,Status,QueueNumber) WITH(DROP_EXISTING=ON)");
                await setup.WaitlistEntries.ExecuteDeleteAsync();await setup.CourseOfferings.ExecuteDeleteAsync();
                await setup.Students.ExecuteDeleteAsync();await setup.Courses.ExecuteDeleteAsync();await setup.AcademicTerms.ExecuteDeleteAsync();
                foreach(var file in Directory.GetFiles(plans,"*.sqlplan"))File.Delete(file);
                Directory.Delete(plans);
                Console.WriteLine("PASS: fixture rows and temporary plans removed; original index restored");
            }
            catch(Exception ex){Console.Error.WriteLine("CLEANUP index-performance: "+ex.GetType().Name);if(failure is null)throw new CleanupFailure();}
        }
    }
}
