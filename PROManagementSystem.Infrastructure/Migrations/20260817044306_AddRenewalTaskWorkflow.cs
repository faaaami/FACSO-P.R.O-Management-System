using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PROManagementSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRenewalTaskWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "renewal_tasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientCompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedStaffId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    BlockedReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    BlockedSince = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_renewal_tasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_renewal_tasks_client_companies_ClientCompanyId",
                        column: x => x.ClientCompanyId,
                        principalTable: "client_companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_renewal_tasks_documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_renewal_tasks_users_AssignedStaffId",
                        column: x => x.AssignedStaffId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "renewal_step_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RenewalTaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CompletedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ProofFileUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_renewal_step_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_renewal_step_logs_renewal_tasks_RenewalTaskId",
                        column: x => x.RenewalTaskId,
                        principalTable: "renewal_tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_renewal_step_logs_users_CompletedBy",
                        column: x => x.CompletedBy,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "renewal_task_histories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RenewalTaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ChangedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_renewal_task_histories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_renewal_task_histories_renewal_tasks_RenewalTaskId",
                        column: x => x.RenewalTaskId,
                        principalTable: "renewal_tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_renewal_task_histories_users_ChangedBy",
                        column: x => x.ChangedBy,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_renewal_step_logs_CompletedBy",
                table: "renewal_step_logs",
                column: "CompletedBy");

            migrationBuilder.CreateIndex(
                name: "IX_renewal_step_logs_RenewalTaskId",
                table: "renewal_step_logs",
                column: "RenewalTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_renewal_task_histories_ChangedAt",
                table: "renewal_task_histories",
                column: "ChangedAt");

            migrationBuilder.CreateIndex(
                name: "IX_renewal_task_histories_ChangedBy",
                table: "renewal_task_histories",
                column: "ChangedBy");

            migrationBuilder.CreateIndex(
                name: "IX_renewal_task_histories_RenewalTaskId",
                table: "renewal_task_histories",
                column: "RenewalTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_renewal_tasks_AssignedStaffId",
                table: "renewal_tasks",
                column: "AssignedStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_renewal_tasks_ClientCompanyId",
                table: "renewal_tasks",
                column: "ClientCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_renewal_tasks_DocumentId",
                table: "renewal_tasks",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_renewal_tasks_Status",
                table: "renewal_tasks",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "renewal_step_logs");

            migrationBuilder.DropTable(
                name: "renewal_task_histories");

            migrationBuilder.DropTable(
                name: "renewal_tasks");
        }
    }
}
