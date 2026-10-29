using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityRegistration.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPrerequisitesAndCompletedCourses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CoursePrerequisites",
                columns: table => new
                {
                    CourseId = table.Column<int>(type: "int", nullable: false),
                    PrerequisiteCourseId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoursePrerequisites", x => new { x.CourseId, x.PrerequisiteCourseId });
                    table.CheckConstraint("CK_CoursePrerequisites_NotSelf", "[CourseId] <> [PrerequisiteCourseId]");
                    table.ForeignKey(
                        name: "FK_CoursePrerequisites_Courses",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CoursePrerequisites_PrerequisiteCourses",
                        column: x => x.PrerequisiteCourseId,
                        principalTable: "Courses",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "StudentCompletedCourses",
                columns: table => new
                {
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    CourseId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentCompletedCourses", x => new { x.StudentId, x.CourseId });
                    table.ForeignKey(
                        name: "FK_StudentCompletedCourses_Courses",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StudentCompletedCourses_Students",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CoursePrerequisites_PrerequisiteCourseId",
                table: "CoursePrerequisites",
                column: "PrerequisiteCourseId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentCompletedCourses_CourseId",
                table: "StudentCompletedCourses",
                column: "CourseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CoursePrerequisites");

            migrationBuilder.DropTable(
                name: "StudentCompletedCourses");
        }
    }
}
