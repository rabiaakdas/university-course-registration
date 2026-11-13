using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using UniversityRegistration.Application;
using UniversityRegistration.Data;
using UniversityRegistration.Models;

// Only delete receipts belonging to the exact enrollment cleanup predicate.
internal static class EnrollmentReceiptTestCleanup
{
    public static async Task<int> DeleteAsync(UniversityDbContext db, IQueryable<Enrollment> rows, CancellationToken ct = default)
    {
        if (await db.Database.SqlQuery<int>($"SELECT CASE WHEN OBJECT_ID(N'dbo.EnrollmentCreationReceipts') IS NULL THEN 0 ELSE 1 END AS [Value]").SingleAsync(ct) == 1)
            await db.EnrollmentCreationReceipts.Where(x => rows.Select(e => (int?)e.Id).Contains(x.EnrollmentId)).ExecuteDeleteAsync(ct);
        return await rows.ExecuteDeleteAsync(ct);
    }
}

internal static class IdempotencyChecks
{
    static void Check(bool ok, string label) { if (!ok) throw new BehaviorFailure("Idempotency: " + label); }
    public static async Task Upgrade(Func<UniversityDbContext> factory)
    {
        var student = new Student { StudentNumber = "I41U" + Guid.NewGuid().ToString("N")[..10], FullName = "Preserved profile" };
        try
        {
            await using (var db = factory())
            {
                await db.GetService<IMigrator>().MigrateAsync("20261110112155_AddCourseRowVersion");
                db.Students.Add(student); await db.SaveChangesAsync();
                await db.Database.MigrateAsync(); await db.Database.MigrateAsync();
            }
            await using var fresh = factory();
            Check(await fresh.Students.AnyAsync(x => x.Id == student.Id && x.StudentNumber == student.StudentNumber && x.FullName == student.FullName), "upgrade preserves existing profile");
            Check(!await fresh.EnrollmentCreationReceipts.AnyAsync() && !fresh.Database.HasPendingModelChanges(), "new empty receipts; model aligned; second update");
            Console.WriteLine("PASS: idempotency migration upgrade preserves old profile; second update/model aligned");
        }
        finally { await using var db = factory(); await db.Students.Where(x => x.Id == student.Id).ExecuteDeleteAsync(); }
    }
    sealed record Reply(int Status, string Json, string? Location);
    sealed class ClaimGate : SaveChangesInterceptor
    {
        readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int arrivals;
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (data.Context!.ChangeTracker.Entries<EnrollmentCreationReceipt>().Any(e => e.State == EntityState.Added))
            {
                if (Interlocked.Increment(ref arrivals) == 2) ready.TrySetResult();
                await ready.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);
            }
            return result;
        }
        public int Collisions;
        public override Task SaveChangesFailedAsync(DbContextErrorEventData data, CancellationToken ct = default)
        {
            if (data.Exception is DbUpdateException e && RegistrationWriteErrors.Classify(e) == "idempotencyKey") Interlocked.Increment(ref Collisions);
            return Task.CompletedTask;
        }
    }
    public static async Task Run(Func<UniversityDbContext> factory, CatalogTestIdentity identity, Action stop, Func<Task> start)
    {
        var tag = "I41" + Guid.NewGuid().ToString("N")[..10];
        var c = new Course { Code = tag, Name = "Idempotency", Credits = 3 };
        var term = new AcademicTerm { Name = tag, StartsOn = new(2026,1,1), EndsOn = new(2026,6,1) };
        var a = new Student { StudentNumber = tag + "A", FullName = "Test A" };
        var b = new Student { StudentNumber = tag + "B", FullName = "Test B" };
        var offers = new List<CourseOffering>();
        var faultInstalled = false; Exception? primaryFailure=null;
        try
        {
            await using (var db = factory())
            {
                db.AddRange(c, term, a, b); await db.SaveChangesAsync();
                offers.AddRange(Enumerable.Range(1,5).Select(i => new CourseOffering { CourseId = c.Id, AcademicTermId = term.Id, SectionCode = i.ToString(), Capacity = 10 }));
                db.AddRange(offers); await db.SaveChangesAsync();
            }
            var ca = await identity.ForRequestAsync("POST", "/enrollments", new { studentId = a.Id }, null);
            var cb = await identity.ForRequestAsync("POST", "/enrollments", new { studentId = b.Id }, null);
            async Task<Reply> Send(HttpClient client, int student, int offering, string? key, bool reverse = false)
            {
                using var q = new HttpRequestMessage(HttpMethod.Post, "/enrollments");
                q.Content = reverse ? JsonContent.Create(new { courseOfferingId = offering, studentId = student }) : JsonContent.Create(new { studentId = student, courseOfferingId = offering });
                if (key is not null) q.Headers.Add("Idempotency-Key", key);
                using var requestTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                using var response = await client.SendAsync(q, requestTimeout.Token);
                return new((int)response.StatusCode, await response.Content.ReadAsStringAsync(), response.Headers.Location?.ToString());
            }
            foreach (var invalid in new string?[] { null, "bad", Guid.Empty.ToString("D") })
                Check((await Send(ca,a.Id,offers[0].Id,invalid)).Status == 400, "required nonempty UUID");
            var key = Guid.NewGuid().ToString("D");
            var first = await Send(ca,a.Id,offers[0].Id,key);
            Check(first.Status == 201 && JsonNode.Parse(first.Json)!["status"]!.GetValue<string>() == "Draft", "first Draft201");
            Check(first == await Send(ca,a.Id,offers[0].Id,key,true), "same persisted JSON/Location; field order irrelevant");
            var csrf = ca.DefaultRequestHeaders.GetValues("X-CSRF-TOKEN").Single();
            ca.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
            try { Check((await Send(ca,a.Id,offers[0].Id,key)).Status == 400, "CSRF required even on replay"); }
            finally { ca.DefaultRequestHeaders.Add("X-CSRF-TOKEN",csrf); }
            using (var anonymous = identity.AnonymousClient()) Check((await Send(anonymous,a.Id,offers[0].Id,key)).Status == 401, "anonymous cannot replay");
            var different = await Send(ca,a.Id,offers[1].Id,key);
            Check(different.Status == 409 && different.Json.Contains("idempotency_key_reused"), "different meaningful request");
            Check((await Send(cb,a.Id,offers[0].Id,key)).Status == 404, "other account cannot replay A");
            var ownB = await Send(cb,b.Id,offers[0].Id,key);
            Check(ownB.Status == 201 && ownB.Location != first.Location, "same key independent account scope");
            var id = JsonNode.Parse(first.Json)!["id"]!.GetValue<int>();
            using (var submitted = await ca.PostAsync($"/enrollments/{id}/submit", null)) Check((int)submitted.StatusCode == 200, "resource evolves");
            Check(first == await Send(ca,a.Id,offers[0].Id,key), "replay keeps original Draft response");
            using (var detail = await ca.GetAsync(first.Location)) Check((await detail.Content.ReadAsStringAsync()).Contains("Submitted"), "detail is current");
            stop(); await start();
            Check(first == await Send(ca,a.Id,offers[0].Id,key), "restart persisted receipt");
            // Commit result deliberately discarded, then retried. This is not injected network loss.
            var lostKey = Guid.NewGuid().ToString("D");
            _ = await Send(ca,a.Id,offers[1].Id,lostKey);
            Check((await Send(ca,a.Id,offers[1].Id,lostKey)).Status == 201, "retry after ignoring committed response");
            var raceKey = Guid.NewGuid().ToString("D");
            var race = await Task.WhenAll(Enumerable.Range(0,4).Select(_ => Send(ca,a.Id,offers[2].Id,raceKey)));
            Check(race.All(r => r.Status == 201 && r == race[0]), "concurrent HTTPS same response");
            Console.WriteLine("RESULT idempotency HTTP: four201, identical stored responses");
            await ConcurrencyInvariantChecks.Verify(factory,offers.Select(o=>o.Id).ToArray(),"same-key HTTPS create");
            await using(var db=factory()) Check(await db.Enrollments.CountAsync(e=>e.StudentId==a.Id&&e.CourseOfferingId==offers[2].Id)==1
                && await db.Enrollments.AnyAsync(e=>e.StudentId==a.Id&&e.CourseOfferingId==offers[2].Id&&e.Status==EnrollmentStatus.Draft),"same-key one persisted Draft");
            string account;
            await using (var db = factory()) account = (await db.Students.SingleAsync(x => x.Id == a.Id)).UserId!;
            // Force both separate contexts past the absent-key read before attempting UNIQUE claims.
            var gate = new ClaimGate();
            using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var directKey = Guid.NewGuid();
            async Task<IdempotentEnrollmentCreation.Result> Contender()
            {
                try
                {
                    await using var template = factory();
                    var options = new DbContextOptionsBuilder<UniversityDbContext>().UseSqlServer(template.Database.GetConnectionString()).AddInterceptors(gate).Options;
                    await using var db = new UniversityDbContext(options);
                    var ops = new EnrollmentOperations(db,new EnrollmentPolicy(30),DecoratorChecks.TimedEvaluator(),ObserverChecks.EmptyPublisher());
                    return await new IdempotentEnrollmentCreation(db,ops,options).CreateAsync(account,directKey,new(a.Id,offers[3].Id),limit.Token);
                }
                catch { limit.Cancel(); throw; }
            }
            var pair = await Task.WhenAll(Contender(),Contender());
            Check(gate.Collisions == 1 && pair.All(r => r.Receipt is not null) && pair[0].Receipt!.ResponseJson == pair[1].Receipt!.ResponseJson, "real UNIQUE loser rereads using fresh context");
            Console.WriteLine("REACHED: idempotency UNIQUE claim collision exactly1; both workers settled");
            await ConcurrencyInvariantChecks.Verify(factory,offers.Select(o=>o.Id).ToArray(),"same-key SQL claim race");
            var retryKey = Guid.NewGuid().ToString("D");
            Check((await Send(ca,a.Id,int.MaxValue,retryKey)).Status == 400, "business rejection");
            await using (var db = factory()) Check(!await db.EnrollmentCreationReceipts.AnyAsync(x => x.Key == Guid.Parse(retryKey)), "rejection rolls back claim");
            await using (var db = factory())
            {
                await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER dbo.IdempotencyTestFault ON dbo.EnrollmentCreationReceipts AFTER UPDATE AS BEGIN THROW 51041, 'Controlled receipt write failure', 1; END");
                faultInstalled = true;
            }
            Check((await Send(ca,a.Id,offers[4].Id,retryKey)).Status == 500, "receipt write fault safe500");
            await using (var db = factory())
            {
                Check(!await db.EnrollmentCreationReceipts.AnyAsync(x => x.Key == Guid.Parse(retryKey)) && !await db.Enrollments.AnyAsync(x => x.StudentId == a.Id && x.CourseOfferingId == offers[4].Id), "real write error rolls back Draft and claim");
                await db.Database.ExecuteSqlRawAsync("DROP TRIGGER dbo.IdempotencyTestFault"); faultInstalled = false;
            }
            await ConcurrencyInvariantChecks.Verify(factory,offers.Select(o=>o.Id).ToArray(),"receipt failure rollback");
            Check((await Send(ca,a.Id,offers[4].Id,retryKey)).Status == 201, "same key usable after rollback");
            await using (var db = factory())
            {
                var receipt = await db.EnrollmentCreationReceipts.SingleAsync(x => x.AccountId == account && x.Key == Guid.Parse(key));
                receipt.CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-2); receipt.ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(-1); await db.SaveChangesAsync();
            }
            var expired = await Send(ca,a.Id,offers[0].Id,key);
            Check(expired.Status == 409 && expired.Json.Contains("idempotency_key_expired"), "expired key never new work");
            await using (var db = factory()) await db.Students.Where(x => x.Id == a.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.UserId,(string?)null));
            Check((await Send(ca,a.Id,offers[1].Id,lostKey)).Status == 404, "current ownership before replay");
            await using (var db = factory())
            {
                await db.Students.Where(x => x.Id == a.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.UserId,account));
                var memberships = await db.UserRoles.Where(x => x.UserId == account).ToListAsync();
                db.UserRoles.RemoveRange(memberships); await db.SaveChangesAsync();
                Check((await Send(ca,a.Id,offers[1].Id,lostKey)).Status == 403, "removed DB role overrides stale cookie on replay");
                db.UserRoles.AddRange(memberships); await db.SaveChangesAsync();
                Check(await db.Enrollments.CountAsync(x => x.StudentId == a.Id) == 5 && await db.EnrollmentCreationReceipts.CountAsync(x => x.AccountId == account) == 5, "one receipt per committed Draft; no partial receipts");
            }
            Console.WriteLine("PASS: idempotency HTTPS replay/race/restart/account scope/current access/expiry; real UNIQUE collision and receipt failure rollback; original response vs current detail");
        }
        catch(Exception error) { primaryFailure=error; throw; }
        finally
        {
            try
            {
                await using var db = factory();
                if (faultInstalled) await db.Database.ExecuteSqlRawAsync("DROP TRIGGER dbo.IdempotencyTestFault");
                await EnrollmentReceiptTestCleanup.DeleteAsync(db,db.Enrollments.Where(x => x.StudentId == a.Id || x.StudentId == b.Id));
                await db.Students.Where(x => x.Id == a.Id || x.Id == b.Id).ExecuteDeleteAsync();
                await db.CourseOfferings.Where(x => x.CourseId == c.Id).ExecuteDeleteAsync();
                await db.AcademicTerms.Where(x => x.Id == term.Id).ExecuteDeleteAsync();
                await db.Courses.Where(x => x.Id == c.Id).ExecuteDeleteAsync();
            }
            catch(Exception cleanup)
            {
                Console.Error.WriteLine("CLEANUP idempotency: "+cleanup.GetType().Name);
                if(primaryFailure is null) throw new CleanupFailure();
            }
        }
    }
}
