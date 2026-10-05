using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PROManagementSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceRequestTypeAndSubject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "DocumentId",
                table: "service_requests",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "EmployeeId",
                table: "service_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EntityId",
                table: "service_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "service_requests",
                type: "integer",
                nullable: false,
                defaultValue: 3);

            // Preserve existing rows: backfill the subject from the linked
            // document's owner so the CK_service_requests_subject constraint
            // (EmployeeId OR EntityId) holds for legacy rows too.
            migrationBuilder.Sql("""
                UPDATE service_requests sr
                SET
                    "EmployeeId" = d.employee_id,
                    "EntityId" = d.client_entity_id
                FROM documents d
                WHERE sr."DocumentId" = d.id;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_service_requests_EmployeeId",
                table: "service_requests",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_service_requests_EntityId",
                table: "service_requests",
                column: "EntityId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_service_requests_subject",
                table: "service_requests",
                sql: "\"EmployeeId\" IS NOT NULL OR \"EntityId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_service_requests_client_entities_EntityId",
                table: "service_requests",
                column: "EntityId",
                principalTable: "client_entities",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_service_requests_employees_EmployeeId",
                table: "service_requests",
                column: "EmployeeId",
                principalTable: "employees",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_service_requests_client_entities_EntityId",
                table: "service_requests");

            migrationBuilder.DropForeignKey(
                name: "FK_service_requests_employees_EmployeeId",
                table: "service_requests");

            migrationBuilder.DropIndex(
                name: "IX_service_requests_EmployeeId",
                table: "service_requests");

            migrationBuilder.DropIndex(
                name: "IX_service_requests_EntityId",
                table: "service_requests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_service_requests_subject",
                table: "service_requests");

            migrationBuilder.DropColumn(
                name: "EmployeeId",
                table: "service_requests");

            migrationBuilder.DropColumn(
                name: "EntityId",
                table: "service_requests");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "service_requests");

            migrationBuilder.AlterColumn<Guid>(
                name: "DocumentId",
                table: "service_requests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
