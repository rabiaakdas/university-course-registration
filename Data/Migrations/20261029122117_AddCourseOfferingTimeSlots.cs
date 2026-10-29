using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityRegistration.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCourseOfferingTimeSlots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence<int>(
                name: "CourseOfferingTimeSlotIds",
                schema: "dbo",
                minValue: 1L);

            migrationBuilder.CreateTable(
                name: "CourseOfferingTimeSlots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false, defaultValueSql: "NEXT VALUE FOR [dbo].[CourseOfferingTimeSlotIds]")
                        .Annotation("Relational:DefaultConstraintName", "DF_CourseOfferingTimeSlots_Id"),
                    CourseOfferingId = table.Column<int>(type: "int", nullable: false),
                    Day = table.Column<int>(type: "int", nullable: false),
                    StartsAt = table.Column<TimeOnly>(type: "time(7)", nullable: false),
                    EndsAt = table.Column<TimeOnly>(type: "time(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseOfferingTimeSlots", x => x.Id);
                    table.CheckConstraint("CK_CourseOfferingTimeSlots_Day", "[Day] BETWEEN 0 AND 6");
                    table.CheckConstraint("CK_CourseOfferingTimeSlots_Range", "[StartsAt] < [EndsAt]");
                    table.ForeignKey(
                        name: "FK_CourseOfferingTimeSlots_CourseOfferings",
                        column: x => x.CourseOfferingId,
                        principalTable: "CourseOfferings",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "UX_CourseOfferingTimeSlots_Offering_Day_Range",
                table: "CourseOfferingTimeSlots",
                columns: new[] { "CourseOfferingId", "Day", "StartsAt", "EndsAt" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CourseOfferingTimeSlots");

            migrationBuilder.DropSequence(
                name: "CourseOfferingTimeSlotIds",
                schema: "dbo");
        }
    }
}
