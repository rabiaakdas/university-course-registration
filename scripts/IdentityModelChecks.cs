using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UniversityRegistration.Data;
using UniversityRegistration.Models;

// Synthetic, passwordless accounts only. The runner verifies ownership of a fresh test DB.
internal static class IdentityModelChecks
{
    static void Check(bool ok, string label) { if (!ok) throw new BehaviorFailure("Identity model: " + label); }
    public static void Unit(bool probe = false)
    {
        foreach (var value in new string?[] { null, "", " \t\r\n\u3000" })
        {
            var student = new Student();
            Check(!student.TryLinkAccount(value) && student.UserId is null, "invalid account ID leaves profile unlinked");
        }
        var s = new Student { StudentNumber = "00123", FullName = "Synthetic student" };
        Check(s.TryLinkAccount(" AbC-123 ") && s.UserId == " AbC-123 ", "opaque account ID preserved exactly");
        foreach (var value in new string?[] { " AbC-123 ", "other", null })
            Check(!s.TryLinkAccount(value) && s.UserId == " AbC-123 ", "repeat/relink rejection preserves connection");
        Check(s.StudentNumber == "00123" && s.FullName == "Synthetic student", "academic profile unchanged");
        Check(!probe, "controlled runner failure probe");
        Console.WriteLine("PASS: account link exact value, invalid/repeated/relink rejection; academic profile preserved (no DB)");
    }

    static async Task Constraint(Func<Task> action, int number, string name)
    {
        try { await action(); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql &&
            sql.Errors.Cast<SqlError>().Any(e => e.Number == number && e.Message.Contains(name, StringComparison.Ordinal))) { return; }
        throw new BehaviorFailure($"Identity model: expected SQL {number}/{name}");
    }

    // Prior model cannot INSERT Student through today's mapping (UserId does not exist yet).
    public static async Task Upgrade(Func<UniversityDbContext> factory)
    {
        const string previous = "20261029131736_AddEnrollmentLifecycle";
        var tag = "I24M_" + Guid.NewGuid().ToString("N")[..10];
        var c = new Course { Code = tag, Name = "İlişki koruma", Credits = 4 };
        var t = new AcademicTerm { Name = tag, StartsOn = new(2026, 9, 1), EndsOn = new(2027, 1, 1) };
        CourseOffering? o = null; int studentId = 0; Enrollment? e = null; Exception? failure = null;
        try
        {
            await using (var db = factory())
            {
                Check(!(await db.Database.GetAppliedMigrationsAsync()).Any(x => x.EndsWith("AddIdentityAccounts")), "upgrade fixture never downgrades");
                await db.GetService<IMigrator>().MigrateAsync(previous);
                db.Add(t); await db.SaveChangesAsync(); await CourseVersionChecks.HistoricalCourseAsync(db,c);
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.Students(StudentNumber, FullName) VALUES({tag}, {"Synthetic migration student"})");
                studentId = await db.Students.Where(x => x.StudentNumber == tag).Select(x => x.Id).SingleAsync();
                o = await SeatCapacityChecks.HistoricalOfferingAsync(db,c.Id,t.Id,30);
                e = StateChecks.Active(studentId, o.Id);
                db.AddRange(e, new StudentCompletedCourse { StudentId = studentId, CourseId = c.Id });
                await db.SaveChangesAsync();
                await db.Database.MigrateAsync(); await db.Database.MigrateAsync();
            }
            await using (var db = factory())
            {
                var s = await db.Students.SingleAsync(x => x.Id == studentId);
                var enrollment = await db.Enrollments.SingleAsync(x => x.Id == e!.Id);
                Check(s.UserId is null && s.StudentNumber == tag && s.FullName == "Synthetic migration student", "old Student fields/ID/null UserId");
                Check(enrollment.StudentId == studentId && enrollment.CourseOfferingId == o!.Id && enrollment.Status == EnrollmentStatus.Approved &&
                    enrollment.RejectionReason is null && enrollment.CreatedAtUtc == e!.CreatedAtUtc, "old enrollment identity/state/time/FKs");
                Check(await db.StudentCompletedCourses.AnyAsync(x => x.StudentId == studentId && x.CourseId == c.Id), "completion relation preserved");
                Check(await db.Courses.AnyAsync(x => x.Id == c.Id && x.Name == c.Name && x.Credits == 4) &&
                    await db.AcademicTerms.AnyAsync(x => x.Id == t.Id && x.StartsOn == t.StartsOn && x.EndsOn == t.EndsOn) &&
                    await db.CourseOfferings.AnyAsync(x => x.Id == o!.Id && x.CourseId == c.Id && x.AcademicTermId == t.Id && x.Capacity == 30), "academic records preserved");
                Check(!db.Database.HasPendingModelChanges() && !(await db.Database.GetPendingMigrationsAsync()).Any(), "migration replay/snapshot");
            }
            Console.WriteLine("PASS: previous-schema Student/enrollment/completion/academic data preserved; UserId NULL; migration replay and snapshot aligned");
        }
        catch (Exception ex) { failure = ex; throw; }
        finally
        {
            try
            {
                await using var db = factory();
                await db.Enrollments.Where(x => x.StudentId == studentId).ExecuteDeleteAsync();
                await db.StudentCompletedCourses.Where(x => x.StudentId == studentId).ExecuteDeleteAsync();
                if (o is not null) await db.CourseOfferings.Where(x => x.Id == o.Id).ExecuteDeleteAsync();
                await db.Students.Where(x => x.Id == studentId).ExecuteDeleteAsync();
                await db.AcademicTerms.Where(x => x.Id == t.Id).ExecuteDeleteAsync();
                await db.Courses.Where(x => x.Id == c.Id).ExecuteDeleteAsync();
            }
            catch (Exception ex) { Console.Error.WriteLine("CLEANUP identity migration: " + ex.GetType().Name); if (failure is null) throw new CleanupFailure(); }
        }
    }

    public static async Task Run(Func<UniversityDbContext> factory)
    {
        string connection;
        await using (var db = factory()) connection = db.Database.GetConnectionString()!;
        var services = new ServiceCollection();
        services.AddLogging(b => b.ClearProviders());
        services.AddDbContext<UniversityDbContext>(b => b.UseSqlServer(connection));
        services.AddIdentityCore<ApplicationUser>().AddEntityFrameworkStores<UniversityDbContext>();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        var tag = "I24_" + Guid.NewGuid().ToString("N")[..10];
        var userIds = new List<string>(); var studentIds = new List<int>(); Exception? failure = null;
        var course = new Course { Code = tag, Name = "Identity test course", Credits = 1 };
        var term = new AcademicTerm { Name = tag, StartsOn = new(2026, 1, 1), EndsOn = new(2026, 6, 1) };
        CourseOffering? offering = null; Enrollment? enrollment = null;
        async Task<ApplicationUser> Create(UserManager<ApplicationUser> manager, string suffix)
        {
            var user = new ApplicationUser { UserName = tag + suffix };
            userIds.Add(user.Id); // Ownership retained even if the following write fails.
            var result = await manager.CreateAsync(user);
            Check(result.Succeeded, "passwordless CreateAsync: " + string.Join(',', result.Errors.Select(x => x.Code)));
            Check(!await manager.HasPasswordAsync(user), "passwordless is not password login");
            return user;
        }
        try
        {
            string linkedId;
            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<UniversityDbContext>();
                var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                Check(scope.ServiceProvider.GetRequiredService<IUserStore<ApplicationUser>>() is not null, "EF store DI");
                Check(scope.ServiceProvider.GetService<RoleManager<IdentityRole>>() is null, "role tables do not imply role services");
                var user = await Create(manager, "linked"); linkedId = user.Id;
                var duplicate = await manager.CreateAsync(new ApplicationUser { UserName = user.UserName });
                Check(!duplicate.Succeeded && duplicate.Errors.Select(x => x.Code).SequenceEqual(new[] { "DuplicateUserName" }), "explicit IdentityResult duplicate reason");
                var students = Enumerable.Range(0, 3).Select(i => new Student { StudentNumber = tag + i, FullName = "Synthetic identity student" }).ToArray();
                db.AddRange(students); db.AddRange(course, term); await db.SaveChangesAsync();
                studentIds.AddRange(students.Select(x => x.Id));
                Check(students.All(x => x.UserId is null), "multiple NULL account links");
                offering = new() { CourseId = course.Id, AcademicTermId = term.Id, SectionCode = "A", Capacity = 40 };
                db.Add(offering); await db.SaveChangesAsync();
                enrollment = StateChecks.Active(students[0].Id, offering.Id);
                db.AddRange(enrollment, new StudentCompletedCourse { StudentId = students[0].Id, CourseId = course.Id });
                Check(students[0].TryLinkAccount(user.Id), "link existing account");
                await SeatCapacityChecks.SaveFixtureAsync(db);
                await Create(manager, "profileless");
            }
            await using (var scope = provider.CreateAsyncScope())
            {
                var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var db = scope.ServiceProvider.GetRequiredService<UniversityDbContext>();
                Check((await manager.FindByIdAsync(linkedId))?.Id == linkedId && await manager.FindByIdAsync(Guid.NewGuid().ToString()) is null, "new scope existing/missing identity");
                Check((await db.Students.SingleAsync(x => x.Id == studentIds[0])).UserId == linkedId, "private setter persisted/reloaded");
                Check(await db.Users.CountAsync(x => userIds.Contains(x.Id)) == 2 && await db.Students.CountAsync(x => x.UserId != null && userIds.Contains(x.UserId)) == 1, "profileless account allowed");
            }
            await using (var db = factory())
            {
                var s = await db.Students.SingleAsync(x => x.Id == studentIds[1]);
                Check(s.TryLinkAccount(linkedId), "entity alone does not enforce cross-profile ownership");
                await Constraint(async () => { await db.SaveChangesAsync(); }, 2601, "UX_Students_UserId");
            }
            await using (var db = factory())
            {
                var s = await db.Students.SingleAsync(x => x.Id == studentIds[2]);
                Check(s.TryLinkAccount(Guid.NewGuid().ToString()), "missing account is checked by FK");
                await Constraint(async () => { await db.SaveChangesAsync(); }, 547, "FK_Students_AspNetUsers");
            }
            await using (var scope = provider.CreateAsyncScope())
            {
                var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var user = (await manager.FindByIdAsync(linkedId))!;
                await Constraint(async () => { await manager.DeleteAsync(user); }, 547, "FK_Students_AspNetUsers");
            }
            string separatelySavedId;
            await using (var scope = provider.CreateAsyncScope())
            {
                var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var db = scope.ServiceProvider.GetRequiredService<UniversityDbContext>();
                var user = await Create(manager, "separate"); separatelySavedId = user.Id;
                var invalidProfile = new Student { StudentNumber = tag + "0", FullName = "Synthetic duplicate profile" };
                Check(invalidProfile.TryLinkAccount(user.Id), "arrange separate-save failure");
                db.Add(invalidProfile);
                await Constraint(async () => { await db.SaveChangesAsync(); }, 2601, "UX_Students_StudentNumber");
                // Do not reuse this failed context or retry its pending entities.
            }
            await using (var db = factory())
            {
                Check(await db.Users.AnyAsync(x => x.Id == separatelySavedId) && !await db.Students.AnyAsync(x => x.UserId == separatelySavedId), "CreateAsync committed account survives later profile failure in same context");
                Check(await db.Users.AnyAsync(x => x.Id == linkedId) && await db.Students.CountAsync(x => studentIds.Contains(x.Id)) == 3 &&
                    await db.Students.CountAsync(x => studentIds.Contains(x.Id) && x.UserId == null) == 2, "constraint failures preserve accounts/profiles");
                Check(await db.Enrollments.AnyAsync(x => x.Id == enrollment!.Id && x.Status == EnrollmentStatus.Approved && x.StudentId == studentIds[0] && x.CourseOfferingId == offering!.Id) &&
                    await db.StudentCompletedCourses.AnyAsync(x => x.StudentId == studentIds[0] && x.CourseId == course.Id), "blocked account deletion preserves academic data");
            }
            Console.WriteLine("PASS: real Identity EF DI/passwordless UserManager/read/IdentityResult; optional unique FK/NO ACTION; private setter reload; separate-save account survives profile failure");
        }
        catch (Exception ex) { failure = ex; throw; }
        finally
        {
            try
            {
                await using var db = factory();
                // Number prefix covers an INSERT committed just before a setup exception; it is unique to this run.
                var owned = await db.Students.Where(x => x.StudentNumber.StartsWith(tag)).Select(x => x.Id).ToArrayAsync();
                await db.Enrollments.Where(x => owned.Contains(x.StudentId)).ExecuteDeleteAsync();
                await db.StudentCompletedCourses.Where(x => owned.Contains(x.StudentId)).ExecuteDeleteAsync();
                await db.Students.Where(x => owned.Contains(x.Id)).ExecuteDeleteAsync();
                await db.Users.Where(x => userIds.Contains(x.Id) && x.UserName!.StartsWith(tag)).ExecuteDeleteAsync();
                if (offering is not null) await db.CourseOfferings.Where(x => x.Id == offering.Id).ExecuteDeleteAsync();
                await db.AcademicTerms.Where(x => x.Id == term.Id).ExecuteDeleteAsync();
                await db.Courses.Where(x => x.Id == course.Id).ExecuteDeleteAsync();
                Check(!await db.Users.AnyAsync(x => userIds.Contains(x.Id)) && !await db.Students.AnyAsync(x => x.StudentNumber.StartsWith(tag)), "owned identity/profile cleanup");
                Console.WriteLine("PASS: owned test profiles removed before test accounts; academic fixture cleaned");
            }
            catch (Exception ex) { Console.Error.WriteLine("CLEANUP identity model: " + ex.GetType().Name); if (failure is null) throw new CleanupFailure(); }
        }
    }
}
