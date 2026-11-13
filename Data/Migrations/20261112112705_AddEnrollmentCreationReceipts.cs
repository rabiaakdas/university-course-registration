using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityRegistration.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEnrollmentCreationReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EnrollmentCreationReceipts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Operation = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    Key = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestHash = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    EnrollmentId = table.Column<int>(type: "int", nullable: true),
                    StatusCode = table.Column<int>(type: "int", nullable: true),
                    ResponseJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Location = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnrollmentCreationReceipts", x => x.Id);
                    table.CheckConstraint("CK_EnrollmentReceipts_Expiry", "[ExpiresAtUtc] > [CreatedAtUtc]");
                    table.CheckConstraint("CK_EnrollmentReceipts_Result", "([EnrollmentId] IS NULL AND [StatusCode] IS NULL AND [ResponseJson] IS NULL AND [Location] IS NULL) OR ([EnrollmentId] IS NOT NULL AND [StatusCode] IS NOT NULL AND [StatusCode] = 201 AND [ResponseJson] IS NOT NULL AND ISJSON([ResponseJson]) = 1 AND [Location] IS NOT NULL AND [Location] LIKE '/enrollments/%')");
                    table.ForeignKey(
                        name: "FK_EnrollmentCreationReceipts_AspNetUsers_AccountId",
                        column: x => x.AccountId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EnrollmentCreationReceipts_Enrollments_EnrollmentId",
                        column: x => x.EnrollmentId,
                        principalTable: "Enrollments",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_EnrollmentCreationReceipts_EnrollmentId",
                table: "EnrollmentCreationReceipts",
                column: "EnrollmentId");

            migrationBuilder.CreateIndex(
                name: "UX_EnrollmentReceipts_Account_Operation_Key",
                table: "EnrollmentCreationReceipts",
                columns: new[] { "AccountId", "Operation", "Key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EnrollmentCreationReceipts");
        }
    }
}
