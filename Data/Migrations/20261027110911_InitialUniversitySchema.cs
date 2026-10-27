using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityRegistration.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialUniversitySchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AcademicTerms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StartsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    EndsOn = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcademicTerms", x => x.Id);
                    table.CheckConstraint("CK_AcademicTerms_DateRange", "[EndsOn] >= [StartsOn]");
                });

            migrationBuilder.CreateTable(
                name: "Courses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Credits = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Courses", x => x.Id);
                    table.CheckConstraint("CK_Courses_Credits_Positive", "[Credits] > 0");
                });

            migrationBuilder.CreateTable(
                name: "CourseOfferings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    CourseId = table.Column<int>(type: "int", nullable: false),
                    AcademicTermId = table.Column<int>(type: "int", nullable: false),
                    SectionCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseOfferings", x => x.Id);
                    table.CheckConstraint("CK_CourseOfferings_Capacity_Positive", "[Capacity] > 0");
                    table.ForeignKey(
                        name: "FK_CourseOfferings_AcademicTerms",
                        column: x => x.AcademicTermId,
                        principalTable: "AcademicTerms",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CourseOfferings_Courses",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CourseOfferings_AcademicTermId",
                table: "CourseOfferings",
                column: "AcademicTermId");

            migrationBuilder.CreateIndex(
                name: "UX_CourseOfferings_Course_Term_Section",
                table: "CourseOfferings",
                columns: new[] { "CourseId", "AcademicTermId", "SectionCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Courses_Code",
                table: "Courses",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CourseOfferings");

            migrationBuilder.DropTable(
                name: "AcademicTerms");

            migrationBuilder.DropTable(
                name: "Courses");
        }
    }
}
