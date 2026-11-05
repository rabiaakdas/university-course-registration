using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityRegistration.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRefreshTokenHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RefreshTokenRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    UsedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: true),
                    ReplacedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokenRecords", x => x.Id);
                    table.UniqueConstraint("AK_RefreshTokenRecords_SessionId_Id", x => new { x.SessionId, x.Id });
                    table.CheckConstraint("CK_RefreshTokens_Replacement", "[ReplacedById] IS NULL OR ([UsedAtUtc] IS NOT NULL AND [ReplacedById] <> [Id])");
                    table.CheckConstraint("CK_RefreshTokens_Used", "[UsedAtUtc] IS NULL OR [UsedAtUtc] >= [CreatedAtUtc]");
                    table.ForeignKey(
                        name: "FK_RefreshTokenRecords_RefreshSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "RefreshSessions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RefreshTokenRecords_RefreshTokenRecords_SessionId_ReplacedById",
                        columns: x => new { x.SessionId, x.ReplacedById },
                        principalTable: "RefreshTokenRecords",
                        principalColumns: new[] { "SessionId", "Id" });
                });

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokenRecords_SessionId_ReplacedById",
                table: "RefreshTokenRecords",
                columns: new[] { "SessionId", "ReplacedById" });

            migrationBuilder.CreateIndex(
                name: "UX_RefreshTokens_Hash",
                table: "RefreshTokenRecords",
                column: "TokenHash",
                unique: true);
            migrationBuilder.Sql("""
                INSERT INTO RefreshTokenRecords (Id, SessionId, TokenHash, CreatedAtUtc)
                SELECT NEWID(), Id, RefreshTokenHash, CreatedAtUtc FROM RefreshSessions;
                """);
            migrationBuilder.DropIndex(name: "UX_RefreshSessions_TokenHash", table: "RefreshSessions");
            migrationBuilder.DropColumn(name: "RefreshTokenHash", table: "RefreshSessions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Downgrade loses history. Preserve one latest token per family, never revive revoked families.
            migrationBuilder.AddColumn<byte[]>(name: "RefreshTokenHash", table: "RefreshSessions", type: "binary(32)", nullable: true);
            migrationBuilder.Sql("""
                UPDATE s SET RefreshTokenHash = t.TokenHash FROM RefreshSessions s
                CROSS APPLY (SELECT TOP (1) TokenHash FROM RefreshTokenRecords r WHERE r.SessionId=s.Id
                    ORDER BY CASE WHEN UsedAtUtc IS NULL THEN 0 ELSE 1 END, CreatedAtUtc DESC, Id DESC) t;
                IF EXISTS (SELECT 1 FROM RefreshSessions WHERE RefreshTokenHash IS NULL)
                    THROW 51000, 'Cannot downgrade a session without token history.', 1;
                """);
            migrationBuilder.AlterColumn<byte[]>(name: "RefreshTokenHash", table: "RefreshSessions", type: "binary(32)", nullable: false,
                oldClrType: typeof(byte[]), oldType: "binary(32)", oldNullable: true);
            migrationBuilder.CreateIndex(name: "UX_RefreshSessions_TokenHash", table: "RefreshSessions", column: "RefreshTokenHash", unique: true);
            migrationBuilder.DropTable(name: "RefreshTokenRecords");
        }
    }
}
