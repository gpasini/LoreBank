using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoreBank.Probe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialProbeSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "probe");

            migrationBuilder.CreateTable(
                name: "probe_things",
                schema: "probe",
                columns: table => new {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_probe_things", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "probe_things",
                schema: "probe");
        }
    }
}
