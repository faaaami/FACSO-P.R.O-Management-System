using System.Net;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.ClientTenancy;

/// <summary>
/// Week 3 Day 3: Client tasks (read mirror) + client service requests
/// (create + read mirror).
///
/// Locked behavior:
///  - The tenant is ALWAYS derived from the authenticated user's
///    ClientCompanyId claim and enforced inside the SQL/EF queries.
///  - By-id reads return 404 for cross-company or unknown resources.
///  - Service request subjects are validated: EMPLOYEE XOR ENTITY least-one
///    constraint; inconsistent employee+entity -> 400; cross-company or
///    unknown subject -> 404; duplicate open request -> 409; EarlyRenewal
///    resolves an active Visa document or returns 409.
///  - A client-supplied clientCompanyId can never override the tenant, and
///    the create body never accepts one.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class ClientTasksAndServiceRequestsApiTests : IntegrationTestBase
{
    private static readonly Guid ClientA = TestDataSeeder.Ids.ClientUserA;
    private static readonly Guid ClientB = TestDataSeeder.Ids.ClientUserB;
    private static readonly Guid Admin = TestDataSeeder.Ids.Admin;

    private static readonly string CompanyA =
        TestDataSeeder.Ids.CompanyA.ToString();
    private static readonly string CompanyB =
        TestDataSeeder.Ids.CompanyB.ToString();

    public ClientTasksAndServiceRequestsApiTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    // ------------------------------------------------------------------
    // Client tasks list
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "T1: Client lists only their company's renewal tasks (200)")]
    public async Task Tasks_Own_OnlyOwnWorld()
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet("/api/v1/client/tasks", ClientA,
                role: "Client", clientCompanyId: CompanyA));

        await EnsureOk(response, HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(TestDataSeeder.Ids.TaskA.ToString(), body);
        Assert.DoesNotContain(TestDataSeeder.Ids.TaskB.ToString(), body);
    }

    [Fact(DisplayName =
        "T2: Client tasks filterable by status (200)")]
    public async Task Tasks_FilterByStatus_Returns200()
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet("/api/v1/client/tasks?status=Submitted", ClientA,
                role: "Client", clientCompanyId: CompanyA));

        await EnsureOk(response, HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(TestDataSeeder.Ids.TaskA.ToString(), body);
        Assert.DoesNotContain(TestDataSeeder.Ids.TaskB.ToString(), body);
    }

    [Fact(DisplayName =
        "T3: Client tasks reject an invalid status filter (400)")]
    public async Task Tasks_FilterInvalidStatus_Returns400()
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet("/api/v1/client/tasks?status=Banana", ClientA,
                role: "Client", clientCompanyId: CompanyA));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ------------------------------------------------------------------
    // Client task by id
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "T4: Client reads their own renewal task (200)")]
    public async Task TaskById_Own_Returns200()
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet(
                $"/api/v1/client/tasks/{TestDataSeeder.Ids.TaskA}",
                ClientA, role: "Client", clientCompanyId: CompanyA));

        await EnsureOk(response, HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(TestDataSeeder.Ids.TaskA.ToString(), body);
    }

    [Fact(DisplayName =
        "T5: Cross-company renewal task returns 404")]
    public async Task TaskById_CrossCompany_Returns404()
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet(
                $"/api/v1/client/tasks/{TestDataSeeder.Ids.TaskB}",
                ClientA, role: "Client", clientCompanyId: CompanyA));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName =
        "T6: Unknown renewal task returns 404")]
    public async Task TaskById_Unknown_Returns404()
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet(
                $"/api/v1/client/tasks/{TestDataSeeder.Ids.Unknown}",
                ClientA, role: "Client", clientCompanyId: CompanyA));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ------------------------------------------------------------------
    // Client task history
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "T7: Client reads their own task history (200, empty ok)")]
    public async Task TaskHistory_Own_Returns200()
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet(
                $"/api/v1/client/tasks/{TestDataSeeder.Ids.TaskA}/history",
                ClientA, role: "Client", clientCompanyId: CompanyA));

        await EnsureOk(response, HttpStatusCode.OK);
    }

    [Fact(DisplayName =
        "T8: Cross-company task history returns 404")]
    public async Task TaskHistory_CrossCompany_Returns404()
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet(
                $"/api/v1/client/tasks/{TestDataSeeder.Ids.TaskB}/history",
                ClientA, role: "Client", clientCompanyId: CompanyA));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName =
        "T9: Unknown task history returns 404")]
    public async Task TaskHistory_Unknown_Returns404()
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet(
                $"/api/v1/client/tasks/{TestDataSeeder.Ids.Unknown}/history",
                ClientA, role: "Client", clientCompanyId: CompanyA));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ------------------------------------------------------------------
    // Client service requests list + detail
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "S1: Client lists only their company's service requests (200)")]
    public async Task ServiceRequests_Own_OnlyOwnWorld()
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet("/api/v1/client/service-requests", ClientA,
                role: "Client", clientCompanyId: CompanyA));

        await EnsureOk(response, HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(TestDataSeeder.Ids.ServiceRequestA.ToString(), body);
        Assert.DoesNotContain(TestDataSeeder.Ids.ServiceRequestB.ToString(), body);
    }

    [Fact(DisplayName =
        "S2: Client service requests filterable by status (200)")]
    public async Task ServiceRequests_FilterByStatus_Returns200()
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet("/api/v1/client/service-requests?status=Submitted", ClientA,
                role: "Client", clientCompanyId: CompanyA));

        await EnsureOk(response, HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(TestDataSeeder.Ids.ServiceRequestA.ToString(), body);
    }

    [Fact(DisplayName =
        "S3: Client service requests reject an invalid status filter (400)")]
    public async Task ServiceRequests_FilterInvalidStatus_Returns400()
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet("/api/v1/client/service-requests?status=Banana", ClientA,
                role: "Client", clientCompanyId: CompanyA));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName =
        "S4: Client reads their own service request detail (200)")]
    public async Task ServiceRequestById_Own_Returns200()
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet(
                $"/api/v1/client/service-requests/{TestDataSeeder.Ids.ServiceRequestA}",
                ClientA, role: "Client", clientCompanyId: CompanyA));

        await EnsureOk(response, HttpStatusCode.OK);

        var body = await ReadJsonAsync<TestResponse<ServiceRequestDto>>(response);
        Assert.NotNull(body?.Data);
        Assert.Equal(ServiceRequestType.EarlyRenewal, body!.Data!.Type);
        Assert.Equal(TestDataSeeder.Ids.EmployeeA, body.Data.EmployeeId);
        Assert.Equal(TestDataSeeder.Ids.EntityA, body.Data.EntityId);
        Assert.Equal(TestDataSeeder.Ids.DocumentA, body.Data.DocumentId);
    }

    [Fact(DisplayName =
        "S5: Cross-company service request returns 404")]
    public async Task ServiceRequestById_CrossCompany_Returns404()
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet(
                $"/api/v1/client/service-requests/{TestDataSeeder.Ids.ServiceRequestB}",
                ClientA, role: "Client", clientCompanyId: CompanyA));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName =
        "S6: Unknown service request returns 404")]
    public async Task ServiceRequestById_Unknown_Returns404()
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet(
                $"/api/v1/client/service-requests/{TestDataSeeder.Ids.Unknown}",
                ClientA, role: "Client", clientCompanyId: CompanyA));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ------------------------------------------------------------------
    // Create service request — subject rules
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "C1: NewVisa for an employee creates a request without a document (201)")]
    public async Task Create_NewVisa_EmployeeOnly_Returns201()
    {
        var response = await ScopeClient.SendAsync(
            ScopePost(
                "/api/v1/client/service-requests",
                new
                {
                    type = "NewVisa",
                    employeeId = TestDataSeeder.Ids.EmployeeA.ToString(),
                    notes = "New visa request."
                },
                ClientA, role: "Client", clientCompanyId: CompanyA));

        await EnsureOk(response, HttpStatusCode.Created);

        var body = await ReadJsonAsync<TestResponse<CreateServiceRequestDto>>(response);
        Assert.NotNull(body?.Data);
        Assert.Equal(ServiceRequestType.NewVisa, body!.Data!.Type);
        Assert.Equal(ServiceRequestStatus.Submitted, body.Data.Status);
        Assert.Equal(TestDataSeeder.Ids.EmployeeA, body.Data.EmployeeId);
        Assert.Null(body.Data.DocumentId);
        Assert.Equal("New visa request.", body.Data.Description);
    }

    [Fact(DisplayName =
        "C2: NewVisa for an entity creates a request without a document (201)")]
    public async Task Create_NewVisa_EntityOnly_Returns201()
    {
        var response = await ScopeClient.SendAsync(
            ScopePost(
                "/api/v1/client/service-requests",
                new
                {
                    type = "NewVisa",
                    entityId = TestDataSeeder.Ids.EntityA.ToString()
                },
                ClientA, role: "Client", clientCompanyId: CompanyA));

        await EnsureOk(response, HttpStatusCode.Created);

        var body = await ReadJsonAsync<TestResponse<CreateServiceRequestDto>>(response);
        Assert.NotNull(body?.Data);
        Assert.Equal(TestDataSeeder.Ids.EntityA, body!.Data!.EntityId);
        Assert.Null(body.Data.DocumentId);
    }

    [Fact(DisplayName =
        "C3: Other for an employee creates a request without a document (201)")]
    public async Task Create_Other_EmployeeOnly_Returns201()
    {
        var response = await ScopeClient.SendAsync(
            ScopePost(
                "/api/v1/client/service-requests",
                new
                {
                    type = "Other",
                    employeeId = TestDataSeeder.Ids.EmployeeA.ToString()
                },
                ClientA, role: "Client", clientCompanyId: CompanyA));

        await EnsureOk(response, HttpStatusCode.Created);

        var body = await ReadJsonAsync<TestResponse<CreateServiceRequestDto>>(response);
        Assert.NotNull(body?.Data);
        Assert.Equal(ServiceRequestType.Other, body!.Data!.Type);
        Assert.Null(body.Data.DocumentId);
    }

    [Fact(DisplayName =
        "C4: Neither employee nor entity subject is rejected (400)")]
    public async Task Create_NoSubject_Returns400()
    {
        var response = await ScopeClient.SendAsync(
            ScopePost(
                "/api/v1/client/service-requests",
                new { type = "NewVisa" },
                ClientA, role: "Client", clientCompanyId: CompanyA));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("VALIDATION_ERROR", body);
    }

    [Fact(DisplayName =
        "C5: Unknown employee subject returns 404")]
    public async Task Create_UnknownEmployee_Returns404()
    {
        var response = await ScopeClient.SendAsync(
            ScopePost(
                "/api/v1/client/service-requests",
                new
                {
                    type = "NewVisa",
                    employeeId = TestDataSeeder.Ids.Unknown.ToString()
                },
                ClientA, role: "Client", clientCompanyId: CompanyA));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName =
        "C6: Cross-company employee subject returns 404")]
    public async Task Create_CrossCompanyEmployee_Returns404()
    {
        var response = await ScopeClient.SendAsync(
            ScopePost(
                "/api/v1/client/service-requests",
                new
                {
                    type = "NewVisa",
                    employeeId = TestDataSeeder.Ids.EmployeeB.ToString()
                },
                ClientA, role: "Client", clientCompanyId: CompanyA));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName =
        "C7: Cross-company entity subject returns 404")]
    public async Task Create_CrossCompanyEntity_Returns404()
    {
        var response = await ScopeClient.SendAsync(
            ScopePost(
                "/api/v1/client/service-requests",
                new
                {
                    type = "NewVisa",
                    entityId = TestDataSeeder.Ids.EntityB.ToString()
                },
                ClientA, role: "Client", clientCompanyId: CompanyA));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName =
        "C8: Employee that does not belong to the supplied entity is rejected (400)")]
    public async Task Create_InconsistentEmployeeAndEntity_Returns400()
    {
        var response = await ScopeClient.SendAsync(
            ScopePost(
                "/api/v1/client/service-requests",
                new
                {
                    type = "NewVisa",
                    employeeId = TestDataSeeder.Ids.EmployeeA.ToString(),
                    entityId = TestDataSeeder.Ids.EntityB.ToString()
                },
                ClientA, role: "Client", clientCompanyId: CompanyA));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName =
        "C9: Consistent employee + entity subject creates a request (201)")]
    public async Task Create_ConsistentEmployeeAndEntity_Returns201()
    {
        var response = await ScopeClient.SendAsync(
            ScopePost(
                "/api/v1/client/service-requests",
                new
                {
                    type = "NewVisa",
                    employeeId = TestDataSeeder.Ids.EmployeeA.ToString(),
                    entityId = TestDataSeeder.Ids.EntityA.ToString()
                },
                ClientA, role: "Client", clientCompanyId: CompanyA));

        await EnsureOk(response, HttpStatusCode.Created);

        var body = await ReadJsonAsync<TestResponse<CreateServiceRequestDto>>(response);
        Assert.NotNull(body?.Data);
        Assert.Equal(TestDataSeeder.Ids.EmployeeA, body!.Data!.EmployeeId);
        Assert.Equal(TestDataSeeder.Ids.EntityA, body.Data.EntityId);
    }

    [Fact(DisplayName =
        "C10: Duplicate open request for the same type + subject is rejected (409)")]
    public async Task Create_DuplicateOpenRequest_Returns409()
    {
        object[] bodies =
        {
            new
            {
                type = "NewVisa",
                employeeId = TestDataSeeder.Ids.EmployeeA.ToString()
            }
        };

        var first = await ScopeClient.SendAsync(
            ScopePost(
                "/api/v1/client/service-requests",
                bodies[0],
                ClientA, role: "Client", clientCompanyId: CompanyA));

        await EnsureOk(first, HttpStatusCode.Created);

        var second = await ScopeClient.SendAsync(
            ScopePost(
                "/api/v1/client/service-requests",
                bodies[0],
                ClientA, role: "Client", clientCompanyId: CompanyA));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    // ------------------------------------------------------------------
    // Early renewal resolution
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "R1: EarlyRenewal without an active visa document is rejected (409)")]
    public async Task Create_EarlyRenewal_NoVisa_Returns409()
    {
        var employeeId = Guid.NewGuid();
        await SeedEmployee(employeeId);

        var response = await ScopeClient.SendAsync(
            ScopePost(
                "/api/v1/client/service-requests",
                new
                {
                    type = "EarlyRenewal",
                    employeeId = employeeId.ToString()
                },
                ClientA, role: "Client", clientCompanyId: CompanyA));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact(DisplayName =
        "R2: EarlyRenewal resolves the active visa document (201)")]
    public async Task Create_EarlyRenewal_WithVisa_Returns201WithDocument()
    {
        var employeeId = Guid.NewGuid();
        var visaId = Guid.NewGuid();
        await SeedEmployee(employeeId);
        await SeedVisaDocument(visaId, employeeId);

        var response = await ScopeClient.SendAsync(
            ScopePost(
                "/api/v1/client/service-requests",
                new
                {
                    type = "EarlyRenewal",
                    employeeId = employeeId.ToString()
                },
                ClientA, role: "Client", clientCompanyId: CompanyA));

        await EnsureOk(response, HttpStatusCode.Created);

        var body = await ReadJsonAsync<TestResponse<CreateServiceRequestDto>>(response);
        Assert.NotNull(body?.Data);
        Assert.Equal(visaId, body!.Data!.DocumentId);
        Assert.Equal(ServiceRequestType.EarlyRenewal, body.Data.Type);
    }

    [Fact(DisplayName =
        "R3: Duplicate open EarlyRenewal for the same subject is rejected (409)")]
    public async Task Create_EarlyRenewal_Duplicate_Returns409()
    {
        var employeeId = Guid.NewGuid();
        var visaId = Guid.NewGuid();
        await SeedEmployee(employeeId);
        await SeedVisaDocument(visaId, employeeId);

        var body = new
        {
            type = "EarlyRenewal",
            employeeId = employeeId.ToString()
        };

        var first = await ScopeClient.SendAsync(
            ScopePost(
                "/api/v1/client/service-requests",
                body,
                ClientA, role: "Client", clientCompanyId: CompanyA));

        await EnsureOk(first, HttpStatusCode.Created);

        var second = await ScopeClient.SendAsync(
            ScopePost(
                "/api/v1/client/service-requests",
                body,
                ClientA, role: "Client", clientCompanyId: CompanyA));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    // ------------------------------------------------------------------
    // Admin convert guard (service requests without a document)
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "V1: Admin convert of a request without a document returns 409")]
    public async Task Convert_NoDocument_Returns409()
    {
        var created = await CreateNewVisaForEmployeeA();

        var response = await ScopeClient.SendAsync(
            ScopePatch(
                $"/api/v1/service-requests/{created}/convert",
                null,
                Admin, role: "Admin"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact(DisplayName =
        "V2: Admin convert of a document-linked request succeeds, then 409 on repeat")]
    public async Task Convert_DocumentLinked_ConvertsOnce()
    {
        var documentId = Guid.NewGuid();
        var serviceRequestId = Guid.NewGuid();
        await WithScopeDbContextAsync(db =>
        {
            db.Documents.Add(new Document
            {
                Id = documentId,
                EmployeeId = TestDataSeeder.Ids.EmployeeA,
                Type = DocumentType.Passport,
                DocumentNumber = "DOC-CONVERT-CLIENT",
                IssueDate = DateTime.UtcNow.AddYears(-1),
                ExpiryDate = DateTime.UtcNow.AddYears(2),
                FileUrl = "https://storage.test/convert",
                FileName = "convert.pdf",
                ContentType = "application/pdf",
                FileSize = 2048,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsDeleted = false,
                DeletedAt = null
            });
            db.ServiceRequests.Add(new ServiceRequest
            {
                Id = serviceRequestId,
                ClientCompanyId = TestDataSeeder.Ids.CompanyA,
                Type = ServiceRequestType.Other,
                EmployeeId = TestDataSeeder.Ids.EmployeeA,
                EntityId = TestDataSeeder.Ids.EntityA,
                DocumentId = documentId,
                Status = ServiceRequestStatus.Submitted,
                Description = "Convertible request.",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsDeleted = false,
                DeletedAt = null
            });
            db.SaveChanges();
        });

        var first = await ScopeClient.SendAsync(
            ScopePatch(
                $"/api/v1/service-requests/{serviceRequestId}/convert",
                null,
                Admin, role: "Admin"));

        await EnsureOk(first, HttpStatusCode.OK);

        var body = await ReadJsonAsync<TestResponse<ConvertResponseDto>>(first);
        Assert.NotNull(body?.Data);
        Assert.NotEqual(Guid.Empty, body!.Data!.RenewalTaskId);
        Assert.Equal(ServiceRequestStatus.Converted, body.Data.Status);

        var second = await ScopeClient.SendAsync(
            ScopePatch(
                $"/api/v1/service-requests/{serviceRequestId}/convert",
                null,
                Admin, role: "Admin"));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private async Task<Guid> CreateNewVisaForEmployeeA()
    {
        var response = await ScopeClient.SendAsync(
            ScopePost(
                "/api/v1/client/service-requests",
                new
                {
                    type = "NewVisa",
                    employeeId = TestDataSeeder.Ids.EmployeeA.ToString()
                },
                ClientA, role: "Client", clientCompanyId: CompanyA));

        await EnsureOk(response, HttpStatusCode.Created);

        var body = await ReadJsonAsync<TestResponse<CreateServiceRequestDto>>(response);
        return body!.Data!.Id;
    }

    private Task SeedEmployee(Guid employeeId)
        => WithScopeDbContextAsync(db =>
        {
            db.Employees.Add(new Employee
            {
                Id = employeeId,
                ClientEntityId = TestDataSeeder.Ids.EntityA,
                FullName = "Scoped Test Employee",
                JobTitle = "Engineer",
                HireDate = DateTime.UtcNow.AddYears(-1),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsDeleted = false,
                DeletedAt = null
            });
            db.SaveChanges();
        });

    private Task SeedVisaDocument(Guid visaId, Guid employeeId)
        => WithScopeDbContextAsync(db =>
        {
            db.Documents.Add(new Document
            {
                Id = visaId,
                EmployeeId = employeeId,
                Type = DocumentType.Visa,
                DocumentNumber = "VISA-SCOPED",
                IssueDate = DateTime.UtcNow.AddYears(-1),
                ExpiryDate = DateTime.UtcNow.AddYears(2),
                FileUrl = "https://storage.test/visa",
                FileName = "visa.pdf",
                ContentType = "application/pdf",
                FileSize = 2048,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsDeleted = false,
                DeletedAt = null
            });
            db.SaveChanges();
        });

    private sealed record TestResponse<T>(bool Success, int StatusCode, T? Data);

    private sealed record ServiceRequestDto(
        Guid Id,
        Guid ClientCompanyId,
        ServiceRequestType Type,
        Guid? EmployeeId,
        Guid? EntityId,
        Guid? DocumentId,
        ServiceRequestStatus Status,
        string? Description);

    private sealed record CreateServiceRequestDto(
        Guid Id,
        Guid ClientCompanyId,
        ServiceRequestType Type,
        Guid? EmployeeId,
        Guid? EntityId,
        Guid? DocumentId,
        ServiceRequestStatus Status,
        string? Description,
        DateTime CreatedAt);

    private sealed record ConvertResponseDto(
        Guid Id,
        Guid RenewalTaskId,
        ServiceRequestStatus Status,
        DateTime ConvertedAt);
}