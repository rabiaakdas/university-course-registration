using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace UniversityRegistration.Data;

public static class OfferingWriteErrors
{
    // SQL Server exposes Number, but not a structured constraint/index name.
    // Unknown message formats fail closed. CHECK 547 must never become an FK error.
    public static string? Classify(DbUpdateException exception)
    {
        if (exception.InnerException is not SqlException sql) return null;
        foreach (SqlError error in sql.Errors)
        {
            if (error.Number == 2601)
            {
                var names = Regex.Matches(error.Message, "'([^']*)'");
                if (names.Count >= 2 && names[0].Groups[1].Value == "dbo.CourseOfferings"
                    && names[1].Groups[1].Value == "UX_CourseOfferings_Course_Term_Section") return "duplicate";
            }
            if (error.Number == 547)
            {
                var names = Regex.Matches(error.Message, "\"([^\"]*)\"");
                if (names.Count == 0) continue;
                if (names[0].Groups[1].Value == "FK_CourseOfferings_Courses") return "courseId";
                if (names[0].Groups[1].Value == "FK_CourseOfferings_AcademicTerms") return "academicTermId";
            }
        }
        return null;
    }
}
