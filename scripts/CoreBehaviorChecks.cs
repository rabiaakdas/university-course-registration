using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using UniversityRegistration.Data;
using UniversityRegistration.Endpoints;
using UniversityRegistration.Models;

// Assertions contain only scenario names, generated IDs and structural values.
internal sealed class BehaviorFailure(string message) : Exception(message);
internal sealed class CleanupFailure : Exception;
internal static class CoreBehaviorChecks
{
    static void Equal<T>(T expected, T actual, string label)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new BehaviorFailure($"{label}: expected={expected}; actual={actual}");
    }
    public static void Unit(bool probe)
    {
        foreach (var credits in new[] { 0, -1 })
            Equal("credits", string.Join(',', CourseInput.Validate("C", "Name", credits).Errors.Keys), "nonpositive credit field");
        foreach (var blank in new string?[] { null, "", "   " })
        {
            Equal("code", string.Join(',', CourseInput.Validate(blank, "Name", 1).Errors.Keys), "blank code field");
            Equal("name", string.Join(',', CourseInput.Validate("C", blank, 1).Errors.Keys), "blank name field");
        }
        var valid = CourseInput.Validate(" cse201 ", " Veri Yapıları ", 3);
        Equal("CSE201", valid.Code, "code normalization");
        Equal("Veri Yapıları", valid.Name, "name trimming preserves case");
        Equal(0, valid.Errors.Count, "valid input");
        Equal("code,credits,name", string.Join(',', CourseInput.Validate(null, " ", 0).Errors.Keys.Order()), "aggregate field errors");
        SpecificationChecks.Unit();
        EligibilityChecks.Unit();
        PriorityChecks.Unit();
        FactoryExperiments.Run();
        if (probe) Equal(201, 409, "controlled runner failure probe");
        Console.WriteLine("PASS: unit — credit, blank fields, normalization, aggregate errors (no DB/HTTP)");
    }

    public static async Task Run(Func<UniversityDbContext> factory,
        Func<string, string, object?, string?, Task<(int Status, JsonNode Body, string? Location)>> send, bool reverse)
    {
        var scenarios = new[] { "success", "missing-offering", "duplicate" };
        foreach (var scenario in reverse ? scenarios.Reverse() : scenarios)
        {
            var suffix = Guid.NewGuid().ToString("N")[..12];
            var course = new Course { Code = "T10_" + suffix, Name = "Test course", Credits = 3 };
            var term = new AcademicTerm { Name = "T10_" + suffix, StartsOn = new(2026, 1, 1), EndsOn = new(2026, 1, 2) };
            var student = new Student { StudentNumber = "T10_" + suffix, FullName = "Synthetic test student" };
            var offeringIds = new List<int>();
            Exception? failure = null;
            try
            {
                await using (var arrange = factory())
                {
                    arrange.AddRange(course, term, student);
                    await arrange.SaveChangesAsync();
                    var offerings = new[] { "A", "B" }.Select(section => new CourseOffering
                    { CourseId = course.Id, AcademicTermId = term.Id, SectionCode = section, Capacity = 40 }).ToArray();
                    arrange.AddRange(offerings); await arrange.SaveChangesAsync();
                    offeringIds.AddRange(offerings.Select(o => o.Id));
                    arrange.Enrollments.Add(new Enrollment { StudentId = student.Id, CourseOfferingId = offeringIds[0], CreatedAtUtc = DateTimeOffset.UtcNow });
                    await arrange.SaveChangesAsync();
                }
                var requested = scenario == "duplicate" ? offeringIds[0] : offeringIds[1];
                string Snapshot(Enrollment e) => $"{e.Id}|{e.StudentId}|{e.CourseOfferingId}|{e.CreatedAtUtc:O}";
                string[] before;
                int students, offeringCount;
                await using (var read = factory())
                {
                    before = (await read.Enrollments.OrderBy(e => e.Id).ToListAsync()).Select(Snapshot).ToArray();
                    students = await read.Students.CountAsync(); offeringCount = await read.CourseOfferings.CountAsync();
                    if (scenario == "missing-offering")
                    {
                        requested = int.MaxValue;
                        Equal(false, await read.CourseOfferings.AnyAsync(o => o.Id == requested), "missing fixture must be absent");
                    }
                }
                var context = $"{scenario} student={student.Id} offering={requested}";
                var start = DateTimeOffset.UtcNow;
                var result = await send("POST", "/enrollments", new { studentId = student.Id, courseOfferingId = requested }, null);
                var end = DateTimeOffset.UtcNow;
                Equal(scenario == "success" ? 201 : scenario == "duplicate" ? 409 : 400, result.Status, context + " HTTP");
                int? createdId = null;
                if (scenario == "success")
                {
                    createdId = result.Body["id"]!.GetValue<int>();
                    Equal(student.Id, result.Body["studentId"]!.GetValue<int>(), context + " response student");
                    Equal(requested, result.Body["courseOfferingId"]!.GetValue<int>(), context + " response offering");
                    Equal($"/enrollments/{createdId}", result.Location, context + " Location");
                    var detail = await send("GET", result.Location!, null, null);
                    Equal(200, detail.Status, context + " detail status");
                    Equal(true, JsonNode.DeepEquals(result.Body, detail.Body), context + " detail identity");
                    var time = result.Body["createdAtUtc"]!.GetValue<DateTimeOffset>();
                    Equal(true, time.Offset == TimeSpan.Zero && time >= start && time <= end, context + " UTC interval");
                }
                else if (scenario == "missing-offering")
                    Equal(true, result.Body["errors"]?.AsObject().ContainsKey("courseOfferingId") == true, context + " error field");
                await using (var read = factory())
                {
                    var after = await read.Enrollments.OrderBy(e => e.Id).ToListAsync();
                    Equal(before.Length + (createdId.HasValue ? 1 : 0), after.Count, context + " enrollment count");
                    Equal(string.Join('\n', before), string.Join('\n', after.Where(e => e.Id != createdId).Select(Snapshot)), context + " existing records preserved");
                    Equal(scenario == "missing-offering" ? 0 : 1, after.Count(e => e.StudentId == student.Id && e.CourseOfferingId == requested), context + " pair count");
                    if (createdId.HasValue)
                    {
                        var saved = after.Single(e => e.Id == createdId);
                        Equal(student.Id, saved.StudentId, context + " DB student");
                        Equal(requested, saved.CourseOfferingId, context + " DB offering");
                        Equal(result.Body["createdAtUtc"]!.GetValue<DateTimeOffset>(), saved.CreatedAtUtc, context + " DB timestamp");
                    }
                    Equal(students, await read.Students.CountAsync(), context + " parent students");
                    Equal(offeringCount, await read.CourseOfferings.CountAsync(), context + " parent offerings");
                }
                Console.WriteLine("PASS: " + context);
            }
            catch (Exception ex)
            {
                failure = ex;
                if (ex is not BehaviorFailure)
                    Console.Error.WriteLine($"EXECUTION: {scenario}; student={student.Id}; type={ex.GetType().Name}");
                throw;
            }
            finally
            {
                try
                {
                    await using var clean = factory();
                    await clean.Enrollments.Where(e => e.StudentId == student.Id).ExecuteDeleteAsync();
                    // Also finds offerings if fixture SaveChanges succeeded before IDs were copied.
                    await clean.CourseOfferings.Where(o => o.CourseId == course.Id).ExecuteDeleteAsync();
                    await clean.Students.Where(s => s.Id == student.Id).ExecuteDeleteAsync();
                    await clean.AcademicTerms.Where(t => t.Id == term.Id).ExecuteDeleteAsync();
                    await clean.Courses.Where(c => c.Id == course.Id).ExecuteDeleteAsync();
                    Equal(0, await clean.Enrollments.CountAsync(e => e.StudentId == student.Id), scenario + " cleanup enrollments");
                    Console.WriteLine("PASS: cleanup " + scenario);
                }
                catch (Exception cleanup)
                {
                    Console.Error.WriteLine($"CLEANUP: {scenario}; type={cleanup.GetType().Name}");
                    if (failure is null) throw new CleanupFailure();
                }
            }
        }
    }
}
