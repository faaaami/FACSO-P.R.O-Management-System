using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PROManagementSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatedByToDocumentExtractions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                table: "document_extractions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_document_extractions_CreatedBy",
                table: "document_extractions",
                column: "CreatedBy");

            migrationBuilder.AddForeignKey(
                name: "FK_document_extractions_users_CreatedBy",
                table: "document_extractions",
                column: "CreatedBy",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_document_extractions_users_CreatedBy",
                table: "document_extractions");

            migrationBuilder.DropIndex(
                name: "IX_document_extractions_CreatedBy",
                table: "document_extractions");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "document_extractions");
        }
    }
}
