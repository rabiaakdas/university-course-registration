using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace UniversityRegistration.Data;

public static class RegistrationWriteErrors
{
    // Provider supplies error numbers but no structured index/constraint names.
    // Unknown/localized message formats fail closed; never map all 547/2601 errors.
    public static string? Classify(DbUpdateException exception)
    {
        if (exception.InnerException is not SqlException sql) return null;
        foreach (SqlError error in sql.Errors)
        {
            if (error.Number == 2601)
            {
                var names = Regex.Matches(error.Message, "'([^']*)'");
                if (names.Count < 2) continue;
                if (names[0].Groups[1].Value == "dbo.Students" && names[1].Groups[1].Value == "UX_Students_StudentNumber") return "studentNumber";
                if (names[0].Groups[1].Value == "dbo.Enrollments" && names[1].Groups[1].Value == "UX_Enrollments_Student_Offering") return "duplicate";
            }
            if (error.Number == 547)
            {
                var names = Regex.Matches(error.Message, "\"([^\"]*)\"");
                if (names.Count == 0) continue;
                if (names[0].Groups[1].Value == "FK_Enrollments_Students") return "studentId";
                if (names[0].Groups[1].Value == "FK_Enrollments_CourseOfferings") return "courseOfferingId";
            }
        }
        return null;
    }
}
