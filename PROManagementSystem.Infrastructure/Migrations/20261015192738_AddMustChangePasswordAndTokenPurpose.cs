using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PROManagementSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMustChangePasswordAndTokenPurpose : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "must_change_password",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                table: "password_reset_tokens",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                // Every token issued before this migration is a password reset.
                defaultValue: "PasswordReset");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "must_change_password",
                table: "users");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "password_reset_tokens");
        }
    }
}
