using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace UniversityRegistration.Data;

public static class CourseWriteErrors
{
    public static bool IsDuplicateCode(DbUpdateException exception)
    {
        if (exception.InnerException is not SqlException sql) return false;
        foreach (SqlError error in sql.Errors)
        {
            if (error.Number != 2601) continue; // Not PK (2627), CHECK/FK (547), etc.
            // SqlError exposes Number but no structured index name. SQL Server 2601
            // quotes the object and index first. Unknown/localized formats fail closed.
            var identifiers = Regex.Matches(error.Message, "'([^']*)'");
            if (identifiers.Count >= 2 && identifiers[0].Groups[1].Value == "dbo.Courses"
                && identifiers[1].Groups[1].Value == "UX_Courses_Code") return true;
        }
        return false;
    }
}
