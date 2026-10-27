using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.ServiceJobs;

/// <summary>
/// Service catalogue, walk-in customers and the generic job workflow:
/// pricing, subject rules, lifecycle, scoping per role, renewal completion
/// and conversion of client service requests.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class ServiceJobsApiTests : IntegrationTestBase
{
    private static readonly Guid Admin = TestDataSeeder.Ids.Admin;
    private static readonly Guid AgentA = TestDataSeeder.Ids.AgentA;
    private static readonly Guid AgentB = TestDataSeeder.Ids.AgentB;
    private static readonly Guid ClientUserA = TestDataSeeder.Ids.ClientUserA;
    private static readonly Guid ClientUserB = TestDataSeeder.Ids.ClientUserB;
    private static readonly Guid CompanyA = TestDataSeeder.Ids.CompanyA;
    private static readonly Guid CompanyB = TestDataSeeder.Ids.CompanyB;
    private static readonly Guid EmployeeA = TestDataSeeder.Ids.EmployeeA;
    private static readonly Guid EmployeeB = TestDataSeeder.Ids.EmployeeB;
    private static readonly Guid EntityA = TestDataSeeder.Ids.EntityA;
    private static readonly Guid DocumentA = TestDataSeeder.Ids.DocumentA;
    private static readonly Guid ContractA = TestDataSeeder.Ids.ContractA;

    public ServiceJobsApiTests(DatabaseFixture fixture) : base(fixture) { }

    // ================================================================ helpers

    private Task<HttpResponseMessage> AdminPost(string url, object? body) =>
        ScopeClient.SendAsync(ScopePost(url, body, Admin, "Admin"));

    private Task<HttpResponseMessage> AdminPatch(string url, object? body) =>
        ScopeClient.SendAsync(ScopePatch(url, body, Admin, "Admin"));

    private Task<HttpResponseMessage> AdminGet(string url) =>
        ScopeClient.SendAsync(ScopeGet(url, Admin, "Admin"));

    private static async Task<JsonElement> Data(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == expected, $"Expected {expected}, got {(int)response.StatusCode}: {body}");
        var root = JsonDocument.Parse(body).RootElement;
        return root.TryGetProperty("data", out var data) ? data : root;
    }

    private async Task<Guid> SeedServiceAsync(
        string code,
        ServiceSubjectType subject = ServiceSubjectType.Person,
        decimal fee = 500m,
        DocumentType? renews = null,
        bool active = true,
        params string[] steps)
    {
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await WithScopeDbContextAsync(db =>
        {
            var service = new ServiceDefinition
            {
                Id = id, Code = code, Name = $"Service {code}", Category = ServiceCategory.Visa,
                SubjectType = subject, DefaultServiceFee = fee, EstimatedGovernmentFee = 1200m,
                ServiceFeeVatable = true, EstimatedDays = 5, RenewsDocumentType = renews, IsActive = active,
                CreatedAt = now, UpdatedAt = now
            };
            var order = 1;
            foreach (var step in steps.Length == 0 ? ["Typing", "Submission"] : steps)
            {
                service.Steps.Add(new ServiceStepTemplate
                {
                    Id = Guid.NewGuid(), ServiceDefinitionId = id, Order = order++, Name = step,
                    CreatedAt = now, UpdatedAt = now
                });
            }
            service.RequiredDocuments.Add(new ServiceRequiredDocument
            {
                Id = Guid.NewGuid(), ServiceDefinitionId = id, Order = 1, Name = "Passport copy",
                IsMandatory = true, CreatedAt = now, UpdatedAt = now
            });
            db.ServiceDefinitions.Add(service);
            db.SaveChanges();
        });
        return id;
    }

    private async Task<JsonElement> CreateJobAsync(object body, HttpStatusCode expected = HttpStatusCode.Created) =>
        await Data(await AdminPost("/api/v1/admin/service-jobs", body), expected);

    private static object JobBody(Guid company, Guid service, Guid? employee = null, Guid? staff = null,
        Guid? document = null, decimal? fee = null, Guid? entity = null) => new
    {
        clientCompanyId = company,
        serviceDefinitionId = service,
        employeeId = employee,
        clientEntityId = entity,
        documentId = document,
        assignedStaffId = staff,
        priority = "Normal",
        source = "Staff",
        serviceFee = fee,
        notes = "Internal: customer prefers WhatsApp"
    };

    // ================================================================ catalogue

    [Fact]
    public async Task Admin_creates_a_catalogue_service_and_duplicate_codes_are_rejected()
    {
        var body = new
        {
            code = "visa-test-1",
            name = "Test visa",
            category = "Visa",
            subjectType = "Person",
            defaultServiceFee = 350,
            estimatedGovernmentFee = 1000,
            serviceFeeVatable = true,
            estimatedDays = 3,
            isCounterService = false,
            sortOrder = 1,
            steps = new[] { new { order = 1, name = "Typing" }, new { order = 2, name = "Approval" } },
            requiredDocuments = new[] { new { order = 1, name = "Passport", isMandatory = true } }
        };

        var created = await Data(await AdminPost("/api/v1/admin/services", body), HttpStatusCode.Created);
        Assert.Equal("VISA-TEST-1", created.GetProperty("code").GetString());
        Assert.Equal(2, created.GetProperty("steps").GetArrayLength());

        var duplicate = await AdminPost("/api/v1/admin/services", body);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var invalid = await AdminPost("/api/v1/admin/services", new { code = "bad code!", name = "", category = "Visa", subjectType = "Person" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task Editing_a_service_replaces_its_templates_without_touching_existing_jobs()
    {
        var service = await SeedServiceAsync("EDIT-1", steps: ["Old step A", "Old step B"]);
        var job = await CreateJobAsync(JobBody(CompanyA, service, EmployeeA));

        var updated = await Data(await ScopeClient.SendAsync(new HttpRequestMessage(HttpMethod.Put, $"/api/v1/admin/services/{service}")
        {
            Content = System.Net.Http.Json.JsonContent.Create(new
            {
                code = "EDIT-1", name = "Edited", category = "Labour", subjectType = "Person",
                defaultServiceFee = 777, estimatedGovernmentFee = 0, serviceFeeVatable = false, sortOrder = 5,
                steps = new[] { new { order = 1, name = "New step" } },
                requiredDocuments = new[] { new { order = 1, name = "Emirates ID", isMandatory = false } }
            }),
            Headers = { { "X-Test-Identity", TestIdentityService.HeaderPayload(Admin, "Admin", null) } }
        }));

        Assert.Equal("Edited", updated.GetProperty("name").GetString());
        Assert.Equal(777m, updated.GetProperty("defaultServiceFee").GetDecimal());
        Assert.Equal("New step", Assert.Single(updated.GetProperty("steps").EnumerateArray()).GetProperty("name").GetString());

        var reloaded = await Data(await AdminGet($"/api/v1/admin/services/{service}"));
        Assert.Single(reloaded.GetProperty("steps").EnumerateArray());
        Assert.Single(reloaded.GetProperty("requiredDocuments").EnumerateArray());

        // The job created before the edit keeps its own copy of the old steps.
        var existing = await Data(await AdminGet($"/api/v1/admin/service-jobs/{job.GetProperty("id").GetGuid()}"));
        Assert.Equal(2, existing.GetProperty("steps").GetArrayLength());
    }

    [Fact]
    public async Task Catalogue_management_is_admin_only_and_clients_see_active_services_without_prices()
    {
        await SeedServiceAsync("ACTIVE-1");
        await SeedServiceAsync("INACTIVE-1", active: false);

        var agent = await ScopeClient.SendAsync(ScopeGet("/api/v1/admin/services", AgentA, "Agent"));
        Assert.Equal(HttpStatusCode.Forbidden, agent.StatusCode);

        var catalogue = await Data(await ScopeClient.SendAsync(
            ScopeGet("/api/v1/services", ClientUserA, "Client", CompanyA.ToString())));
        var codes = catalogue.EnumerateArray().Select(x => x.GetProperty("code").GetString()).ToList();
        Assert.Contains("ACTIVE-1", codes);
        Assert.DoesNotContain("INACTIVE-1", codes);
        Assert.False(catalogue[0].TryGetProperty("defaultServiceFee", out _));
    }

    // ================================================================ walk-in customers

    [Fact]
    public async Task Walk_in_individual_is_registered_with_a_code_and_found_by_passport()
    {
        var created = await Data(await AdminPost("/api/v1/admin/customers/walk-in", new
        {
            customerType = "WalkInIndividual",
            name = "Ahmed Khan",
            mobile = "+971 50 123 4567",
            nationality = "Pakistan",
            passportNumber = "ab1234567",
            emiratesIdNumber = "784198012345671",
            gender = "M"
        }), HttpStatusCode.Created);

        Assert.StartsWith("C-", created.GetProperty("customerCode").GetString());
        Assert.NotEqual(JsonValueKind.Null, created.GetProperty("personId").ValueKind);

        var byPassport = await Data(await AdminGet("/api/v1/admin/customers/lookup?q=AB1234567"));
        var match = Assert.Single(byPassport.EnumerateArray());
        Assert.Equal("WalkInIndividual", match.GetProperty("customerType").GetString());
        Assert.Equal("784-1980-1234567-1",
            match.GetProperty("matchedPersons")[0].GetProperty("emiratesIdNumber").GetString());

        var byMobile = await Data(await AdminGet("/api/v1/admin/customers/lookup?q=0501234567"));
        Assert.Empty(byMobile.EnumerateArray()); // leading 0 vs +971: digits differ
        var byMobileDigits = await Data(await AdminGet("/api/v1/admin/customers/lookup?q=501234567"));
        Assert.Single(byMobileDigits.EnumerateArray());
    }

    [Fact]
    public async Task Walk_in_requires_a_way_to_reach_the_customer_and_a_valid_emirates_id()
    {
        var response = await AdminPost("/api/v1/admin/customers/walk-in", new
        {
            customerType = "WalkInIndividual",
            name = "No Contact",
            emiratesIdNumber = "123"
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Dependent_requires_a_sponsor_of_the_same_customer()
    {
        var withoutSponsor = await AdminPost($"/api/v1/admin/entities/{EntityA}/persons",
            new { personType = "Dependent", fullName = "Child A" });
        Assert.Equal(HttpStatusCode.BadRequest, withoutSponsor.StatusCode);

        var foreignSponsor = await AdminPost($"/api/v1/admin/entities/{EntityA}/persons",
            new { personType = "Dependent", fullName = "Child A", sponsorId = EmployeeB, relationshipToSponsor = "Son" });
        Assert.Equal(HttpStatusCode.Conflict, foreignSponsor.StatusCode);

        var ok = await Data(await AdminPost($"/api/v1/admin/entities/{EntityA}/persons",
            new { personType = "Dependent", fullName = "Child A", sponsorId = EmployeeA, relationshipToSponsor = "Son" }),
            HttpStatusCode.Created);
        Assert.Equal("Son", ok.GetProperty("relationshipToSponsor").GetString());

        var sponsor = await Data(await AdminGet($"/api/v1/admin/persons/{EmployeeA}"));
        Assert.Equal(1, sponsor.GetProperty("dependentCount").GetInt32());
    }

    // ================================================================ job creation and pricing

    [Fact]
    public async Task Job_copies_templates_gets_a_number_and_uses_the_default_fee()
    {
        var service = await SeedServiceAsync("JOB-NEW-1", fee: 450m, steps: ["Typing", "Submission", "Approval"]);

        // CompanyB has a contract but no price for this service → default fee.
        var job = await CreateJobAsync(JobBody(CompanyB, service, EmployeeB));

        Assert.Matches(@"^JOB-\d{4}-\d{6}$", job.GetProperty("jobNumber").GetString());
        Assert.Equal(450m, job.GetProperty("serviceFee").GetDecimal());
        Assert.Equal(3, job.GetProperty("steps").GetArrayLength());
        Assert.Single(job.GetProperty("documents").EnumerateArray());
        Assert.Equal("New", job.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, job.GetProperty("dueDate").ValueKind);
    }

    [Fact]
    public async Task Subscription_contract_price_and_retainer_inclusion_override_the_default_fee()
    {
        var service = await SeedServiceAsync("JOB-PRICE-1", fee: 450m);

        var price = await Data(await ScopeClient.SendAsync(new HttpRequestMessage(HttpMethod.Put,
            $"/api/v1/admin/contracts/{ContractA}/service-prices/{service}")
        {
            Content = System.Net.Http.Json.JsonContent.Create(new { serviceFee = 300, includedInRetainer = false }),
            Headers = { { "X-Test-Identity", TestIdentityService.HeaderPayload(Admin, "Admin", null) } }
        }));
        Assert.Equal(300m, price.GetProperty("serviceFee").GetDecimal());

        var discounted = await CreateJobAsync(JobBody(CompanyA, service, EmployeeA));
        Assert.Equal(300m, discounted.GetProperty("serviceFee").GetDecimal());

        await ScopeClient.SendAsync(new HttpRequestMessage(HttpMethod.Put,
            $"/api/v1/admin/contracts/{ContractA}/service-prices/{service}")
        {
            Content = System.Net.Http.Json.JsonContent.Create(new { serviceFee = 300, includedInRetainer = true }),
            Headers = { { "X-Test-Identity", TestIdentityService.HeaderPayload(Admin, "Admin", null) } }
        });

        var included = await CreateJobAsync(JobBody(CompanyA, service, EmployeeA));
        Assert.Equal(0m, included.GetProperty("serviceFee").GetDecimal());

        var overridden = await CreateJobAsync(JobBody(CompanyA, service, EmployeeA, fee: 99));
        Assert.Equal(99m, overridden.GetProperty("serviceFee").GetDecimal());
    }

    [Fact]
    public async Task Subject_rules_are_enforced()
    {
        var personService = await SeedServiceAsync("SUBJ-PERSON");
        var inactive = await SeedServiceAsync("SUBJ-OFF", active: false);

        await CreateJobAsync(JobBody(CompanyA, personService), HttpStatusCode.Conflict);           // no person
        await CreateJobAsync(JobBody(CompanyA, personService, EmployeeB), HttpStatusCode.Conflict); // other customer
        await CreateJobAsync(JobBody(CompanyA, inactive, EmployeeA), HttpStatusCode.Conflict);      // withdrawn service
        await CreateJobAsync(JobBody(CompanyA, personService, EmployeeA, staff: ClientUserA), HttpStatusCode.Conflict); // not staff

        // A company service picks the only licence automatically.
        var companyService = await SeedServiceAsync("SUBJ-COMPANY", ServiceSubjectType.Company);
        var job = await CreateJobAsync(JobBody(CompanyA, companyService));
        Assert.Equal(EntityA, job.GetProperty("clientEntityId").GetGuid());
    }

    // ================================================================ lifecycle

    [Fact]
    public async Task Job_moves_through_its_lifecycle_and_completion_needs_every_step_closed()
    {
        var service = await SeedServiceAsync("LIFE-1", steps: ["Typing", "Submission"]);
        var job = await CreateJobAsync(JobBody(CompanyA, service, EmployeeA, staff: AgentA));
        var id = job.GetProperty("id").GetGuid();
        var steps = job.GetProperty("steps").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToList();

        // Marking a step done starts a New job.
        var afterStep = await Data(await AdminPatch($"/api/v1/admin/service-jobs/{id}/steps/{steps[0]}",
            new { status = "Done", referenceNumber = "APP-77" }));
        Assert.Equal("InProgress", afterStep.GetProperty("status").GetString());

        var blocked = await AdminPost($"/api/v1/admin/service-jobs/{id}/complete", new { });
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);

        // Hold needs a reason, and steps are frozen while on hold.
        Assert.Equal(HttpStatusCode.BadRequest,
            (await AdminPost($"/api/v1/admin/service-jobs/{id}/hold", new { reason = "" })).StatusCode);
        var held = await Data(await AdminPost($"/api/v1/admin/service-jobs/{id}/hold", new { reason = "Waiting for photo" }));
        Assert.Equal("OnHold", held.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Conflict,
            (await AdminPatch($"/api/v1/admin/service-jobs/{id}/steps/{steps[1]}", new { status = "Skipped" })).StatusCode);

        await Data(await AdminPost($"/api/v1/admin/service-jobs/{id}/resume", new { }));
        await Data(await AdminPatch($"/api/v1/admin/service-jobs/{id}/steps/{steps[1]}", new { status = "Skipped" }));

        var completed = await Data(await AdminPost($"/api/v1/admin/service-jobs/{id}/complete", new { note = "Handed over" }));
        Assert.Equal("Completed", completed.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, completed.GetProperty("completedAt").ValueKind);

        Assert.Equal(HttpStatusCode.Conflict,
            (await AdminPost($"/api/v1/admin/service-jobs/{id}/cancel", new { reason = "late" })).StatusCode);

        // Client contacts were told about the hold and the completion.
        await WithScopeDbContextAsync(db =>
        {
            var titles = db.Notifications.Where(x => x.UserId == ClientUserA).Select(x => x.Title).ToList();
            Assert.Contains("Action may be needed", titles);
            Assert.Contains("Service completed", titles);
            Assert.Contains(db.Notifications.Where(x => x.UserId == AgentA).Select(x => x.Title), t => t == "Job assigned to you");
        });
    }

    [Fact]
    public async Task Completing_a_renewal_job_updates_the_linked_document()
    {
        var service = await SeedServiceAsync("RENEW-PP", renews: DocumentType.Passport, steps: ["Submit"]);
        var job = await CreateJobAsync(JobBody(CompanyA, service, document: DocumentA));
        Assert.Equal(EmployeeA, job.GetProperty("employeeId").GetGuid());

        var id = job.GetProperty("id").GetGuid();
        var step = job.GetProperty("steps")[0].GetProperty("id").GetGuid();
        await Data(await AdminPatch($"/api/v1/admin/service-jobs/{id}/steps/{step}", new { status = "Done" }));

        Assert.Equal(HttpStatusCode.Conflict,
            (await AdminPost($"/api/v1/admin/service-jobs/{id}/complete", new { })).StatusCode);

        var newExpiry = DateTime.UtcNow.Date.AddYears(10);
        await Data(await AdminPost($"/api/v1/admin/service-jobs/{id}/complete",
            new { renewedExpiryDate = newExpiry, renewedDocumentNumber = "NEW123456" }));

        await WithScopeDbContextAsync(db =>
        {
            var document = db.Documents.Single(x => x.Id == DocumentA);
            Assert.Equal(newExpiry, document.ExpiryDate);
            Assert.Equal("NEW123456", document.DocumentNumber);
        });
    }

    [Fact]
    public async Task Renewal_service_rejects_a_document_of_another_type()
    {
        var service = await SeedServiceAsync("RENEW-EID", renews: DocumentType.EmiratesId);
        await CreateJobAsync(JobBody(CompanyA, service, document: DocumentA), HttpStatusCode.Conflict);
    }

    // ================================================================ scope

    [Fact]
    public async Task Agents_only_reach_jobs_assigned_to_them_and_cannot_cancel_or_reprice()
    {
        var service = await SeedServiceAsync("SCOPE-1");
        var job = await CreateJobAsync(JobBody(CompanyA, service, EmployeeA));
        var id = job.GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.NotFound,
            (await ScopeClient.SendAsync(ScopeGet($"/api/v1/agent/service-jobs/{id}", AgentA))).StatusCode);

        await Data(await AdminPatch($"/api/v1/admin/service-jobs/{id}/assign", new { assignedStaffId = AgentA }));

        var mine = await Data(await ScopeClient.SendAsync(ScopeGet("/api/v1/agent/service-jobs", AgentA)));
        Assert.Equal(1, mine.GetProperty("totalCount").GetInt32());
        var theirs = await Data(await ScopeClient.SendAsync(ScopeGet("/api/v1/agent/service-jobs", AgentB)));
        Assert.Equal(0, theirs.GetProperty("totalCount").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound,
            (await ScopeClient.SendAsync(ScopePost($"/api/v1/agent/service-jobs/{id}/start", null, AgentB))).StatusCode);

        var started = await Data(await ScopeClient.SendAsync(ScopePost($"/api/v1/agent/service-jobs/{id}/start", null, AgentA)));
        Assert.Equal("InProgress", started.GetProperty("status").GetString());

        var updated = await Data(await ScopeClient.SendAsync(
            ScopePatch($"/api/v1/agent/service-jobs/{id}", new { applicationNumber = "MB123", notes = "x" }, AgentA)));
        Assert.Equal("MB123", updated.GetProperty("applicationNumber").GetString());

        // No cancel route for agents, and the admin route is role-gated.
        Assert.Equal(HttpStatusCode.Forbidden,
            (await ScopeClient.SendAsync(ScopePost($"/api/v1/admin/service-jobs/{id}/cancel", new { reason = "x" }, AgentA))).StatusCode);
    }

    [Fact]
    public async Task Clients_see_only_their_jobs_without_internal_notes()
    {
        var service = await SeedServiceAsync("CLIENT-1");
        var jobA = await CreateJobAsync(JobBody(CompanyA, service, EmployeeA));
        await CreateJobAsync(JobBody(CompanyB, service, EmployeeB));
        var idA = jobA.GetProperty("id").GetGuid();

        await Data(await AdminPost($"/api/v1/admin/service-jobs/{idA}/notes", new { note = "Internal only", isInternal = true }));
        await Data(await AdminPost($"/api/v1/admin/service-jobs/{idA}/notes", new { note = "Please send your photo", isInternal = false }));

        var list = await Data(await ScopeClient.SendAsync(ScopeGet("/api/v1/client/service-jobs", ClientUserA, "Client", CompanyA.ToString())));
        Assert.Equal(1, list.GetProperty("totalCount").GetInt32());

        var detail = await Data(await ScopeClient.SendAsync(ScopeGet($"/api/v1/client/service-jobs/{idA}", ClientUserA, "Client", CompanyA.ToString())));
        Assert.Equal(JsonValueKind.Null, detail.GetProperty("notes").ValueKind);
        var notes = detail.GetProperty("activities").EnumerateArray()
            .Select(x => x.GetProperty("note").GetString()).ToList();
        Assert.Contains("Please send your photo", notes);
        Assert.DoesNotContain("Internal only", notes);

        Assert.Equal(HttpStatusCode.NotFound,
            (await ScopeClient.SendAsync(ScopeGet($"/api/v1/client/service-jobs/{idA}", ClientUserB, "Client", CompanyB.ToString()))).StatusCode);
    }

    // ================================================================ service requests → jobs

    [Fact]
    public async Task A_new_visa_request_is_converted_into_a_job()
    {
        var service = await SeedServiceAsync("REQ-NEWVISA");

        var request = await Data(await ScopeClient.SendAsync(ScopePost("/api/v1/client/service-requests", new
        {
            type = "NewVisa",
            employeeId = EmployeeA,
            notes = "New hire joining next week",
            serviceDefinitionId = service
        }, ClientUserA, "Client", CompanyA.ToString())), HttpStatusCode.Created);
        var requestId = request.GetProperty("id").GetGuid();

        var converted = await Data(await AdminPost($"/api/v1/admin/service-jobs/from-request/{requestId}",
            new { priority = "Urgent", assignedStaffId = AgentA }), HttpStatusCode.Created);
        var jobId = converted.GetProperty("serviceJobId").GetGuid();

        var job = await Data(await AdminGet($"/api/v1/admin/service-jobs/{jobId}"));
        Assert.Equal("ClientPortal", job.GetProperty("source").GetString());
        Assert.Equal("Urgent", job.GetProperty("priority").GetString());
        Assert.Contains("New hire", job.GetProperty("notes").GetString());

        var listed = await Data(await AdminGet($"/api/v1/service-requests/{requestId}"));
        Assert.Equal("Converted", listed.GetProperty("status").GetString());
        Assert.Equal(jobId, listed.GetProperty("convertedServiceJobId").GetGuid());
        Assert.Equal(converted.GetProperty("jobNumber").GetString(), listed.GetProperty("convertedJobNumber").GetString());

        Assert.Equal(HttpStatusCode.Conflict,
            (await AdminPost($"/api/v1/admin/service-jobs/from-request/{requestId}", new { priority = "Normal" })).StatusCode);
    }

    [Fact]
    public async Task Converting_a_request_without_a_chosen_service_asks_for_one()
    {
        var response = await AdminPost($"/api/v1/admin/service-jobs/from-request/{TestDataSeeder.Ids.ServiceRequestA}",
            new { priority = "Normal" });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ================================================================ fees and summary

    [Fact]
    public async Task Government_fees_are_recorded_on_the_job_and_summary_counts_open_work()
    {
        var service = await SeedServiceAsync("FEE-1");
        var job = await CreateJobAsync(JobBody(CompanyA, service, EmployeeA));
        var id = job.GetProperty("id").GetGuid();
        await CreateJobAsync(JobBody(CompanyA, service, EmployeeA, staff: AgentA));

        var withFee = await Data(await AdminPost($"/api/v1/admin/service-jobs/{id}/government-fees",
            new { feeDescription = "Entry permit fee", governmentReference = "TX-1", amount = 1150.50 }));
        var fee = Assert.Single(withFee.GetProperty("governmentFees").EnumerateArray());
        Assert.Equal(1150.50m, fee.GetProperty("amount").GetDecimal());

        await WithScopeDbContextAsync(db =>
            Assert.Equal(CompanyA, db.GovFeeDisbursements.Single(x => x.ServiceJobId == id).ClientCompanyId));

        var summary = await Data(await AdminGet("/api/v1/admin/service-jobs/summary"));
        Assert.Equal(2, summary.GetProperty("new").GetInt32());
        Assert.Equal(1, summary.GetProperty("unassigned").GetInt32());
        Assert.Equal(2, summary.GetProperty("openTotal").GetInt32());

        var agentSummary = await Data(await ScopeClient.SendAsync(ScopeGet("/api/v1/agent/service-jobs/summary", AgentA)));
        Assert.Equal(1, agentSummary.GetProperty("openTotal").GetInt32());
    }

    [Fact]
    public async Task Admin_list_filters_by_search_and_status()
    {
        var service = await SeedServiceAsync("LIST-1");
        var job = await CreateJobAsync(JobBody(CompanyA, service, EmployeeA));
        await CreateJobAsync(JobBody(CompanyB, service, EmployeeB));
        var number = job.GetProperty("jobNumber").GetString();

        var bySearch = await Data(await AdminGet($"/api/v1/admin/service-jobs?search={number}"));
        Assert.Equal(1, bySearch.GetProperty("totalCount").GetInt32());

        var byCompany = await Data(await AdminGet($"/api/v1/admin/service-jobs?clientCompanyId={CompanyB}"));
        Assert.Equal(1, byCompany.GetProperty("totalCount").GetInt32());

        var completed = await Data(await AdminGet("/api/v1/admin/service-jobs?status=Completed"));
        Assert.Equal(0, completed.GetProperty("totalCount").GetInt32());
    }
}
