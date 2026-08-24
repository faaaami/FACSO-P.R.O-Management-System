using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PROManagementSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixClientCompanyColumnNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_client_entities_client_companies_ClientCompanyId",
                table: "client_entities");

            migrationBuilder.RenameColumn(
                name: "Emirate",
                table: "client_entities",
                newName: "emirate");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "client_entities",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "client_entities",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "TradeLicenseNumber",
                table: "client_entities",
                newName: "trade_license_number");

            migrationBuilder.RenameColumn(
                name: "IsDeleted",
                table: "client_entities",
                newName: "is_deleted");

            migrationBuilder.RenameColumn(
                name: "IsActive",
                table: "client_entities",
                newName: "is_active");

            migrationBuilder.RenameColumn(
                name: "EntityName",
                table: "client_entities",
                newName: "entity_name");

            migrationBuilder.RenameColumn(
                name: "DeletedAt",
                table: "client_entities",
                newName: "deleted_at");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "client_entities",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "ClientCompanyId",
                table: "client_entities",
                newName: "client_company_id");

            migrationBuilder.RenameIndex(
                name: "IX_client_entities_TradeLicenseNumber",
                table: "client_entities",
                newName: "IX_client_entities_trade_license_number");

            migrationBuilder.RenameIndex(
                name: "IX_client_entities_ClientCompanyId",
                table: "client_entities",
                newName: "IX_client_entities_client_company_id");

            migrationBuilder.RenameColumn(
                name: "Phone",
                table: "client_companies",
                newName: "phone");

            migrationBuilder.RenameColumn(
                name: "Emirate",
                table: "client_companies",
                newName: "emirate");

            migrationBuilder.RenameColumn(
                name: "Email",
                table: "client_companies",
                newName: "email");

            migrationBuilder.RenameColumn(
                name: "Address",
                table: "client_companies",
                newName: "address");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "client_companies",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "client_companies",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "TradeLicenseNumber",
                table: "client_companies",
                newName: "trade_license_number");

            migrationBuilder.RenameColumn(
                name: "IsDeleted",
                table: "client_companies",
                newName: "is_deleted");

            migrationBuilder.RenameColumn(
                name: "IsActive",
                table: "client_companies",
                newName: "is_active");

            migrationBuilder.RenameColumn(
                name: "DeletedAt",
                table: "client_companies",
                newName: "deleted_at");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "client_companies",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "CompanyName",
                table: "client_companies",
                newName: "company_name");

            migrationBuilder.AddForeignKey(
                name: "FK_client_entities_client_companies_client_company_id",
                table: "client_entities",
                column: "client_company_id",
                principalTable: "client_companies",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_client_entities_client_companies_client_company_id",
                table: "client_entities");

            migrationBuilder.RenameColumn(
                name: "emirate",
                table: "client_entities",
                newName: "Emirate");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "client_entities",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "client_entities",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "trade_license_number",
                table: "client_entities",
                newName: "TradeLicenseNumber");

            migrationBuilder.RenameColumn(
                name: "is_deleted",
                table: "client_entities",
                newName: "IsDeleted");

            migrationBuilder.RenameColumn(
                name: "is_active",
                table: "client_entities",
                newName: "IsActive");

            migrationBuilder.RenameColumn(
                name: "entity_name",
                table: "client_entities",
                newName: "EntityName");

            migrationBuilder.RenameColumn(
                name: "deleted_at",
                table: "client_entities",
                newName: "DeletedAt");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "client_entities",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "client_company_id",
                table: "client_entities",
                newName: "ClientCompanyId");

            migrationBuilder.RenameIndex(
                name: "IX_client_entities_trade_license_number",
                table: "client_entities",
                newName: "IX_client_entities_TradeLicenseNumber");

            migrationBuilder.RenameIndex(
                name: "IX_client_entities_client_company_id",
                table: "client_entities",
                newName: "IX_client_entities_ClientCompanyId");

            migrationBuilder.RenameColumn(
                name: "phone",
                table: "client_companies",
                newName: "Phone");

            migrationBuilder.RenameColumn(
                name: "emirate",
                table: "client_companies",
                newName: "Emirate");

            migrationBuilder.RenameColumn(
                name: "email",
                table: "client_companies",
                newName: "Email");

            migrationBuilder.RenameColumn(
                name: "address",
                table: "client_companies",
                newName: "Address");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "client_companies",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "client_companies",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "trade_license_number",
                table: "client_companies",
                newName: "TradeLicenseNumber");

            migrationBuilder.RenameColumn(
                name: "is_deleted",
                table: "client_companies",
                newName: "IsDeleted");

            migrationBuilder.RenameColumn(
                name: "is_active",
                table: "client_companies",
                newName: "IsActive");

            migrationBuilder.RenameColumn(
                name: "deleted_at",
                table: "client_companies",
                newName: "DeletedAt");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "client_companies",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "company_name",
                table: "client_companies",
                newName: "CompanyName");

            migrationBuilder.AddForeignKey(
                name: "FK_client_entities_client_companies_ClientCompanyId",
                table: "client_entities",
                column: "ClientCompanyId",
                principalTable: "client_companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
