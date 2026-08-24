using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PROManagementSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGovFeeDisbursements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "gov_fee_disbursements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientCompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    RenewalTaskId = table.Column<Guid>(type: "uuid", nullable: true),
                    FeeDescription = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    GovernmentReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PaidByFirmAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    InvoicedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReimbursedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gov_fee_disbursements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_gov_fee_disbursements_client_companies_ClientCompanyId",
                        column: x => x.ClientCompanyId,
                        principalTable: "client_companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_gov_fee_disbursements_renewal_tasks_RenewalTaskId",
                        column: x => x.RenewalTaskId,
                        principalTable: "renewal_tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_gov_fee_disbursements_ClientCompanyId",
                table: "gov_fee_disbursements",
                column: "ClientCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_gov_fee_disbursements_RenewalTaskId",
                table: "gov_fee_disbursements",
                column: "RenewalTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_gov_fee_disbursements_Status",
                table: "gov_fee_disbursements",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "gov_fee_disbursements");
        }
    }
}
