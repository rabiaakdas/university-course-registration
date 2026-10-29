using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityRegistration.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEnrollmentLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "Enrollments",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Enrollments",
                type: "int",
                nullable: false,
                defaultValue: 0)
                .Annotation("Relational:DefaultConstraintName", "DF_Enrollments_Status");

            // Writes must be stopped: preserve the old direct-registration meaning, not an approval audit.
            migrationBuilder.Sql("UPDATE [Enrollments] SET [Status] = 2;");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Enrollments_RejectionReason",
                table: "Enrollments",
                sql: "([Status] = 3 AND [RejectionReason] IS NOT NULL AND LEN(TRIM(NCHAR(9)+NCHAR(10)+NCHAR(11)+NCHAR(12)+NCHAR(13)+NCHAR(32)+NCHAR(133)+NCHAR(160)+NCHAR(5760)+NCHAR(8192)+NCHAR(8193)+NCHAR(8194)+NCHAR(8195)+NCHAR(8196)+NCHAR(8197)+NCHAR(8198)+NCHAR(8199)+NCHAR(8200)+NCHAR(8201)+NCHAR(8202)+NCHAR(8232)+NCHAR(8233)+NCHAR(8239)+NCHAR(8287)+NCHAR(12288) FROM [RejectionReason])) > 0) OR ([Status] <> 3 AND [RejectionReason] IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Enrollments_Status",
                table: "Enrollments",
                sql: "[Status] IN (0, 1, 2, 3)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Enrollments_RejectionReason",
                table: "Enrollments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Enrollments_Status",
                table: "Enrollments");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "Enrollments");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Enrollments")
                .Annotation("Relational:DefaultConstraintName", "DF_Enrollments_Status");
        }
    }
}
