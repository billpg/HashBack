using System;
using System.Net;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DemoService.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHelloRequestLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HelloRequestLogs",
                columns: table => new
                {
                    RecordId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CallerIp = table.Column<IPAddress>(type: "inet", nullable: false),
                    ClaimVersion = table.Column<string>(type: "text", nullable: true),
                    ClaimHost = table.Column<string>(type: "text", nullable: true),
                    ClaimNow = table.Column<long>(type: "bigint", nullable: true),
                    ClaimUnus = table.Column<string>(type: "text", nullable: true),
                    ClaimVerify = table.Column<string>(type: "text", nullable: true),
                    VerificationIp = table.Column<IPAddress>(type: "inet", nullable: true),
                    Outcome = table.Column<string>(type: "text", nullable: false),
                    Detail = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HelloRequestLogs", x => x.RecordId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HelloRequestLogs_CallerIp",
                table: "HelloRequestLogs",
                column: "CallerIp");

            migrationBuilder.CreateIndex(
                name: "IX_HelloRequestLogs_RequestedAt",
                table: "HelloRequestLogs",
                column: "RequestedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HelloRequestLogs");
        }
    }
}
