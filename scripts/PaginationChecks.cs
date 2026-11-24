using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using UniversityRegistration.Application;
using UniversityRegistration.Contracts;
using UniversityRegistration.Data;
using UniversityRegistration.Models;

internal static class PaginationChecks
{
    static void Check(bool ok,string message){if(!ok)throw new BehaviorFailure("Pagination: "+message);}
    sealed class SqlCapture : Microsoft.EntityFrameworkCore.Diagnostics.DbCommandInterceptor
    {
        public string Sql="";public SqlParameter[] Parameters=[];
        public override ValueTask<System.Data.Common.DbDataReader> ReaderExecutedAsync(System.Data.Common.DbCommand command,Microsoft.EntityFrameworkCore.Diagnostics.CommandExecutedEventData data,System.Data.Common.DbDataReader result,CancellationToken ct=default)
        {Sql=command.CommandText;Parameters=command.Parameters.Cast<SqlParameter>().Select(p=>(SqlParameter)((ICloneable)p).Clone()).ToArray();return ValueTask.FromResult(result);}
    }
    const string ReportSql="""
        SELECT o.Id AS OfferingId,c.Code AS CourseCode,o.Capacity,
        COALESCE(a.ActiveCount,0) AS ActiveCount,o.OccupiedSeats,
        100.0*COALESCE(a.ActiveCount,0)/o.Capacity AS OccupancyPercent
        FROM CourseOfferings o JOIN Courses c ON c.Id=o.CourseId
        LEFT JOIN (SELECT CourseOfferingId,COUNT(*) AS ActiveCount FROM Enrollments
        WHERE Status IN (1,2) GROUP BY CourseOfferingId) a ON a.CourseOfferingId=o.Id
        WHERE o.AcademicTermId=@term ORDER BY o.Id OFFSET 0 ROWS FETCH NEXT 100 ROWS ONLY
        """;
    public static async Task Run(Func<UniversityDbContext> factory,CatalogTestIdentity identity)
    {
        var courses=Enumerable.Range(0,6000).Select(i=>new Course{Code=$"P49_{i:00000}",Name="Pagination49",Credits=i%2+3}).ToList();
        var students=Enumerable.Range(0,3).Select(i=>new Student{StudentNumber="P49_"+i,FullName="Synthetic"}).ToArray();
        var term=new AcademicTerm{Name="P49",StartsOn=new(2026,1,1),EndsOn=new(2026,6,1)};
        var offers=new List<CourseOffering>();Exception? failure=null;
        await using var setup=factory();var connection=setup.Database.GetConnectionString()!;var actual=new SqlCapture();
        async Task<JsonNode> Get(string query,int status=200)
        {
            using var r=await identity.Client.GetAsync("/courses/catalog/keyset"+query);
            Check((int)r.StatusCode==status,"HTTPS "+query+" status "+(int)r.StatusCode);
            return status==200?JsonNode.Parse(await r.Content.ReadAsStringAsync())!:new JsonObject();
        }
        async Task Measure(string label,Func<Task> action,SqlConnection c)
        {
            var io=new List<string>();SqlInfoMessageEventHandler handler=(_,e)=>io.Add(e.Message);
            c.InfoMessage+=handler;try
            {
                using(var cmd=c.CreateCommand()){cmd.CommandText="SET STATISTICS IO ON; SET STATISTICS TIME ON";await cmd.ExecuteNonQueryAsync();}
                var watch=Stopwatch.StartNew();await action();watch.Stop();
                Console.WriteLine($"MEASURE {label} complete-ms={watch.Elapsed.TotalMilliseconds:F2}");
                                if(!label.StartsWith("report-dapper"))
                {
                    // EF may close its reader before informational result messages are consumed.
                    // Replay the completed command/typed parameters, drain all results for SQL IO.
                    io.Clear();using var replay=c.CreateCommand();replay.CommandTimeout=30;
                    replay.CommandText="SET STATISTICS XML ON;\n"+actual.Sql;
                    replay.Parameters.AddRange(actual.Parameters.Select(p=>(SqlParameter)((ICloneable)p).Clone()).ToArray());
                    await using(var r=await replay.ExecuteReaderAsync())
                    {do{while(await r.ReadAsync())if(r.FieldCount==1&&r.GetName(0).Contains("Showplan"))
                        {var plan=System.Xml.Linq.XDocument.Parse(r.GetString(0));System.Xml.Linq.XNamespace ns="http://schemas.microsoft.com/sqlserver/2004/07/showplan";
                        Check(plan.Descendants(ns+"RunTimeCountersPerThread").Any(),"actual plan");
                        Console.WriteLine("PLAN "+label+" "+string.Join(",",plan.Descendants(ns+"RelOp").Select(x=>x.Attribute("PhysicalOp")?.Value).Distinct()));}
                    }while(await r.NextResultAsync());}
                    using var off=c.CreateCommand();off.CommandText="SET STATISTICS XML OFF";await off.ExecuteNonQueryAsync();
                }
                foreach(var line in io.Where(x=>x.Contains("logical reads")||x.Contains("CPU time")))Console.WriteLine(line);
            }finally{c.InfoMessage-=handler;}
        }
        try
        {
            setup.AddRange(courses);setup.AddRange(students);setup.Add(term);await setup.SaveChangesAsync();
            // A known hole: cursor need not identify an existing row.
            int hole=courses[3].Id;await setup.Courses.Where(c=>c.Id==hole).ExecuteDeleteAsync();courses.RemoveAt(3);
            var catalogBefore=JsonSerializer.Serialize(await setup.Courses.AsNoTracking().OrderBy(c=>c.Id).ToArrayAsync());
            var first=await Get("?search=%20P49_%20&pageSize=3");
            Check(first["items"]!.AsArray().Select(x=>x!["id"]!.GetValue<int>()).SequenceEqual(courses.Take(3).Select(c=>c.Id)),"first page");
            Check(first.AsObject().Select(p=>p.Key).Order().SequenceEqual(new[]{"hasMore","items","nextAfterId"}),"keyset envelope");
            Check(first["hasMore"]!.GetValue<bool>()&&first["nextAfterId"]!.GetValue<int>()==courses[2].Id,"extra row omitted/cursor");
            var gap=await Get($"?search=P49_&afterId={hole}&pageSize=1");Check(gap["items"]![0]!["id"]!.GetValue<int>()==courses[3].Id,"deleted cursor accepted");
            var last=await Get($"?search=P49_&afterId={courses[^2].Id}");Check(last["items"]!.AsArray().Count==1&&!last["hasMore"]!.GetValue<bool>()&&last["nextAfterId"] is null,"last");
            var empty=await Get($"?afterId={int.MaxValue}");Check(empty["items"]!.AsArray().Count==0&&empty["nextAfterId"] is null,"empty");
            foreach(var q in new[]{"?afterId=","?afterId=%20","?afterId=0","?afterId=-1","?afterId=x","?afterId=2147483648","?pageSize=0","?pageSize=101","?credits=0"})await Get(q,400);
            var filtered=await Get("?search=P49_&credits=4&pageSize=100");Check(filtered["items"]!.AsArray().Select(x=>x!["id"]!.GetValue<int>()).SequenceEqual(courses.Where(c=>c.Credits==4).Take(100).Select(c=>c.Id)),"combined filters");
            await using(var db=factory())
            {
                var reader=new CourseCatalogReader(db);int? after=null;int count=0,previous=0;long sum=0;
                do{var page=await reader.ReadKeysetAsync(new("P49_",null,after,73),default);
                    foreach(var row in page.Items){Check(row.Id>previous,"strict traversal order/no duplicates");previous=row.Id;sum+=row.Id;count++;}
                    after=page.NextAfterId;if(!page.HasMore)break;
                }while(true);
                Check(count==courses.Count&&sum==courses.Sum(c=>(long)c.Id)&&previous==courses[^1].Id,"bounded traversal count/checksum");
                using var cancel=new CancellationTokenSource();cancel.Cancel();bool cancelled=false;
                try{await reader.ReadKeysetAsync(new(),cancel.Token);}catch(OperationCanceledException){cancelled=true;}Check(cancelled,"cancellation");
                Check(!db.ChangeTracker.Entries().Any(),"no tracking");
            }
            // Boundary comes from ordinal lookup, not Id=offset; preparation cost is outside measurements.
            int offset=5000;int boundary=courses[offset-1].Id;
            var expected=courses.Skip(offset).Take(20).Select(c=>c.Id).ToArray();
            var capture=new CatalogSqlCapture();
            await using(var db=new UniversityDbContext(new DbContextOptionsBuilder<UniversityDbContext>().UseSqlServer(connection,o=>o.CommandTimeout(30)).AddInterceptors(capture,actual).Options))
            {
                await db.Database.OpenConnectionAsync();var c=(SqlConnection)db.Database.GetDbConnection();
                async Task Offset(){var ids=await db.Courses.Where(x=>x.Code.Contains("P49_")).OrderBy(x=>x.Id).Skip(offset).Take(20).Select(x=>new CourseListItemDto(x.Id,x.Code,x.Name)).ToListAsync();Check(ids.Select(x=>x.Id).SequenceEqual(expected),"deep offset");}
                async Task Keyset(){var page=await new CourseCatalogReader(db).ReadKeysetAsync(new("P49_",null,boundary,20),default);Check(page.Items.Select(x=>x.Id).SequenceEqual(expected),"equivalent keyset");}
                await Offset();await Keyset();
                for(int i=0;i<3;i++){await Measure("offset-"+i,Offset,c);await Measure("keyset-"+i,Keyset,c);}
                Check(capture.Commands.Any(x=>x.Text.Contains("OFFSET"))&&capture.Commands.Any(x=>x.Text.Contains("TOP")&&x.Text.Contains(">")),"executed SQL shapes");
                foreach(var sql in capture.Commands.Select(x=>x.Text).Distinct())Console.WriteLine("SQL "+sql);
            }
            await using(var verify=factory())Check(catalogBefore==JsonSerializer.Serialize(await verify.Courses.AsNoTracking().OrderBy(c=>c.Id).ToArrayAsync()),"catalog reads unchanged");
            // Controlled deletion before cursor makes offset skip a surviving row. Keyset continues.
            int cursor=courses[2].Id;await setup.Courses.Where(c=>c.Id==courses[0].Id).ExecuteDeleteAsync();courses.RemoveAt(0);
            await using(var db=factory())
            {
                var off=await new CourseCatalogReader(db).ReadAsync(new("P49_",null,2,3),default);
                var key=await new CourseCatalogReader(db).ReadKeysetAsync(new("P49_",null,cursor,3),default);
                Check(key.Items[0].Id==courses[2].Id&&off.Items[0].Id==courses[3].Id,"deletion shifts offset, keyset stable boundary");
            }
            var added=new Course{Code="P49_NEW",Name="Pagination49",Credits=3};setup.Add(added);await setup.SaveChangesAsync();courses.Add(added);
            await using(var db=factory()){var page=await new CourseCatalogReader(db).ReadKeysetAsync(new("P49_",null,courses[^2].Id,3),default);Check(page.Items.Single().Id==added.Id,"later insert visible, not snapshot");var offsetTail=await db.Courses.Where(c=>c.Code.Contains("P49_")).OrderBy(c=>c.Id).Skip(courses.Count-1).Take(3).Select(c=>c.Id).ToArrayAsync();Check(offsetTail.Single()==added.Id,"append visible to offset too");}
            offers.AddRange(Enumerable.Range(0,2).Select(i=>new CourseOffering{CourseId=courses[i].Id,AcademicTermId=term.Id,SectionCode="P49",Capacity=4}));
            setup.AddRange(offers);await setup.SaveChangesAsync();
            var a=new Enrollment{StudentId=students[0].Id,CourseOfferingId=offers[0].Id,CreatedAtUtc=DateTimeOffset.UtcNow};a.TrySubmit();
            var b=new Enrollment{StudentId=students[1].Id,CourseOfferingId=offers[0].Id,CreatedAtUtc=DateTimeOffset.UtcNow};b.TrySubmit();b.TryApprove();
            setup.AddRange(a,b,new Enrollment{StudentId=students[2].Id,CourseOfferingId=offers[1].Id,CreatedAtUtc=DateTimeOffset.UtcNow});
            setup.AddRange(Enumerable.Range(0,2).Select(i=>new CourseOfferingTimeSlot{CourseOfferingId=offers[0].Id,Day=DayOfWeek.Monday,StartsAt=new(8+i,0),EndsAt=new(9+i,0)}));
            await SeatCapacityChecks.SaveFixtureAsync(setup);
            var wanted=new[]{new OfferingOccupancy(offers[0].Id,courses[0].Code,4,2,2,50m),new OfferingOccupancy(offers[1].Id,courses[1].Code,4,0,0,0m)};
            async Task<string> Snapshot(){await using var d=factory();return JsonSerializer.Serialize(new{Courses=await d.Courses.AsNoTracking().OrderBy(x=>x.Id).ToArrayAsync(),Enrollments=await d.Enrollments.AsNoTracking().OrderBy(x=>x.Id).ToArrayAsync(),Offers=await d.CourseOfferings.AsNoTracking().OrderBy(x=>x.Id).ToArrayAsync()});}
            var before=await Snapshot();
            using var reportTimeout=new CancellationTokenSource(TimeSpan.FromMinutes(2));var reportCt=reportTimeout.Token;
            await using(var db=new UniversityDbContext(new DbContextOptionsBuilder<UniversityDbContext>().UseSqlServer(connection).AddInterceptors(actual).Options))
            {
                db.Database.SetCommandTimeout(30);await db.Database.OpenConnectionAsync();var c=(SqlConnection)db.Database.GetDbConnection();
                async Task Linq(){Check((await new RegistrationReports(db).OccupancyAsync(term.Id,1,100,reportCt)).Items.SequenceEqual(wanted),"LINQ independent expected");}
                async Task Raw(){Check((await db.Database.SqlQueryRaw<OfferingOccupancy>(ReportSql,new SqlParameter("@term",term.Id)).ToListAsync(reportCt)).SequenceEqual(wanted),"EF raw independent expected");}
                Check((await new RegistrationReports(db).OccupancyAsync(int.MaxValue,1,100,reportCt)).Items.Count==0,"empty LINQ report");
                Check((await db.Database.SqlQueryRaw<OfferingOccupancy>(ReportSql,new SqlParameter("@term",int.MaxValue)).ToListAsync(reportCt)).Count==0,"empty raw report");
                await Linq();await Raw();for(int i=0;i<3;i++){await Measure("report-linq-"+i,Linq,c);await Measure("report-raw-"+i,Raw,c);}
                Check(!db.ChangeTracker.Entries().Any(),"report untracked");
            }
            await using(var c=new SqlConnection(connection))
            {
                await c.OpenAsync();async Task Read(){var rows=(await c.QueryAsync<OfferingOccupancy>(new CommandDefinition(ReportSql,new{term=term.Id},commandTimeout:30,flags:CommandFlags.Buffered,cancellationToken:reportCt))).ToArray();Check(rows.SequenceEqual(wanted),"Dapper independent expected");}
                Check(!(await c.QueryAsync<OfferingOccupancy>(new CommandDefinition(ReportSql,new{term=int.MaxValue},commandTimeout:30,cancellationToken:reportCt))).Any(),"empty Dapper report");
                await Read();for(int i=0;i<3;i++)await Measure("report-dapper-"+i,Read,c);
            }
            Check(before==await Snapshot(),"reads unchanged fresh context");
            Console.WriteLine("PASS: keyset HTTPS, bounded traversal, cancellation, mutation/deep-page and three independent report results");
        }
        catch(Exception ex){failure=ex;throw;}
        finally
        {
            try{await using var db=factory();var ids=offers.Select(x=>x.Id).ToArray();await db.Enrollments.Where(e=>ids.Contains(e.CourseOfferingId)).ExecuteDeleteAsync();await db.CourseOfferingTimeSlots.Where(s=>ids.Contains(s.CourseOfferingId)).ExecuteDeleteAsync();await db.CourseOfferings.Where(o=>ids.Contains(o.Id)).ExecuteDeleteAsync();await db.AcademicTerms.Where(t=>t.Id==term.Id).ExecuteDeleteAsync();var sids=students.Select(s=>s.Id).ToArray();await db.Students.Where(s=>sids.Contains(s.Id)).ExecuteDeleteAsync();await db.Courses.Where(c=>c.Code.StartsWith("P49_")).ExecuteDeleteAsync();Console.WriteLine("PASS: pagination fixtures cleaned");}
            catch(Exception ex){Console.Error.WriteLine("CLEANUP pagination "+ex.GetType().Name);if(failure is null)throw new CleanupFailure();}
        }
    }
}
