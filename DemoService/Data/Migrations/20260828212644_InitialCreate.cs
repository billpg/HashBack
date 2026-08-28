using System;
using System.Net;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

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
                name: "StoredHashes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AddedBy = table.Column<IPAddress>(type: "inet", nullable: false),
                    GetCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoredHashes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HashGetEvents",
                columns: table => new
                {
                    RecordId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HashId = table.Column<Guid>(type: "uuid", nullable: false),
                    GotAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GotBy = table.Column<IPAddress>(type: "inet", nullable: false),
                    RequestHeaders = table.Column<string>(type: "text", nullable: false)
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HashGetEvents");

            migrationBuilder.DropTable(
                name: "StoredHashes");
        }
    }
}
