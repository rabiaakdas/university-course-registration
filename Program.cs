using Microsoft.EntityFrameworkCore;
using UniversityRegistration.Data;
using UniversityRegistration.Models;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("UniversityDatabase");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("ConnectionStrings:UniversityDatabase bağlantısı yapılandırılmalıdır.");
builder.Services.AddDbContext<UniversityDbContext>(options => options.UseSqlServer(connectionString));
var app = builder.Build();

// Geçici olarak yalnızca /course-offerings yanıtını zenginleştiren bellek verisi.
var offeringCourseSamples = new Course[]
{
    new() { Id = 1, Code = "CSE201", Name = "Veri Yapıları", Credits = 4 },
    new() { Id = 2, Code = "CSE203", Name = "Veritabanı Yönetimi", Credits = 3 }
};

var academicTerms = new AcademicTerm[]
{
    new() { Id = 1, Name = "2026-2027 Güz", StartsOn = new DateOnly(2026, 9, 21), EndsOn = new DateOnly(2027, 1, 15) },
    new() { Id = 2, Name = "2026-2027 Bahar", StartsOn = new DateOnly(2027, 2, 15), EndsOn = new DateOnly(2027, 6, 11) }
};

var courseOfferings = new CourseOffering[]
{
    new() { Id = 1, CourseId = 1, AcademicTermId = 1, SectionCode = "A", Capacity = 40 },
    new() { Id = 2, CourseId = 1, AcademicTermId = 1, SectionCode = "B", Capacity = 30 },
    new() { Id = 3, CourseId = 1, AcademicTermId = 2, SectionCode = "A", Capacity = 35 }
};

app.MapGet("/courses", async (UniversityDbContext db, CancellationToken cancellationToken) =>
{
    var courses = await db.Courses.OrderBy(course => course.Id)
        .Select(course => new { course.Id, course.Code, course.Name })
        .ToListAsync(cancellationToken);
    return Results.Ok(courses);
});

app.MapGet("/academic-terms", () => Results.Ok(academicTerms));

app.MapGet("/course-offerings", () => Results.Ok(courseOfferings.Select(offering =>
{
    var course = offeringCourseSamples.Single(course => course.Id == offering.CourseId);
    var term = academicTerms.Single(term => term.Id == offering.AcademicTermId);

    return new
    {
        offering.Id,
        offering.CourseId,
        CourseCode = course.Code,
        CourseName = course.Name,
        offering.AcademicTermId,
        TermName = term.Name,
        offering.SectionCode,
        offering.Capacity
    };
})));

app.Run();
