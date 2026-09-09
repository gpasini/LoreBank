using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoreBank.Bank.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBankAccountOpenedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "opened_at",
                schema: "bank",
                table: "bank_accounts",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "opened_at",
                schema: "bank",
                table: "bank_accounts");
        }
    }
}
