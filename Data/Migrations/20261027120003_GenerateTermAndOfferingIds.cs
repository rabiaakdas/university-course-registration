using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityRegistration.Data.Migrations
{
    /// <inheritdoc />
    public partial class GenerateTermAndOfferingIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence<int>(
                name: "AcademicTermIds",
                schema: "dbo",
                minValue: 1L);

            migrationBuilder.CreateSequence<int>(
                name: "CourseOfferingIds",
                schema: "dbo",
                minValue: 1L);

            // Stop writers before applying. Locks last for the migration transaction.
            migrationBuilder.Sql("""
                DECLARE @termMax bigint, @offeringMax bigint;
                SELECT @termMax=COALESCE(MAX(CONVERT(bigint,Id)),0) FROM dbo.AcademicTerms WITH (TABLOCKX,HOLDLOCK);
                SELECT @offeringMax=COALESCE(MAX(CONVERT(bigint,Id)),0) FROM dbo.CourseOfferings WITH (TABLOCKX,HOLDLOCK);
                IF @termMax>=2147483647 OR @offeringMax>=2147483647
                    THROW 51000, 'Term/offering Id int range exhausted.', 1;
                DECLARE @termNext bigint=CASE WHEN @termMax<0 THEN 1 ELSE @termMax+1 END;
                DECLARE @offeringNext bigint=CASE WHEN @offeringMax<0 THEN 1 ELSE @offeringMax+1 END;
                DECLARE @sql nvarchar(300)=N'ALTER SEQUENCE dbo.AcademicTermIds RESTART WITH '+CONVERT(nvarchar(20),@termNext)
                    +N'; ALTER SEQUENCE dbo.CourseOfferingIds RESTART WITH '+CONVERT(nvarchar(20),@offeringNext);
                EXEC sys.sp_executesql @sql;
                """);
            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "CourseOfferings",
                type: "int",
                nullable: false,
                defaultValueSql: "NEXT VALUE FOR [dbo].[CourseOfferingIds]",
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("Relational:DefaultConstraintName", "DF_CourseOfferings_Id");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "AcademicTerms",
                type: "int",
                nullable: false,
                defaultValueSql: "NEXT VALUE FOR [dbo].[AcademicTermIds]",
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("Relational:DefaultConstraintName", "DF_AcademicTerms_Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "CourseOfferings",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValueSql: "NEXT VALUE FOR [dbo].[CourseOfferingIds]")
                .OldAnnotation("Relational:DefaultConstraintName", "DF_CourseOfferings_Id");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "AcademicTerms",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValueSql: "NEXT VALUE FOR [dbo].[AcademicTermIds]")
                .OldAnnotation("Relational:DefaultConstraintName", "DF_AcademicTerms_Id");
            migrationBuilder.DropSequence(
                name: "AcademicTermIds",
                schema: "dbo");

            migrationBuilder.DropSequence(
                name: "CourseOfferingIds",
                schema: "dbo");
        }
    }
}
