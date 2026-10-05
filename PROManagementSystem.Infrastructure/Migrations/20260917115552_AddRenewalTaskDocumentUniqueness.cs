using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PROManagementSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRenewalTaskDocumentUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_renewal_tasks_DocumentId",
                table: "renewal_tasks");

            migrationBuilder.CreateIndex(
                name: "IX_renewal_tasks_DocumentId",
                table: "renewal_tasks",
                column: "DocumentId",
                unique: true,
                filter: "\"IsDeleted\" = false AND \"Status\" != 6");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_renewal_tasks_DocumentId",
                table: "renewal_tasks");

            migrationBuilder.CreateIndex(
                name: "IX_renewal_tasks_DocumentId",
                table: "renewal_tasks",
                column: "DocumentId");
        }
    }
}
