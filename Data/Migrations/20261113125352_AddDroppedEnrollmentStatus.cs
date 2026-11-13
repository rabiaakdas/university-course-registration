using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityRegistration.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDroppedEnrollmentStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Enrollments_Status",
                table: "Enrollments");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Enrollments_Status",
                table: "Enrollments",
                sql: "[Status] IN (0, 1, 2, 3, 4)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Enrollments_Status",
                table: "Enrollments");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Enrollments_Status",
                table: "Enrollments",
                sql: "[Status] IN (0, 1, 2, 3)");
        }
    }
}
