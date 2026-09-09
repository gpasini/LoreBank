using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoreBank.Ledger.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddJournalEntryRecordedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "recorded_at",
                schema: "ledger",
                table: "journal_entries",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "recorded_at",
                schema: "ledger",
                table: "journal_entries");
        }
    }
}
