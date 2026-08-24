using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PROManagementSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "payment_orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceType = table.Column<int>(type: "integer", nullable: false),
                    RetainerInvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ServiceFeeInvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClientCompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RazorpayOrderId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RazorpayPaymentId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RazorpaySignature = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_orders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_payment_orders_client_companies_ClientCompanyId",
                        column: x => x.ClientCompanyId,
                        principalTable: "client_companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payment_orders_retainer_invoices_RetainerInvoiceId",
                        column: x => x.RetainerInvoiceId,
                        principalTable: "retainer_invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payment_orders_service_fee_invoices_ServiceFeeInvoiceId",
                        column: x => x.ServiceFeeInvoiceId,
                        principalTable: "service_fee_invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_payment_orders_ClientCompanyId",
                table: "payment_orders",
                column: "ClientCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_payment_orders_InvoiceType",
                table: "payment_orders",
                column: "InvoiceType");

            migrationBuilder.CreateIndex(
                name: "IX_payment_orders_RazorpayOrderId",
                table: "payment_orders",
                column: "RazorpayOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_payment_orders_RetainerInvoiceId",
                table: "payment_orders",
                column: "RetainerInvoiceId",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_payment_orders_ServiceFeeInvoiceId",
                table: "payment_orders",
                column: "ServiceFeeInvoiceId",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_payment_orders_Status",
                table: "payment_orders",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payment_orders");
        }
    }
}
