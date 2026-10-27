using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityRegistration.Data.Migrations
{
    /// <inheritdoc />
    public partial class GenerateCourseIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.CreateSequence<int>(
                name: "CourseIds",
                schema: "dbo",
                minValue: 1L);

            // Apply with all writers stopped. This is one-time initialization,
            // not application-side MAX(Id)+1 allocation. The migration transaction
            // holds the table lock until the sequence/default are ready.
            migrationBuilder.Sql("""
                DECLARE @maximum bigint;
                SELECT @maximum = COALESCE(MAX(CONVERT(bigint, Id)), 0)
                FROM dbo.Courses WITH (TABLOCKX, HOLDLOCK);
                IF @maximum >= 2147483647
                    THROW 51000, 'Course Id int range exhausted.', 1;
                DECLARE @next bigint = CASE WHEN @maximum < 0 THEN 1 ELSE @maximum + 1 END;
                DECLARE @sql nvarchar(200) = N'ALTER SEQUENCE dbo.CourseIds RESTART WITH '
                    + CONVERT(nvarchar(20), @next);
                EXEC sys.sp_executesql @sql;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Courses",
                type: "int",
                nullable: false,
                defaultValueSql: "NEXT VALUE FOR [dbo].[CourseIds]",
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("Relational:DefaultConstraintName", "DF_Courses_Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Courses",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValueSql: "NEXT VALUE FOR [dbo].[CourseIds]")
                .OldAnnotation("Relational:DefaultConstraintName", "DF_Courses_Id");

            migrationBuilder.DropSequence(
                name: "CourseIds",
                schema: "dbo");
        }
    }
}
