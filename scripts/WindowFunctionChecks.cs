using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using UniversityRegistration.Application;
using UniversityRegistration.Data;
using UniversityRegistration.Models;

internal static class WindowFunctionChecks
{
    static void Check(bool value, string label) { if (!value) throw new BehaviorFailure("Windows: " + label); }
    public static async Task Run(string root, Func<UniversityDbContext> factory, CatalogTestIdentity identity)
    {
        var tag="W46_"+Guid.NewGuid().ToString("N")[..8];
        var course=new Course{Code=tag,Name="Window fixture",Credits=3};
        var term=new AcademicTerm{Name=tag,StartsOn=new(2026,1,1),EndsOn=new(2026,6,1)};
        var students=Enumerable.Range(0,6).Select(i=>new Student{StudentNumber=tag+i,FullName="Window fixture"}).ToArray();
        var offerings=new List<CourseOffering>();var entries=new List<WaitlistEntry>();
        Exception? failure=null;
        string Path(int i,string suffix="")=>$"/students/{students[i].Id}/waitlist{suffix}";
        async Task<JsonNode> Get(HttpClient client,string path,int status=200)
        {
            using var response=await client.GetAsync(path);
            Check((int)response.StatusCode==status,"HTTPS expected "+status+" actual "+(int)response.StatusCode);
            if(status==200)Check(response.Headers.CacheControl?.NoStore==true,"private response no-store");
            return JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        }
        async Task<string> Snapshot()
        {
            await using var db=factory();var oids=offerings.Select(o=>o.Id).ToArray();var sids=students.Select(s=>s.Id).ToArray();
            return JsonSerializer.Serialize(new {
                Entries=await db.WaitlistEntries.AsNoTracking().Where(w=>oids.Contains(w.CourseOfferingId)).OrderBy(w=>w.Id).ToArrayAsync(),
                Offerings=await db.CourseOfferings.AsNoTracking().Where(o=>oids.Contains(o.Id)).OrderBy(o=>o.Id).ToArrayAsync(),
                Students=await db.Students.AsNoTracking().Where(s=>sids.Contains(s.Id)).OrderBy(s=>s.Id).ToArrayAsync(),
                Enrollments=await db.Enrollments.AsNoTracking().Where(e=>oids.Contains(e.CourseOfferingId)).OrderBy(e=>e.Id).ToArrayAsync(),
                Slots=await db.CourseOfferingTimeSlots.AsNoTracking().Where(s=>oids.Contains(s.CourseOfferingId)).OrderBy(s=>s.Id).ToArrayAsync()
            });
        }
        async Task Add(int student,int offering,WaitlistStatus status)
        {
            var entry=new WaitlistEntry{StudentId=students[student].Id,CourseOfferingId=offerings[offering].Id,JoinedAtUtc=DateTimeOffset.UtcNow};
            entries.Add(entry);await using var db=factory();db.Add(entry);await db.SaveChangesAsync();
            // Read-fixture closed states, not a placement/lifecycle experiment.
            if(status!=WaitlistStatus.Waiting)
                await db.WaitlistEntries.Where(w=>w.Id==entry.Id).ExecuteUpdateAsync(s=>s
                    .SetProperty(w=>w.Status,status).SetProperty(w=>w.ClosedAtUtc,entry.JoinedAtUtc).SetProperty(w=>w.ClosureReason,"fixture"));
        }
        try
        {
            await using(var db=factory()){db.AddRange(students);db.AddRange(course,term);await db.SaveChangesAsync();}
            offerings.AddRange(Enumerable.Range(0,2).Select(i=>new CourseOffering{CourseId=course.Id,AcademicTermId=term.Id,SectionCode="S"+i,Capacity=3}));
            await using(var db=factory())
            {
                db.AddRange(offerings);await db.SaveChangesAsync();
                db.AddRange(Enumerable.Range(0,3).Select(i=>new CourseOfferingTimeSlot{CourseOfferingId=offerings[0].Id,
                    Day=DayOfWeek.Monday,StartsAt=new(8+i,0),EndsAt=new(9+i,0)}));await db.SaveChangesAsync();
            }
            await Add(0,0,WaitlistStatus.Waiting);await Add(1,0,WaitlistStatus.Cancelled);
            await Add(2,0,WaitlistStatus.Waiting);await Add(3,0,WaitlistStatus.Placed);
            await Add(4,0,WaitlistStatus.Ineligible);await Add(5,0,WaitlistStatus.Waiting);
            await Add(2,1,WaitlistStatus.Waiting);await Add(0,1,WaitlistStatus.Waiting);await Add(5,1,WaitlistStatus.Waiting);
            var target=await identity.ForRequestAsync("GET",Path(2),null,null);
            var other=await identity.ForRequestAsync("GET",Path(0),null,null);
            var closed=await identity.ForRequestAsync("GET",Path(1),null,null);
            var before=await Snapshot();
            var items=(await Get(target,Path(2)))["items"]!.AsArray();
            Check(items.Count==2,"two own Waiting entries only");
            Check(items[0]!["id"]!.GetValue<long>()==entries[2].Id && items[1]!["id"]!.GetValue<long>()==entries[6].Id,"correct identities/order");
            Check(items[0]!["position"]!.GetValue<long>()==2 && items[1]!["position"]!.GetValue<long>()==1,"student filter after windows, independent partitions");
            Check(items.All(n=>n!["totalWaiting"]!.GetValue<long>()==3),"COUNT_BIG partition total excludes closed and slots");
            Check(items.All(n=>n!.AsObject().Select(p=>p.Key).Order().SequenceEqual(new[]{"id","courseOfferingId","queueNumber","joinedAtUtc","position","totalWaiting"}.Order())),"minimal fields no other student data");
            var page=(await Get(target,Path(2,"?page=2&pageSize=1")))["items"]!.AsArray();
            Check(page.Count==1 && JsonNode.DeepEquals(page[0],items[1]),"paging after global position");
            Check((await Get(target,Path(2,"?page=3&pageSize=1")))["items"]!.AsArray().Count==0,"past last page");
            Check((await Get(closed,Path(1)))["items"]!.AsArray().Count==0,"cancelled-only student empty");
            await Get(target,Path(0),404);await Get(other,Path(2),404);
            using(var anon=identity.AnonymousClient())await Get(anon,Path(2),401);
            await Get(identity.Client,Path(2),403);
            foreach(var suffix in new[]{"?page=0","?pageSize=101","?pageSize=0","?page=2147483647&pageSize=100"})
                await Get(target,Path(2,suffix),400);
            var single=await Get(target,Path(2,"/"+entries[2].Id));
            Check(single["position"]!.GetValue<long>()==2 && single["status"]!.GetValue<string>()=="Waiting"
                && single["totalWaiting"] is null,"single response contract retained");
            Check(entries[2].QueueNumber>entries[0].QueueNumber+1,"queue gap fixture");
            await using(var template=factory())
            {
                var capture=new CatalogSqlCapture();
                await using var db=new UniversityDbContext(new DbContextOptionsBuilder<UniversityDbContext>()
                    .UseSqlServer(template.Database.GetConnectionString()).AddInterceptors(capture).Options);
                var allPositions=new List<WaitingPosition>();
                foreach(var i in new[]{0,2,5})
                {
                    capture.Commands.Clear();
                    allPositions.AddRange(await WaitlistPositions.ReadAsync(db,students[i].Id,1,100,default));
                    Check(capture.Commands.Count==1,"one SQL batch for two offerings");
                }
                foreach(var o in offerings)
                    Check(allPositions.Where(p=>p.CourseOfferingId==o.Id).OrderBy(p=>p.QueueNumber).Select(p=>p.Position).SequenceEqual(new long[]{1,2,3}),"continuous positions despite gaps");
                var command=capture.Commands.Single();
                Check(command.Text.Contains("ROW_NUMBER()") && command.Text.Contains("COUNT_BIG(*) OVER")
                    && command.Text.Contains("PARTITION BY") && command.Text.Contains("OFFSET") && !command.Text.Contains("TimeSlots"),"real window SQL without multiplicative join");
                Check(command.Parameters.Any(p=>p is int n && n==students[5].Id),"student parameter bound");
                capture.Commands.Clear();await WaitlistPositions.ReadAsync(db,students[2].Id,2,1,default);
                Check(capture.Commands.Count==1 && !db.ChangeTracker.Entries().Any(),"one query for one row, unmapped DTO no tracking");
                capture.Commands.Clear();
                try {await WaitlistPositions.ReadAsync(db,students[2].Id,0,10,default);Check(false,"direct invalid accepted");}catch(ArgumentOutOfRangeException){}
                Check(capture.Commands.Count==0,"invalid direct read no SQL");
            }

            // Executable SQL learning fixture. SQL types and every expected boundary are checked.
            await using(var db=factory())
            {
                await db.Database.OpenConnectionAsync();await using var cmd=db.Database.GetDbConnection().CreateCommand();
                cmd.CommandText=await File.ReadAllTextAsync(System.IO.Path.Combine(root,"sql/app/window-functions-example.sql"));
                await using var reader=await cmd.ExecuteReaderAsync();
                long[] rowNumber={1,2,3,4,1,2},rank={1,1,3,4,1,2},dense={1,1,2,3,1,2};
                long[] rowsTotal={10,30,60,100,5,12},rangeTotal={30,30,60,100,5,12};
                int?[] previous={null,10,20,30,null,5},next={20,30,40,null,7,null};
                var i=0;
                while(await reader.ReadAsync())
                {
                    Check(i<6 && reader.GetInt32(0)==i+1,"fixture deterministic order");
                    Check(reader.GetInt64(4)==rowNumber[i] && reader.GetInt64(5)==rank[i] && reader.GetInt64(6)==dense[i],"ranking bigint and ties");
                    Check(reader.GetInt64(7)==rowsTotal[i] && reader.GetInt64(8)==rangeTotal[i],"ROWS vs RANGE");
                    Check((reader.IsDBNull(9)?(int?)null:reader.GetInt32(9))==previous[i]
                        && (reader.IsDBNull(10)?(int?)null:reader.GetInt32(10))==next[i],"LAG/LEAD NULL boundaries");
                    i++;
                }
                Check(i==6,"six learning rows");
            }
            Check(before==await Snapshot(),"fresh-context fixture unchanged");
            Console.WriteLine("PASS: windows two partitions/positions1,2,3/own2,1/totals3/gaps/closed states/paging; HTTPS access/no-store; one parameterized untracked SQL; rank/dense/frame/LAG/LEAD exact results; no writes");
        }
        catch(Exception ex){failure=ex;throw;}
        finally
        {
            try
            {
                await using var db=factory();var oids=offerings.Select(o=>o.Id).ToArray();var sids=students.Select(s=>s.Id).ToArray();
                await db.WaitlistEntries.Where(w=>oids.Contains(w.CourseOfferingId)).ExecuteDeleteAsync();
                await db.CourseOfferingTimeSlots.Where(s=>oids.Contains(s.CourseOfferingId)).ExecuteDeleteAsync();
                await db.Students.Where(s=>sids.Contains(s.Id)).ExecuteDeleteAsync();
                await db.CourseOfferings.Where(o=>oids.Contains(o.Id)).ExecuteDeleteAsync();
                await db.Courses.Where(c=>c.Id==course.Id).ExecuteDeleteAsync();await db.AcademicTerms.Where(t=>t.Id==term.Id).ExecuteDeleteAsync();
                Console.WriteLine("PASS: window fixtures cleaned");
            }
            catch(Exception ex){Console.Error.WriteLine("CLEANUP windows: "+ex.GetType().Name);if(failure is null)throw new CleanupFailure();}
        }
    }
}
