using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoreBank.Bank.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBankAccountOpenedBy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "opened_by",
                schema: "bank",
                table: "bank_accounts",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "opened_by",
                schema: "bank",
                table: "bank_accounts");
        }
    }
}
