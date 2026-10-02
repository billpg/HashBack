using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DemoService.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHashSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            /* Every row that already exists was necessarily added before this column did,
             * which only happened once Source started being tracked at all - so "Put" (the
             * ordinary, pre-existing case of an external caller publishing directly) is the
             * correct backfill, not an arbitrary guess. An empty-string default would throw
             * on read the next time any pre-existing row is loaded, since "" doesn't match
             * either enum value. */
            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "Hash",
                type: "TEXT",
                nullable: false,
                defaultValue: "Put");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Source",
                table: "Hash");
        }
    }
}
