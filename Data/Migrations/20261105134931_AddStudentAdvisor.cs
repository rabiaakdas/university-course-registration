using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityRegistration.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentAdvisor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdvisorUserId",
                table: "Students",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Students_AdvisorUserId",
                table: "Students",
                column: "AdvisorUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Students_AdvisorUser",
                table: "Students",
                column: "AdvisorUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Students_AdvisorUser",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_Students_AdvisorUserId",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "AdvisorUserId",
                table: "Students");
        }
    }
}
