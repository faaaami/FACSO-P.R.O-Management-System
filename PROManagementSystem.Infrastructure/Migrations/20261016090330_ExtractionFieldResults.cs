using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PROManagementSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExtractionFieldResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Confidence",
                table: "document_extractions");

            migrationBuilder.AddColumn<string>(
                name: "failure_code",
                table: "document_extractions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "failure_message",
                table: "document_extractions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "field_results",
                table: "document_extractions",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{}'::jsonb");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "failure_code",
                table: "document_extractions");

            migrationBuilder.DropColumn(
                name: "failure_message",
                table: "document_extractions");

            migrationBuilder.DropColumn(
                name: "field_results",
                table: "document_extractions");

            migrationBuilder.AddColumn<decimal>(
                name: "Confidence",
                table: "document_extractions",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: false,
                defaultValue: 0m);
        }
    }
}
