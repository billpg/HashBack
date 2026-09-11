using System;
using System.Net;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DemoService.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboundGetLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OutboundGetLogs",
                columns: table => new
                {
                    RecordId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CallerIp = table.Column<IPAddress>(type: "inet", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    TargetHost = table.Column<string>(type: "text", nullable: false),
                    TargetPathAndQuery = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboundGetLogs", x => x.RecordId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OutboundGetLogs_TargetHost_RequestedAt",
                table: "OutboundGetLogs",
                columns: new[] { "TargetHost", "RequestedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OutboundGetLogs");
        }
    }
}
