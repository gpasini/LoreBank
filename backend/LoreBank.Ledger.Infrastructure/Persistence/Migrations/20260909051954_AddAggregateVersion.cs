using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoreBank.Ledger.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAggregateVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "version",
                schema: "ledger",
                table: "journal_entries",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "version",
                schema: "ledger",
                table: "journal_entries");
        }
    }
}
