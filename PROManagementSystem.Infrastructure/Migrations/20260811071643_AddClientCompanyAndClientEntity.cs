using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PROManagementSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClientCompanyAndClientEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "client_companies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TradeLicenseNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Emirate = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_companies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "client_entities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientCompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TradeLicenseNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Emirate = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_entities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_client_entities_client_companies_ClientCompanyId",
                        column: x => x.ClientCompanyId,
                        principalTable: "client_companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_users_client_company_id",
                table: "users",
                column: "client_company_id");

            migrationBuilder.CreateIndex(
                name: "IX_client_entities_ClientCompanyId",
                table: "client_entities",
                column: "ClientCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_client_entities_TradeLicenseNumber",
                table: "client_entities",
                column: "TradeLicenseNumber");

            migrationBuilder.AddForeignKey(
                name: "FK_users_client_companies_client_company_id",
                table: "users",
                column: "client_company_id",
                principalTable: "client_companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_users_client_companies_client_company_id",
                table: "users");

            migrationBuilder.DropTable(
                name: "client_entities");

            migrationBuilder.DropTable(
                name: "client_companies");

            migrationBuilder.DropIndex(
                name: "IX_users_client_company_id",
                table: "users");
        }
    }
}
