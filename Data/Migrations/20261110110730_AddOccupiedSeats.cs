using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityRegistration.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOccupiedSeats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OccupiedSeats",
                table: "CourseOfferings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Fail rather than silently repair historical over-capacity registrations.
            // Run with application writers stopped; all operations retain the migration transaction.
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT o.Id FROM dbo.CourseOfferings o
                    JOIN dbo.Enrollments e ON e.CourseOfferingId = o.Id
                    WHERE e.Status IN (1, 2)
                    GROUP BY o.Id, o.Capacity
                    HAVING COUNT_BIG(*) > o.Capacity
                )
                    THROW 51037, 'Existing active enrollments exceed capacity; migration aborted.', 1;

                UPDATE o SET OccupiedSeats = CONVERT(int, x.ActiveCount)
                FROM dbo.CourseOfferings o
                CROSS APPLY (
                    SELECT COUNT_BIG(*) AS ActiveCount FROM dbo.Enrollments e
                    WHERE e.CourseOfferingId = o.Id AND e.Status IN (1, 2)
                ) x;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_CourseOfferings_OccupiedSeats",
                table: "CourseOfferings",
                sql: "[OccupiedSeats] >= 0 AND [OccupiedSeats] <= [Capacity]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_CourseOfferings_OccupiedSeats",
                table: "CourseOfferings");

            migrationBuilder.DropColumn(
                name: "OccupiedSeats",
                table: "CourseOfferings");
        }
    }
}
