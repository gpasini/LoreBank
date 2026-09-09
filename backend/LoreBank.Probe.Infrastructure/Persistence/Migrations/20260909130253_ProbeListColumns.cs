using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoreBank.Probe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProbeListColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "active",
                schema: "probe",
                table: "probe_things",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "kind",
                schema: "probe",
                table: "probe_things",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "active",
                schema: "probe",
                table: "probe_things");

            migrationBuilder.DropColumn(
                name: "kind",
                schema: "probe",
                table: "probe_things");
        }
    }
}
