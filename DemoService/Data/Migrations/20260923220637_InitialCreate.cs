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
                name: "Hash",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    HashBytes = table.Column<byte[]>(type: "BLOB", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AddedBy = table.Column<string>(type: "TEXT", nullable: false),
                    GetCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Hash", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HelloRequest",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
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
                    table.PrimaryKey("PK_HelloRequest", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboundGet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CallerIp = table.Column<string>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    TargetHost = table.Column<string>(type: "TEXT", nullable: false),
                    TargetPathAndQuery = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboundGet", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HashGetEvent",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    HashId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GotAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    GotBy = table.Column<string>(type: "TEXT", nullable: false),
                    RequestHeaders = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HashGetEvent", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HashGetEvent_Hash_HashId",
                        column: x => x.HashId,
                        principalTable: "Hash",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HashGetEvent_HashId",
                table: "HashGetEvent",
                column: "HashId");

            migrationBuilder.CreateIndex(
                name: "IX_HelloRequest_CallerIp",
                table: "HelloRequest",
                column: "CallerIp");

            migrationBuilder.CreateIndex(
                name: "IX_HelloRequest_RequestedAt",
                table: "HelloRequest",
                column: "RequestedAt");

            migrationBuilder.CreateIndex(
                name: "IX_OutboundGet_TargetHost_RequestedAt",
                table: "OutboundGet",
                columns: new[] { "TargetHost", "RequestedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HashGetEvent");

            migrationBuilder.DropTable(
                name: "HelloRequest");

            migrationBuilder.DropTable(
                name: "OutboundGet");

            migrationBuilder.DropTable(
                name: "Hash");
        }
    }
}
