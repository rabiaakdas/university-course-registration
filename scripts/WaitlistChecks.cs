using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using UniversityRegistration.Accounts;
using UniversityRegistration.Data;
using UniversityRegistration.Models;

internal static class WaitlistChecks
{
    static void Check(bool ok, string label) { if (!ok) throw new BehaviorFailure("Waitlist: " + label); }
    public static async Task Upgrade(Func<UniversityDbContext> factory)
    {
        var student = new Student { StudentNumber = "W42U" + Guid.NewGuid().ToString("N")[..10], FullName = "Preserved waitlist upgrade" };
        try
        {
            await using (var db = factory())
            {
                await db.GetService<IMigrator>().MigrateAsync("20261112112705_AddEnrollmentCreationReceipts");
                db.Add(student); await db.SaveChangesAsync();
                await db.Database.MigrateAsync(); await db.Database.MigrateAsync();
            }
            await using var fresh = factory();
            Check(await fresh.Students.AnyAsync(s => s.Id == student.Id && s.FullName == student.FullName && s.StudentNumber == student.StudentNumber), "upgrade preserves existing student");
            Check(!await fresh.WaitlistEntries.AnyAsync() && !fresh.Database.HasPendingModelChanges(), "empty waitlist, second update/model aligned");
            Console.WriteLine("PASS: waitlist upgrade preserves old profile; second migration/model aligned");
        }
        finally { await using var db = factory(); await db.Students.Where(x => x.Id == student.Id).ExecuteDeleteAsync(); }
    }

    public static async Task Run(Func<UniversityDbContext> factory, CatalogTestIdentity identity)
    {
        var tag = "W42" + Guid.NewGuid().ToString("N")[..10];
        var course = new Course { Code = tag, Name = "Waitlist fixture", Credits = 3 };
        var term = new AcademicTerm { Name = tag, StartsOn = new(2026,1,1), EndsOn = new(2026,6,1) };
        var students = Enumerable.Range(0,5).Select(i => new Student { StudentNumber = tag+i, FullName = "Waitlist fixture "+i }).ToArray();
        CourseOffering? full = null, empty = null;
        async Task<(int Status, JsonNode? Body)> Send(HttpClient client, string method, string path, object? body = null)
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), path);
            if (body is not null) request.Content = JsonContent.Create(body);
            using var response = await client.SendAsync(request);
            var text = await response.Content.ReadAsStringAsync();
            return ((int)response.StatusCode, string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text));
        }
        string Route(int student, long? entry = null) => $"/students/{student}/waitlist" + (entry is null ? "" : $"/{entry}");
        try
        {
            await using (var db = factory())
            {
                db.AddRange(course, term); db.AddRange(students); await db.SaveChangesAsync();
                full = new CourseOffering { CourseId = course.Id, AcademicTermId = term.Id, SectionCode = "FULL", Capacity = 1 };
                empty = new CourseOffering { CourseId = course.Id, AcademicTermId = term.Id, SectionCode = "EMPTY", Capacity = 1 };
                db.AddRange(full, empty); await db.SaveChangesAsync();
                var occupant = new Enrollment { StudentId = students[4].Id, CourseOfferingId = full.Id, CreatedAtUtc = DateTimeOffset.UtcNow };
                Check(occupant.TrySubmit(), "fixture submitted");
                var draft = new Enrollment { StudentId = students[0].Id, CourseOfferingId = full.Id, CreatedAtUtc = DateTimeOffset.UtcNow };
                db.AddRange(occupant, draft); await SeatCapacityChecks.SaveFixtureAsync(db);
            }
            var clients = new List<HttpClient>();
            foreach (var student in students)
                clients.Add(await identity.ForRequestAsync("GET", $"/students/{student.Id}", null, null));
            var ca = clients[0]; var cb = clients[1];
            var body = new { courseOfferingId = full.Id };
            using var anonymous = identity.AnonymousClient();
            Check((await Send(anonymous,"POST",Route(students[0].Id),body)).Status == 401, "anonymous401");
            using var advisor = await identity.NewRoleClientAsync(UniversityRoles.Advisor);
            Check((await Send(advisor,"POST",Route(students[0].Id),body)).Status == 403, "wrong role403");
            Check((await Send(ca,"POST",Route(students[1].Id),body)).Status == 404, "wrong owner404");
            var csrf = ca.DefaultRequestHeaders.GetValues("X-CSRF-TOKEN").Single();
            ca.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
            try { Check((await Send(ca,"POST",Route(students[0].Id),body)).Status == 400, "join CSRF"); }
            finally { ca.DefaultRequestHeaders.Add("X-CSRF-TOKEN",csrf); }
            Check((await Send(ca,"POST",Route(students[0].Id),new { courseOfferingId = empty.Id })).Status == 409, "empty offering rejected");
            Check((await Send(ca,"POST",Route(students[0].Id),new { courseOfferingId = int.MaxValue })).Status == 404, "missing offering404");
            Check((await Send(ca,"POST",Route(students[0].Id),new { courseOfferingId = 0 })).Status == 400, "invalid offering400");
            Check((await Send(clients[4],"POST",Route(students[4].Id),body)).Body?["code"]?.GetValue<string>() == "waitlist_enrollment_exists", "Submitted rejected");
            await using (var db = factory())
            {
                var e = await db.Enrollments.SingleAsync(x => x.StudentId == students[4].Id);
                Check(e.TryApprove(), "fixture approved"); await db.SaveChangesAsync();
            }
            Check((await Send(clients[4],"POST",Route(students[4].Id),body)).Status == 409, "Approved rejected");
            // Rejected occupies no seat; another Submitted enrollment keeps this offering full.
            await using (var db = factory())
            {
                await db.Enrollments.Where(x => x.StudentId == students[4].Id).ExecuteDeleteAsync();
                var rejected = new Enrollment { StudentId = students[4].Id, CourseOfferingId = full.Id, CreatedAtUtc = DateTimeOffset.UtcNow };
                Check(rejected.TrySubmit() && rejected.TryReject("fixture"), "fixture rejected");
                var holder = new Enrollment { StudentId = students[3].Id, CourseOfferingId = full.Id, CreatedAtUtc = DateTimeOffset.UtcNow };
                Check(holder.TrySubmit(), "fixture seat holder"); db.AddRange(rejected,holder); await SeatCapacityChecks.SaveFixtureAsync(db);
            }
            Check((await Send(clients[4],"POST",Route(students[4].Id),body)).Status == 409, "Rejected rejected");
            // Release both HTTP operations together; no delay-based ordering or in-process production lock.
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            async Task<(int Status, JsonNode? Body)> Race() { await gate.Task.WaitAsync(TimeSpan.FromSeconds(10)); return await Send(ca,"POST",Route(students[0].Id),body); }
            var one = Race(); var two = Race(); gate.SetResult();
            var race = await Task.WhenAll(one,two).WaitAsync(TimeSpan.FromSeconds(30));
            Check(race.Count(x => x.Status == 201) == 1 && race.Count(x => x.Status == 409) == 1, "parallel duplicate one Waiting; Draft allowed");
            var first = race.Single(x => x.Status == 201).Body!;
            var id = first["id"]!.GetValue<long>();
            var parallel = await Task.WhenAll(Send(cb,"POST",Route(students[1].Id),body), Send(clients[2],"POST",Route(students[2].Id),body)).WaitAsync(TimeSpan.FromSeconds(30));
            Check(parallel.All(x => x.Status == 201), "different students joined");
            await using (var db = factory())
            {
                var rows = await db.WaitlistEntries.Where(x => x.CourseOfferingId == full.Id).OrderBy(x => x.QueueNumber).ToArrayAsync();
                Check(rows.Length == 3 && rows.Select(x => x.QueueNumber).Distinct().Count() == 3, "unique deterministic sequence");
                for (var i=0; i<rows.Length; i++)
                {
                    var client = clients[Array.FindIndex(students,s => s.Id == rows[i].StudentId)];
                    var read = await Send(client,"GET",Route(rows[i].StudentId,rows[i].Id));
                    Check(read.Status == 200 && read.Body!["position"]!.GetValue<long>() == i+1, "position ordered by queue number");
                }
            }
            Check((await Send(cb,"GET",Route(students[0].Id,id))).Status == 404, "read owner404");
            Check((await Send(cb,"GET",Route(students[1].Id,id))).Status == 404, "spoofed route entry404");
            Check((await Send(cb,"POST",Route(students[1].Id,id)+"/cancel")).Status == 404, "cancel wrong entry404");
            ca.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
            try { Check((await Send(ca,"POST",Route(students[0].Id,id)+"/cancel")).Status == 400, "cancel CSRF"); }
            finally { ca.DefaultRequestHeaders.Add("X-CSRF-TOKEN",csrf); }
            Check((await Send(ca,"POST",Route(students[0].Id,id)+"/cancel")).Status == 204, "cancel204");
            var cancelled = await Send(ca,"GET",Route(students[0].Id,id));
            Check(cancelled.Body!["position"] is null && cancelled.Body["status"]!.GetValue<string>() == "Cancelled", "closed no position");
            Check((await Send(ca,"POST",Route(students[0].Id,id)+"/cancel")).Status == 204, "repeat cancel204");
            var repeated = await Send(ca,"GET",Route(students[0].Id,id));
            Check(cancelled.Body.ToJsonString() == repeated.Body!.ToJsonString(), "repeat preserves closure timestamp");
            var rejoin = await Send(ca,"POST",Route(students[0].Id),body);
            Check(rejoin.Status == 201 && rejoin.Body!["id"]!.GetValue<long>() != id && rejoin.Body["queueNumber"]!.GetValue<long>() > first["queueNumber"]!.GetValue<long>() && rejoin.Body["position"]!.GetValue<long>() == 3, "rejoin new row at end");
            // Real SQL constraints, each rolled back in its own connection/context.
            async Task Constraint(FormattableString sql, int number, string name)
            {
                await using var db = factory(); await using var tx = await db.Database.BeginTransactionAsync();
                try { await db.Database.ExecuteSqlInterpolatedAsync(sql); throw new BehaviorFailure("Expected constraint " + name); }
                catch (SqlException error) when (error.Number == number && error.Message.Contains(name, StringComparison.Ordinal)) { }
                finally { await tx.RollbackAsync(); }
            }
            await Constraint($"INSERT dbo.WaitlistEntries(StudentId,CourseOfferingId,JoinedAtUtc,Status) VALUES({students[0].Id},{full.Id},{DateTimeOffset.UtcNow},0)",2601,"UX_WaitlistEntries_Waiting_Student_Offering");
            await Constraint($"INSERT dbo.WaitlistEntries(StudentId,CourseOfferingId,JoinedAtUtc,Status) VALUES({int.MaxValue},{full.Id},{DateTimeOffset.UtcNow},0)",547,"FK_WaitlistEntries_Students_StudentId");
            await Constraint($"INSERT dbo.WaitlistEntries(StudentId,CourseOfferingId,JoinedAtUtc,Status) VALUES({students[0].Id},{int.MaxValue},{DateTimeOffset.UtcNow},0)",547,"FK_WaitlistEntries_CourseOfferings_CourseOfferingId");
            await Constraint($"UPDATE dbo.WaitlistEntries SET Status=1 WHERE Id={rejoin.Body!["id"]!.GetValue<long>()}",547,"CK_WaitlistEntries_Closure");
            foreach (var status in new[] { WaitlistStatus.Placed, WaitlistStatus.Ineligible })
            {
                await using (var db = factory())
                    await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.WaitlistEntries SET Status={(int)status},ClosedAtUtc={DateTimeOffset.UtcNow},ClosureReason={"fixture"} WHERE Id={id}");
                Check((await Send(ca,"POST",Route(students[0].Id,id)+"/cancel")).Status == 409, "other closed states409");
            }
            await using (var db = factory())
            {
                Check(await db.CourseOfferings.Where(x => x.Id == full.Id).Select(x => x.OccupiedSeats).SingleAsync() == 1, "occupied seats unchanged");
                var enrollments = await db.Enrollments.Where(x => x.CourseOfferingId == full.Id).ToArrayAsync();
                Check(enrollments.Length == 3 && enrollments.Single(x => x.StudentId == students[0].Id).Status == EnrollmentStatus.Draft
                    && enrollments.Single(x => x.StudentId == students[3].Id).Status == EnrollmentStatus.Submitted
                    && enrollments.Single(x => x.StudentId == students[4].Id).Status == EnrollmentStatus.Rejected, "waitlist did not change enrollment/load");
                Check(await db.WaitlistEntries.CountAsync(x => x.CourseOfferingId == full.Id && x.Status == WaitlistStatus.Waiting) == 3, "final three Waiting");
            }
            Console.WriteLine("PASS: waitlist HTTPS parallel uniqueness/order/position/cancel/rejoin; role/ownership/CSRF; capacity/enrollment rejections; real FK/filtered UNIQUE/CHECK; no enrollment or seat writes");
        }
        finally
        {
            await using var db = factory();
            var ids = students.Select(x => x.Id).ToArray();
            await db.WaitlistEntries.Where(x => ids.Contains(x.StudentId)).ExecuteDeleteAsync();
            await EnrollmentReceiptTestCleanup.DeleteAsync(db,db.Enrollments.Where(x => ids.Contains(x.StudentId)));
            await db.Students.Where(x => ids.Contains(x.Id)).ExecuteDeleteAsync();
            await db.CourseOfferings.Where(x => x.CourseId == course.Id).ExecuteDeleteAsync();
            await db.AcademicTerms.Where(x => x.Id == term.Id).ExecuteDeleteAsync();
            await db.Courses.Where(x => x.Id == course.Id).ExecuteDeleteAsync();
        }
    }
}
