using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityRegistration.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOfferingCapacityReportView : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // CREATE VIEW must be the first statement in its own SQL Server batch.
            migrationBuilder.Sql("""
                CREATE VIEW dbo.OfferingCapacityReport AS
                SELECT o.Id AS OfferingId, o.AcademicTermId, o.Capacity,
                    COALESCE(a.ActiveCount, CONVERT(bigint,0)) AS CalculatedActiveCount,
                    o.OccupiedSeats AS StoredOccupiedSeats
                FROM dbo.CourseOfferings o
                LEFT JOIN (
                    SELECT CourseOfferingId, COUNT_BIG(*) AS ActiveCount
                    FROM dbo.Enrollments WHERE Status IN (1,2)
                    GROUP BY CourseOfferingId
                ) a ON a.CourseOfferingId=o.Id
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW dbo.OfferingCapacityReport");
        }
    }
}
