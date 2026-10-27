using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PROManagementSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PaymentOrdersOpenOrderAndWebhookEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_payment_orders_RetainerInvoiceId",
                table: "payment_orders");

            migrationBuilder.DropIndex(
                name: "IX_payment_orders_ServiceFeeInvoiceId",
                table: "payment_orders");

            migrationBuilder.AddColumn<decimal>(
                name: "CapturedAmount",
                table: "payment_orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CapturedCurrency",
                table: "payment_orders",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewReason",
                table: "payment_orders",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "webhook_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EventType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RazorpayOrderId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RazorpayPaymentId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Outcome = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_webhook_events", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_payment_orders_RetainerInvoiceId",
                table: "payment_orders",
                column: "RetainerInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_payment_orders_RetainerInvoiceId_Open",
                table: "payment_orders",
                column: "RetainerInvoiceId",
                unique: true,
                filter: "\"IsDeleted\" = false AND \"Status\" IN (1, 2)");

            migrationBuilder.CreateIndex(
                name: "IX_payment_orders_ServiceFeeInvoiceId",
                table: "payment_orders",
                column: "ServiceFeeInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_payment_orders_ServiceFeeInvoiceId_Open",
                table: "payment_orders",
                column: "ServiceFeeInvoiceId",
                unique: true,
                filter: "\"IsDeleted\" = false AND \"Status\" IN (1, 2)");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_events_EventId",
                table: "webhook_events",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_webhook_events_RazorpayOrderId",
                table: "webhook_events",
                column: "RazorpayOrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "webhook_events");

            migrationBuilder.DropIndex(
                name: "IX_payment_orders_RetainerInvoiceId",
                table: "payment_orders");

            migrationBuilder.DropIndex(
                name: "IX_payment_orders_RetainerInvoiceId_Open",
                table: "payment_orders");

            migrationBuilder.DropIndex(
                name: "IX_payment_orders_ServiceFeeInvoiceId",
                table: "payment_orders");

            migrationBuilder.DropIndex(
                name: "IX_payment_orders_ServiceFeeInvoiceId_Open",
                table: "payment_orders");

            migrationBuilder.DropColumn(
                name: "CapturedAmount",
                table: "payment_orders");

            migrationBuilder.DropColumn(
                name: "CapturedCurrency",
                table: "payment_orders");

            migrationBuilder.DropColumn(
                name: "ReviewReason",
                table: "payment_orders");

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
        }
    }
}
