using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PROManagementSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceCatalogueAndJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "customer_code_seq");

            migrationBuilder.CreateSequence(
                name: "service_job_number_seq");

            migrationBuilder.AddColumn<Guid>(
                name: "ConvertedServiceJobId",
                table: "service_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ServiceDefinitionId",
                table: "service_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ServiceJobId",
                table: "gov_fee_disbursements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "email",
                table: "employees",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "emirates_id_number",
                table: "employees",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "full_name_arabic",
                table: "employees",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "gender",
                table: "employees",
                type: "character varying(1)",
                maxLength: 1,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "mobile",
                table: "employees",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "mohre_person_code",
                table: "employees",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "person_type",
                table: "employees",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "relationship_to_sponsor",
                table: "employees",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "sponsor_id",
                table: "employees",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "uid_number",
                table: "employees",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "contact_person_name",
                table: "client_companies",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "customer_code",
                table: "client_companies",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "customer_type",
                table: "client_companies",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "notes",
                table: "client_companies",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tax_registration_number",
                table: "client_companies",
                type: "character varying(15)",
                maxLength: 15,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "service_definitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NameArabic = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    Authority = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SubjectType = table.Column<int>(type: "integer", nullable: false),
                    DefaultServiceFee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    EstimatedGovernmentFee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ServiceFeeVatable = table.Column<bool>(type: "boolean", nullable: false),
                    EstimatedDays = table.Column<int>(type: "integer", nullable: true),
                    IsCounterService = table.Column<bool>(type: "boolean", nullable: false),
                    RenewsDocumentType = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_definitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "contract_service_prices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceContractId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceFee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IncludedInRetainer = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contract_service_prices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_contract_service_prices_service_contracts_ServiceContractId",
                        column: x => x.ServiceContractId,
                        principalTable: "service_contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_contract_service_prices_service_definitions_ServiceDefiniti~",
                        column: x => x.ServiceDefinitionId,
                        principalTable: "service_definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "service_jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ClientCompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClientEntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    ServiceRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignedStaffId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    ServiceFee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ServiceFeeVatable = table.Column<bool>(type: "boolean", nullable: false),
                    EstimatedGovernmentFee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ApplicationNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    OnHoldReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_jobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_service_jobs_client_companies_ClientCompanyId",
                        column: x => x.ClientCompanyId,
                        principalTable: "client_companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_service_jobs_client_entities_ClientEntityId",
                        column: x => x.ClientEntityId,
                        principalTable: "client_entities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_service_jobs_documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_service_jobs_employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_service_jobs_service_definitions_ServiceDefinitionId",
                        column: x => x.ServiceDefinitionId,
                        principalTable: "service_definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_service_jobs_users_AssignedStaffId",
                        column: x => x.AssignedStaffId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "service_required_documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsMandatory = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_required_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_service_required_documents_service_definitions_ServiceDefin~",
                        column: x => x.ServiceDefinitionId,
                        principalTable: "service_definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "service_step_templates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_step_templates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_service_step_templates_service_definitions_ServiceDefinitio~",
                        column: x => x.ServiceDefinitionId,
                        principalTable: "service_definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "service_job_activities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FromStatus = table.Column<int>(type: "integer", nullable: true),
                    ToStatus = table.Column<int>(type: "integer", nullable: true),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsInternal = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_job_activities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_service_job_activities_service_jobs_ServiceJobId",
                        column: x => x.ServiceJobId,
                        principalTable: "service_jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "service_job_documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsMandatory = table.Column<bool>(type: "boolean", nullable: false),
                    IsReceived = table.Column<bool>(type: "boolean", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_job_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_service_job_documents_service_jobs_ServiceJobId",
                        column: x => x.ServiceJobId,
                        principalTable: "service_jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "service_job_steps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_job_steps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_service_job_steps_service_jobs_ServiceJobId",
                        column: x => x.ServiceJobId,
                        principalTable: "service_jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_service_requests_ConvertedServiceJobId",
                table: "service_requests",
                column: "ConvertedServiceJobId");

            migrationBuilder.CreateIndex(
                name: "IX_service_requests_ServiceDefinitionId",
                table: "service_requests",
                column: "ServiceDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_gov_fee_disbursements_ServiceJobId",
                table: "gov_fee_disbursements",
                column: "ServiceJobId");

            migrationBuilder.CreateIndex(
                name: "IX_employees_emirates_id_number",
                table: "employees",
                column: "emirates_id_number");

            migrationBuilder.CreateIndex(
                name: "IX_employees_passport_number",
                table: "employees",
                column: "passport_number");

            migrationBuilder.CreateIndex(
                name: "IX_employees_sponsor_id",
                table: "employees",
                column: "sponsor_id");

            migrationBuilder.CreateIndex(
                name: "IX_client_companies_customer_code",
                table: "client_companies",
                column: "customer_code",
                unique: true,
                filter: "customer_code IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_client_companies_customer_type",
                table: "client_companies",
                column: "customer_type");

            migrationBuilder.CreateIndex(
                name: "IX_contract_service_prices_ServiceContractId_ServiceDefinition~",
                table: "contract_service_prices",
                columns: new[] { "ServiceContractId", "ServiceDefinitionId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_contract_service_prices_ServiceDefinitionId",
                table: "contract_service_prices",
                column: "ServiceDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_service_definitions_Category",
                table: "service_definitions",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_service_definitions_Code",
                table: "service_definitions",
                column: "Code",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_service_job_activities_ServiceJobId",
                table: "service_job_activities",
                column: "ServiceJobId");

            migrationBuilder.CreateIndex(
                name: "IX_service_job_documents_ServiceJobId",
                table: "service_job_documents",
                column: "ServiceJobId");

            migrationBuilder.CreateIndex(
                name: "IX_service_job_steps_ServiceJobId",
                table: "service_job_steps",
                column: "ServiceJobId");

            migrationBuilder.CreateIndex(
                name: "IX_service_jobs_AssignedStaffId",
                table: "service_jobs",
                column: "AssignedStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_service_jobs_ClientCompanyId",
                table: "service_jobs",
                column: "ClientCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_service_jobs_ClientEntityId",
                table: "service_jobs",
                column: "ClientEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_service_jobs_DocumentId",
                table: "service_jobs",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_service_jobs_EmployeeId",
                table: "service_jobs",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_service_jobs_JobNumber",
                table: "service_jobs",
                column: "JobNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_service_jobs_ServiceDefinitionId",
                table: "service_jobs",
                column: "ServiceDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_service_jobs_Status",
                table: "service_jobs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_service_required_documents_ServiceDefinitionId",
                table: "service_required_documents",
                column: "ServiceDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_service_step_templates_ServiceDefinitionId",
                table: "service_step_templates",
                column: "ServiceDefinitionId");

            migrationBuilder.AddForeignKey(
                name: "FK_employees_employees_sponsor_id",
                table: "employees",
                column: "sponsor_id",
                principalTable: "employees",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_gov_fee_disbursements_service_jobs_ServiceJobId",
                table: "gov_fee_disbursements",
                column: "ServiceJobId",
                principalTable: "service_jobs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_service_requests_service_definitions_ServiceDefinitionId",
                table: "service_requests",
                column: "ServiceDefinitionId",
                principalTable: "service_definitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_service_requests_service_jobs_ConvertedServiceJobId",
                table: "service_requests",
                column: "ConvertedServiceJobId",
                principalTable: "service_jobs",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Give every existing customer a counter code (C-000001, ...).
            migrationBuilder.Sql(
                "UPDATE client_companies SET customer_code = " +
                "'C-' || lpad(nextval('customer_code_seq')::text, 6, '0') " +
                "WHERE customer_code IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_employees_employees_sponsor_id",
                table: "employees");

            migrationBuilder.DropForeignKey(
                name: "FK_gov_fee_disbursements_service_jobs_ServiceJobId",
                table: "gov_fee_disbursements");

            migrationBuilder.DropForeignKey(
                name: "FK_service_requests_service_definitions_ServiceDefinitionId",
                table: "service_requests");

            migrationBuilder.DropForeignKey(
                name: "FK_service_requests_service_jobs_ConvertedServiceJobId",
                table: "service_requests");

            migrationBuilder.DropTable(
                name: "contract_service_prices");

            migrationBuilder.DropTable(
                name: "service_job_activities");

            migrationBuilder.DropTable(
                name: "service_job_documents");

            migrationBuilder.DropTable(
                name: "service_job_steps");

            migrationBuilder.DropTable(
                name: "service_required_documents");

            migrationBuilder.DropTable(
                name: "service_step_templates");

            migrationBuilder.DropTable(
                name: "service_jobs");

            migrationBuilder.DropTable(
                name: "service_definitions");

            migrationBuilder.DropIndex(
                name: "IX_service_requests_ConvertedServiceJobId",
                table: "service_requests");

            migrationBuilder.DropIndex(
                name: "IX_service_requests_ServiceDefinitionId",
                table: "service_requests");

            migrationBuilder.DropIndex(
                name: "IX_gov_fee_disbursements_ServiceJobId",
                table: "gov_fee_disbursements");

            migrationBuilder.DropIndex(
                name: "IX_employees_emirates_id_number",
                table: "employees");

            migrationBuilder.DropIndex(
                name: "IX_employees_passport_number",
                table: "employees");

            migrationBuilder.DropIndex(
                name: "IX_employees_sponsor_id",
                table: "employees");

            migrationBuilder.DropIndex(
                name: "IX_client_companies_customer_code",
                table: "client_companies");

            migrationBuilder.DropIndex(
                name: "IX_client_companies_customer_type",
                table: "client_companies");

            migrationBuilder.DropColumn(
                name: "ConvertedServiceJobId",
                table: "service_requests");

            migrationBuilder.DropColumn(
                name: "ServiceDefinitionId",
                table: "service_requests");

            migrationBuilder.DropColumn(
                name: "ServiceJobId",
                table: "gov_fee_disbursements");

            migrationBuilder.DropColumn(
                name: "email",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "emirates_id_number",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "full_name_arabic",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "gender",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "mobile",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "mohre_person_code",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "person_type",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "relationship_to_sponsor",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "sponsor_id",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "uid_number",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "contact_person_name",
                table: "client_companies");

            migrationBuilder.DropColumn(
                name: "customer_code",
                table: "client_companies");

            migrationBuilder.DropColumn(
                name: "customer_type",
                table: "client_companies");

            migrationBuilder.DropColumn(
                name: "notes",
                table: "client_companies");

            migrationBuilder.DropColumn(
                name: "tax_registration_number",
                table: "client_companies");

            migrationBuilder.DropSequence(
                name: "customer_code_seq");

            migrationBuilder.DropSequence(
                name: "service_job_number_seq");
        }
    }
}
