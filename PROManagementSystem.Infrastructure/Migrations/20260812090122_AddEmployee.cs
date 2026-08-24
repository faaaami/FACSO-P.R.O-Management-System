using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PROManagementSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The database may already contain the correctly named
            // is_approved column because it was fixed manually earlier.
            //
            // This makes the migration safe for both:
            // 1. Databases containing "IsApproved"
            // 2. Databases already containing "is_approved"

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_name = 'users'
                          AND column_name = 'IsApproved'
                    )
                    AND NOT EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_name = 'users'
                          AND column_name = 'is_approved'
                    )
                    THEN
                        ALTER TABLE users
                        RENAME COLUMN "IsApproved" TO is_approved;
                    END IF;
                END
                $$;
                """);

            migrationBuilder.CreateTable(
                name: "employees",
                columns: table => new
                {
                    id = table.Column<Guid>(
                        type: "uuid",
                        nullable: false),

                    client_entity_id = table.Column<Guid>(
                        type: "uuid",
                        nullable: false),

                    full_name = table.Column<string>(
                        type: "character varying(200)",
                        maxLength: 200,
                        nullable: false),

                    passport_number = table.Column<string>(
                        type: "character varying(50)",
                        maxLength: 50,
                        nullable: true),

                    date_of_birth = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true),

                    nationality = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: true),

                    job_title = table.Column<string>(
                        type: "character varying(150)",
                        maxLength: 150,
                        nullable: true),

                    hire_date = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false),

                    is_active = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: true),

                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false),

                    updated_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false),

                    is_deleted = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: false),

                    deleted_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "PK_employees",
                        x => x.id);

                    table.ForeignKey(
                        name: "FK_employees_client_entities_client_entity_id",
                        column: x => x.client_entity_id,
                        principalTable: "client_entities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_employees_client_entity_id",
                table: "employees",
                column: "client_entity_id");

            migrationBuilder.CreateIndex(
                name: "IX_employees_is_active",
                table: "employees",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_employees_is_deleted",
                table: "employees",
                column: "is_deleted");
        }

        /// <inheritdoc />
        protected override void Down(
            MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "employees");

            // We intentionally do NOT rename is_approved back to
            // "IsApproved" here because the existing database schema
            // has already been standardized to snake_case.
        }
    }
}