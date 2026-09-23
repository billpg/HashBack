using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DemoService.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HelloRequestLogs",
                columns: table => new
                {
                    RecordId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RequestedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CallerIp = table.Column<string>(type: "TEXT", nullable: false),
                    ClaimVersion = table.Column<string>(type: "TEXT", nullable: true),
                    ClaimHost = table.Column<string>(type: "TEXT", nullable: true),
                    ClaimNow = table.Column<long>(type: "INTEGER", nullable: true),
                    ClaimUnus = table.Column<string>(type: "TEXT", nullable: true),
                    ClaimVerify = table.Column<string>(type: "TEXT", nullable: true),
                    VerificationIp = table.Column<string>(type: "TEXT", nullable: true),
                    Outcome = table.Column<string>(type: "TEXT", nullable: false),
                    Detail = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HelloRequestLogs", x => x.RecordId);
                });

            migrationBuilder.CreateTable(
                name: "OutboundGetLogs",
                columns: table => new
                {
                    RecordId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RequestedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CallerIp = table.Column<string>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    TargetHost = table.Column<string>(type: "TEXT", nullable: false),
                    TargetPathAndQuery = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboundGetLogs", x => x.RecordId);
                });

            migrationBuilder.CreateTable(
                name: "StoredHashes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Hash = table.Column<byte[]>(type: "BLOB", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AddedBy = table.Column<string>(type: "TEXT", nullable: false),
                    GetCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoredHashes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HashGetEvents",
                columns: table => new
                {
                    RecordId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HashId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GotAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    GotBy = table.Column<string>(type: "TEXT", nullable: false),
                    RequestHeaders = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HashGetEvents", x => x.RecordId);
                    table.ForeignKey(
                        name: "FK_HashGetEvents_StoredHashes_HashId",
                        column: x => x.HashId,
                        principalTable: "StoredHashes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HashGetEvents_HashId",
                table: "HashGetEvents",
                column: "HashId");

            migrationBuilder.CreateIndex(
                name: "IX_HelloRequestLogs_CallerIp",
                table: "HelloRequestLogs",
                column: "CallerIp");

            migrationBuilder.CreateIndex(
                name: "IX_HelloRequestLogs_RequestedAt",
                table: "HelloRequestLogs",
                column: "RequestedAt");

            migrationBuilder.CreateIndex(
                name: "IX_OutboundGetLogs_TargetHost_RequestedAt",
                table: "OutboundGetLogs",
                columns: new[] { "TargetHost", "RequestedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HashGetEvents");

            migrationBuilder.DropTable(
                name: "HelloRequestLogs");

            migrationBuilder.DropTable(
                name: "OutboundGetLogs");

            migrationBuilder.DropTable(
                name: "StoredHashes");
        }
    }
}
