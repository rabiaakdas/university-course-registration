using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityRegistration.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWaitlistEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "WaitlistQueueNumbers");

            migrationBuilder.CreateTable(
                name: "WaitlistEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    CourseOfferingId = table.Column<int>(type: "int", nullable: false),
                    QueueNumber = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "NEXT VALUE FOR [WaitlistQueueNumbers]"),
                    JoinedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ClosureReason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WaitlistEntries", x => x.Id);
                    table.CheckConstraint("CK_WaitlistEntries_Closure", "([Status] = 0 AND [ClosedAtUtc] IS NULL AND [ClosureReason] IS NULL) OR ([Status] IN (1,2,3) AND [ClosedAtUtc] IS NOT NULL AND [ClosedAtUtc] >= [JoinedAtUtc] AND [ClosureReason] IS NOT NULL AND LEN(LTRIM(RTRIM([ClosureReason]))) > 0)");
                    table.CheckConstraint("CK_WaitlistEntries_QueueNumber", "[QueueNumber] > 0");
                    table.ForeignKey(
                        name: "FK_WaitlistEntries_CourseOfferings_CourseOfferingId",
                        column: x => x.CourseOfferingId,
                        principalTable: "CourseOfferings",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WaitlistEntries_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_WaitlistEntries_CourseOfferingId_Status_QueueNumber",
                table: "WaitlistEntries",
                columns: new[] { "CourseOfferingId", "Status", "QueueNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_WaitlistEntries_QueueNumber",
                table: "WaitlistEntries",
                column: "QueueNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_WaitlistEntries_Waiting_Student_Offering",
                table: "WaitlistEntries",
                columns: new[] { "StudentId", "CourseOfferingId" },
                unique: true,
                filter: "[Status] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WaitlistEntries");

            migrationBuilder.DropSequence(
                name: "WaitlistQueueNumbers");
        }
    }
}
