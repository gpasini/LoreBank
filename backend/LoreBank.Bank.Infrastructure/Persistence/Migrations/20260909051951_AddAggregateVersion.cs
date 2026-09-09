using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoreBank.Bank.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAggregateVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "version",
                schema: "bank",
                table: "bank_accounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "version",
                schema: "bank",
                table: "bank_accounts");
        }
    }
}
