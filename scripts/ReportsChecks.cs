using System.Data;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using UniversityRegistration.Application;
using UniversityRegistration.Accounts;
using UniversityRegistration.Data;
using UniversityRegistration.Models;

internal static class ReportsChecks
{
    static void Check(bool value, string label) { if (!value) throw new BehaviorFailure("Reports: " + label); }
    public static async Task Run(string root, Func<UniversityDbContext> factory, CatalogTestIdentity identity)
    {
        var tag = "R45_" + Guid.NewGuid().ToString("N")[..8];
        var students = Enumerable.Range(0,3).Select(i => new Student { StudentNumber=tag+i, FullName="Report fixture" }).ToArray();
        var terms = Enumerable.Range(0,2).Select(i => new AcademicTerm { Name=tag+i, StartsOn=new(2026,1,1), EndsOn=new(2026,6,1) }).ToArray();
        var courses = Enumerable.Range(0,2).Select(i => new Course { Code=tag+i, Name="Report course", Credits=3 }).ToArray();
        var offerings = new List<CourseOffering>();
        Exception? failure = null;
        string Summary(int i) => $"/students/{students[i].Id}/terms/{terms[0].Id}/registration-summary";
        string Report(string name, string query="") => $"/reports/terms/{terms[0].Id}/{name}{query}";
        async Task<JsonNode> Get(HttpClient client, string path, int status=200)
        {
            using var response = await client.GetAsync(path);
            Check((int)response.StatusCode==status, $"HTTP {path.Split('?')[0]} expected {status}, actual {(int)response.StatusCode}");
            if(status==200) Check(response.Headers.CacheControl?.NoStore==true,"personal/management no-store");
            return JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        }
        async Task<string> Snapshot()
        {
            await using var db=factory();var ids=offerings.Select(o=>o.Id).ToArray();var sids=students.Select(s=>s.Id).ToArray();
            return JsonSerializer.Serialize(new {
                Students=await db.Students.AsNoTracking().Where(s=>sids.Contains(s.Id)).OrderBy(s=>s.Id).ToArrayAsync(),
                Courses=await db.Courses.AsNoTracking().Where(c=>c.Code.StartsWith(tag)).OrderBy(c=>c.Id).ToArrayAsync(),
                Offerings=await db.CourseOfferings.AsNoTracking().Where(o=>ids.Contains(o.Id)).OrderBy(o=>o.Id).ToArrayAsync(),
                Enrollments=await db.Enrollments.AsNoTracking().Where(e=>ids.Contains(e.CourseOfferingId)).OrderBy(e=>e.Id).ToArrayAsync(),
                Slots=await db.CourseOfferingTimeSlots.AsNoTracking().Where(s=>ids.Contains(s.CourseOfferingId)).OrderBy(s=>s.Id).ToArrayAsync()
            });
        }
        async Task<List<object?[]>> SqlExample(string file, int student=0, int minimum=1)
        {
            await using var db=factory();await db.Database.OpenConnectionAsync();
            await using var cmd=db.Database.GetDbConnection().CreateCommand();
            cmd.CommandText=await File.ReadAllTextAsync(Path.Combine(root,"sql/app",file));
            foreach(var (name,value) in new[]{("@termId",terms[0].Id),("@studentId",students[student].Id),("@offset",0),("@pageSize",100),("@minimumCount",minimum)})
                cmd.Parameters.Add(new SqlParameter(name,SqlDbType.Int){Value=value});
            var result=new List<object?[]>();await using var reader=await cmd.ExecuteReaderAsync();
            while(await reader.ReadAsync()) {var row=new object[reader.FieldCount];reader.GetValues(row);result.Add(row.Select(v=>v is DBNull?null:v).ToArray());}
            return result;
        }
        try
        {
            await using(var db=factory()){db.AddRange(students);db.AddRange(terms);db.AddRange(courses);await db.SaveChangesAsync();}
            // A Submitted + B Approved have identical credits. Further A sections cover inactive states and another active section.
            for(int i=0;i<7;i++) offerings.Add(new(){CourseId=courses[i==1?1:0].Id,AcademicTermId=terms[i==6?1:0].Id,SectionCode="S"+i,Capacity=3});
            await using(var db=factory()){db.AddRange(offerings);await db.SaveChangesAsync();}
            Enrollment Make(int s,int o,EnrollmentStatus status)
            {
                var e=new Enrollment{StudentId=students[s].Id,CourseOfferingId=offerings[o].Id,CreatedAtUtc=DateTimeOffset.UtcNow};
                if(status!=EnrollmentStatus.Draft)Check(e.TrySubmit(),"fixture submit");
                if(status==EnrollmentStatus.Approved)Check(e.TryApprove(),"fixture approve");
                if(status==EnrollmentStatus.Rejected)Check(e.TryReject("fixture"),"fixture reject");
                if(status==EnrollmentStatus.Dropped)Check(e.TryDrop(),"fixture drop");
                return e;
            }
            var rows=new[]{Make(0,0,EnrollmentStatus.Submitted),Make(0,1,EnrollmentStatus.Approved),
                Make(0,2,EnrollmentStatus.Draft),Make(0,3,EnrollmentStatus.Rejected),Make(0,4,EnrollmentStatus.Dropped),
                Make(1,0,EnrollmentStatus.Submitted),Make(1,2,EnrollmentStatus.Approved),Make(0,6,EnrollmentStatus.Approved)};
            await using(var db=factory())
            {
                db.AddRange(rows);
                db.AddRange(Enumerable.Range(0,3).Select(i=>new CourseOfferingTimeSlot{CourseOfferingId=offerings[0].Id,Day=DayOfWeek.Monday,StartsAt=new(8+i,0),EndsAt=new(9+i,0)}));
                await SeatCapacityChecks.SaveFixtureAsync(db);
            }
            var owner=await identity.ForRequestAsync("GET",Summary(0),null,null);
            var other=await identity.ForRequestAsync("GET",Summary(1),null,null);
            var empty=await identity.ForRequestAsync("GET",Summary(2),null,null);
            var advisor=await identity.ForRequestAsync("POST",$"/enrollments/{rows[0].Id}/approve",null,null);
            string advisorId;
            await using(var db=factory()) advisorId=(await db.Students.SingleAsync(s=>s.Id==students[0].Id)).AdvisorUserId!;
            var before=await Snapshot();
            var occupancy=(await Get(identity.Client,Report("occupancy")))["items"]!.AsArray();
            Check(occupancy.Count==6,"term filter and empty offering retained");
            int[] expected={2,1,1,0,0,0};
            for(int i=0;i<6;i++)
            {
                var r=occupancy[i]!;
                Check(r["offeringId"]!.GetValue<int>()==offerings[i].Id && r["courseCode"]!.GetValue<string>()==courses[i==1?1:0].Code
                    && r["capacity"]!.GetValue<int>()==3 && r["activeCount"]!.GetValue<int>()==expected[i]
                    && r["occupiedSeats"]!.GetValue<int>()==expected[i],"exact occupancy fields");
                Check(Math.Abs(r["occupancyPercent"]!.GetValue<decimal>()-100m*expected[i]/3)<0.0001m,"fractional percentage");
            }
            var pending=(await Get(identity.Client,Report("pending-approvals")))["items"]!.AsArray();
            Check(pending.Count==2 && pending[0]!["advisorUserId"] is null && pending[1]!["advisorUserId"]!.GetValue<string>()==advisorId
                && pending.All(n=>n!["submittedCount"]!.GetValue<int>()==1),"unassigned and assigned Submitted groups");
            Check((await Get(owner,Summary(0)))["activeCredits"]!.GetValue<long>()==6,"equal credit courses summed, other term excluded");
            Check((await Get(other,Summary(1)))["activeCredits"]!.GetValue<long>()==6,"same course two sections count separately");
            Check((await Get(empty,Summary(2)))["activeCredits"]!.GetValue<long>()==0,"zero load");
            await Get(advisor,Summary(0));await Get(advisor,Summary(1),404);await Get(owner,Summary(1),404);
            using(var anonymous=identity.AnonymousClient()){await Get(anonymous,Report("occupancy"),401);await Get(anonymous,Summary(0),401);}
            foreach(var name in new[]{"occupancy","pending-approvals"}) {await Get(owner,Report(name),403);await Get(advisor,Report(name),403);}
            await Get(identity.Client,Report("occupancy","?page=0"),400);
            await Get(identity.Client,Report("occupancy","?pageSize=101"),400);
            await Get(identity.Client,Report("occupancy","?page=2147483647&pageSize=100"),400);
            var page=(await Get(identity.Client,Report("occupancy","?page=2&pageSize=2")))["items"]!.AsArray();
            Check(page.Select(n=>n!["offeringId"]!.GetValue<int>()).SequenceEqual(offerings.Skip(2).Take(2).Select(o=>o.Id)),"deterministic paging");
            Check((await Get(identity.Client,Report("occupancy","?page=10")))["items"]!.AsArray().Count==0,"beyond final page");

            foreach(var file in new[]{"report-occupancy-join.sql","report-occupancy-subquery.sql","report-occupancy-cte.sql"})
            {
                var sql=await SqlExample(file);Check(sql.Count==6,"SQL occupancy rows");
                for(int i=0;i<6;i++)Check((int)sql[i][0]! ==offerings[i].Id && (string)sql[i][1]! ==courses[i==1?1:0].Code
                    && (int)sql[i][2]! ==3 && (int)sql[i][3]! ==expected[i] && (int)sql[i][4]! ==expected[i]
                    && Math.Abs((decimal)sql[i][5]!-100m*expected[i]/3)<0.0001m,"SQL example equivalence");
            }
            Check((long)(await SqlExample("report-active-credits.sql"))[0][0]! ==6L,"SQL bigint sum");
            Check((long)(await SqlExample("report-active-credits.sql",2))[0][0]! ==0L,"SQL zero sum");
            var groups=await SqlExample("report-pending-having.sql");
            Check(groups.Count==2 && groups[0][0] is null && (string)groups[1][0]! ==advisorId && groups.All(r=>(int)r[1]! ==1),"HAVING at one equals application");
            Check((await SqlExample("report-pending-having.sql",minimum:2)).Count==0,"HAVING removes small groups");
            Check((await SqlExample("report-submitted-exists.sql")).Select(r=>(int)r[0]!).SequenceEqual(new[]{offerings[0].Id}),"EXISTS returns offering once");

            await using(var template=factory())
            {
                var capture=new CatalogSqlCapture();
                await using var db=new UniversityDbContext(new DbContextOptionsBuilder<UniversityDbContext>().UseSqlServer(template.Database.GetConnectionString()).AddInterceptors(capture).Options);
                var reader=new RegistrationReports(db);
                await reader.OccupancyAsync(terms[0].Id,1,100,default);
                Check(capture.Commands.Count==1 && capture.Commands[0].Text.Contains("LEFT JOIN") && capture.Commands[0].Text.Contains("GROUP BY")
                    && capture.Commands[0].Text.Contains("WHERE") && capture.Commands[0].Text.Contains("OFFSET") && capture.Commands[0].Text.Contains("ORDER BY"),"one SQL occupancy aggregate/paging");
                capture.Commands.Clear();var small=await reader.OccupancyAsync(terms[0].Id,1,1,default);
                Check(small.Items.Count==1 && capture.Commands.Count==1,"one read at one and six rows");
                capture.Commands.Clear();await reader.PendingAsync(terms[0].Id,1,100,default);
                Check(capture.Commands.Count==1 && capture.Commands[0].Text.Contains("GROUP BY") && capture.Commands[0].Text.Contains("WHERE"),"one SQL advisor aggregate");
                capture.Commands.Clear();await new TermRegistrationSummaryFacade(db).GetAsync(students[0].Id,terms[0].Id,default);
                var sum=capture.Commands.Single(c=>c.Text.Contains("SUM(")).Text;
                Check(sum.Contains("bigint") && sum.Contains("COALESCE") && sum.Contains("WHERE"),"SQL wide nullable sum");
                Check(!db.ChangeTracker.Entries().Any() && capture.Commands.All(c=>c.Text.TrimStart().StartsWith("SELECT")),"untracked read-only projections");
                capture.Commands.Clear();
                try{await reader.OccupancyAsync(terms[0].Id,int.MaxValue,100,default);Check(false,"invalid direct query accepted");}catch(ArgumentOutOfRangeException){}
                Check(capture.Commands.Count==0,"direct validation avoids SQL");
            }
            Check(before==await Snapshot(),"fresh context fixture unchanged");
            Console.WriteLine("PASS: reports HTTPS counts [2,1,1,0,0,0], credits 6/6/0, advisor groups 1/1; SQL examples equivalent; paging/access/no-store; aggregate SQL and no writes/tracking");
        }
        catch(Exception ex){failure=ex;throw;}
        finally
        {
            try
            {
                await using var db=factory();var oids=offerings.Select(o=>o.Id).ToArray();var sids=students.Select(s=>s.Id).ToArray();
                var tids=terms.Select(t=>t.Id).ToArray();var cids=courses.Select(c=>c.Id).ToArray();
                await db.Enrollments.Where(e=>oids.Contains(e.CourseOfferingId)).ExecuteDeleteAsync();
                await db.CourseOfferingTimeSlots.Where(s=>oids.Contains(s.CourseOfferingId)).ExecuteDeleteAsync();
                await db.Students.Where(s=>sids.Contains(s.Id)).ExecuteDeleteAsync();await db.CourseOfferings.Where(o=>oids.Contains(o.Id)).ExecuteDeleteAsync();
                await db.AcademicTerms.Where(t=>tids.Contains(t.Id)).ExecuteDeleteAsync();await db.Courses.Where(c=>cids.Contains(c.Id)).ExecuteDeleteAsync();
                Console.WriteLine("PASS: reports fixture cleanup");
            }
            catch(Exception ex){Console.Error.WriteLine("CLEANUP reports: "+ex.GetType().Name);if(failure is null)throw new CleanupFailure();}
        }
    }
}
