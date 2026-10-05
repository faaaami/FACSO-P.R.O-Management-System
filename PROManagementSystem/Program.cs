using System.Globalization;
using FluentValidation;
using Hangfire;
using Hangfire.PostgreSql;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Npgsql;
using PROManagementSystem.Api.Common;
using PROManagementSystem.Api.Common.Middlewares;
using PROManagementSystem.Api.Common.Services;
using PROManagementSystem.Api.Hangfire;
using PROManagementSystem.Api.Hubs;
using PROManagementSystem.Api.RateLimiting;
using PROManagementSystem.Application.Auth.Commands.Login;
using PROManagementSystem.Application.Clients.Queries.GetClientCompanies;
using PROManagementSystem.Application.Common.Behaviours;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Common.Jobs;
using PROManagementSystem.Application.Common.Realtime;
using PROManagementSystem.Application.Common.Services;
using PROManagementSystem.Application.Common.DocumentSchemas;
using PROManagementSystem.Application.Common.Validation;
using PROManagementSystem.Infrastructure.AI;
using PROManagementSystem.Infrastructure.AI.DocumentClassification;
using PROManagementSystem.Infrastructure.AI.DocumentClassification.RuleBasedSchemas;
using PROManagementSystem.Infrastructure.AI.DocumentExtractionService;
using PROManagementSystem.Infrastructure.Common;
using PROManagementSystem.Infrastructure.Email;
using PROManagementSystem.Infrastructure.Extraction;
using PROManagementSystem.Infrastructure.Identity;
using PROManagementSystem.Infrastructure.Persistence;
using PROManagementSystem.Infrastructure.Persistence.Repositories;
using PROManagementSystem.Infrastructure.Persistence.Seeders;
using PROManagementSystem.Infrastructure.Pdf;
using PROManagementSystem.Infrastructure.Storage;
using Serilog;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using System.Text.Json.Serialization;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile(
    "appsettings.Local.json",
    optional: true,
    reloadOnChange: true);


builder.Host.UseSerilog();

// ========================================
// Database
// ========================================

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString(
            "DefaultConnection")));

// Task 7B.6.4: centralized translation of the database-level document
// uniqueness violation into DuplicateDocumentException (409). Stateless, so
// a singleton is sufficient.
builder.Services.AddSingleton<
    IDuplicateDocumentTranslator,
    NpgsqlDuplicateDocumentTranslator>();

// ========================================
// Hangfire
// ========================================


builder.Services.AddHangfire(config =>
    config.UsePostgreSqlStorage(options =>
        options.UseNpgsqlConnection(
            builder.Configuration.GetConnectionString(
                "DefaultConnection")
            ?? throw new InvalidOperationException(
                "DefaultConnection is not configured."))));

builder.Services.AddHangfireServer();

// ========================================
// Dapper
// ========================================

builder.Services.AddSingleton(
    NpgsqlDataSource.Create(
        builder.Configuration.GetConnectionString(
            "DefaultConnection")
        ?? throw new InvalidOperationException(
            "DefaultConnection is not configured.")));

builder.Services.AddScoped<
    IUserReadRepository,
    UserReadRepository>();

builder.Services.AddScoped<
    INotificationReadRepository,
    NotificationReadRepository>();


builder.Services.AddScoped<
    IClientCompanyReadRepository,
    ClientCompanyReadRepository>();

builder.Services.AddScoped<
    IClientEntityReadRepository,
    ClientEntityReadRepository>();


builder.Services.AddScoped<
    IClientContactReadRepository,
    ClientContactReadRepository>();

builder.Services.AddScoped<
    IEmployeeReadRepository,
    EmployeeReadRepository>();

builder.Services.AddScoped<IEmployeeDocumentReadRepository,
    EmployeeDocumentReadRepository>();

builder.Services.AddScoped<
    IClientEntityDocumentReadRepository,
    ClientEntityDocumentReadRepository>();

builder.Services.AddScoped<
    IDocumentReadRepository,
    DocumentReadRepository>();

builder.Services.AddScoped<
    IDocumentVersionReadRepository,
    DocumentVersionReadRepository>();

builder.Services.AddScoped<
    IDocumentExpiryReadRepository,
    DocumentExpiryReadRepository>();

builder.Services.AddScoped<
    IEmployeeExpiryReadRepository,
    EmployeeExpiryReadRepository>();

builder.Services.AddScoped<
    IEmployeeTimelineReadRepository,
    EmployeeTimelineReadRepository>();

builder.Services.AddScoped<
    IRenewalTaskReadRepository,
    RenewalTaskReadRepository>();

builder.Services.AddScoped<
    IRenewalTaskCreationService,
    RenewalTaskCreationService>();

builder.Services.AddScoped<
    IServiceRequestReadRepository,
    ServiceRequestReadRepository>();

builder.Services.AddScoped<
    IRetainerInvoiceReadRepository,
    RetainerInvoiceReadRepository>();

builder.Services.AddScoped<
    IServiceFeeInvoiceReadRepository,
    ServiceFeeInvoiceReadRepository>();

builder.Services.AddScoped<
    IServiceFeeInvoiceCreationService,
    ServiceFeeInvoiceCreationService>();

builder.Services.AddScoped<
    IGovFeeDisbursementReadRepository,
    GovFeeDisbursementReadRepository>();

builder.Services.AddScoped<
    IPaymentOrderReadRepository,
    PaymentOrderReadRepository>();

builder.Services.AddScoped<
    IAuditLogReadRepository,
    AuditLogReadRepository>();

builder.Services.AddScoped<
    IDashboardReadRepository,
    DashboardReadRepository>();

builder.Services.AddScoped<
    IClientServiceReportReadRepository,
    ClientServiceReportReadRepository>();

builder.Services.AddScoped<
    IReportPdfService,
    QuestReportPdfService>();

builder.Services.Configure<PROManagementSystem.Infrastructure.Payment.RazorpayOptions>(
    builder.Configuration.GetSection("Razorpay"));

builder.Services.AddHttpClient<PROManagementSystem.Application.Common.Interfaces.IRazorpayService,
    PROManagementSystem.Infrastructure.Payment.RazorpayService>(client =>
{
    client.BaseAddress = new Uri("https://api.razorpay.com/");
})
.AddStandardResilienceHandler();

builder.Services.AddScoped<
    IInvoicePdfService,
    QuestInvoicePdfService>();

var automationUserId =
    builder.Configuration["RenewalAutomation:UserId"];

if (!Guid.TryParse(
        automationUserId,
        out var parsedAutomationUserId))
{
    throw new InvalidOperationException(
        "RenewalAutomation:UserId is not configured correctly.");
}

builder.Services.AddScoped<DocumentExpiryJob>(sp =>
{
    var resolvedAutomationUserId =
        sp.GetRequiredService<IConfiguration>()["RenewalAutomation:UserId"];

    if (!Guid.TryParse(
            resolvedAutomationUserId,
            out var automationUserId))
    {
        throw new InvalidOperationException(
            "RenewalAutomation:UserId is not configured correctly.");
    }

    return new DocumentExpiryJob(
        sp.GetRequiredService<IDocumentExpiryReadRepository>(),
        sp.GetRequiredService<IRenewalTaskCreationService>(),
        sp.GetRequiredService<ILogger<DocumentExpiryJob>>(),
        automationUserId);
});

builder.Services.AddScoped<DocumentOverdueSweepJob>(sp =>
{
    var resolvedAutomationUserId =
        sp.GetRequiredService<IConfiguration>()["RenewalAutomation:UserId"];

    if (!Guid.TryParse(
            resolvedAutomationUserId,
        out var overdueSweepAutomationUserId))
    {
        throw new InvalidOperationException(
            "RenewalAutomation:UserId is not configured correctly.");
    }

    return new DocumentOverdueSweepJob(
        sp.GetRequiredService<IDocumentExpiryReadRepository>(),
        sp.GetRequiredService<IRenewalTaskCreationService>(),
        sp.GetRequiredService<ILogger<DocumentOverdueSweepJob>>(),
        overdueSweepAutomationUserId);
});

builder.Services.AddScoped<ExtractionExpiryCleanupJob>(sp =>
{
    var resolvedAutomationUserId =
        sp.GetRequiredService<IConfiguration>()["RenewalAutomation:UserId"];

    if (!Guid.TryParse(
            resolvedAutomationUserId,
            out var extractionCleanupAutomationUserId))
    {
        throw new InvalidOperationException(
            "RenewalAutomation:UserId is not configured correctly.");
    }

    return new ExtractionExpiryCleanupJob(
        sp.GetRequiredService<IApplicationDbContext>(),
        sp.GetRequiredService<IFileStorageService>(),
        sp.GetRequiredService<ILogger<ExtractionExpiryCleanupJob>>(),
        extractionCleanupAutomationUserId);
});
// ========================================
// Redis
// ========================================

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration =
        builder.Configuration.GetConnectionString("Redis")
        ?? throw new InvalidOperationException(
            "Redis connection string is not configured.");

    options.InstanceName = "PROManagementSystem:";
});


// ========================================
// Health Checks
// ========================================

builder.Services
    .AddHealthChecks()
    .AddNpgSql(
        builder.Configuration.GetConnectionString(
            "DefaultConnection")
        ?? throw new InvalidOperationException(
            "DefaultConnection is not configured."),
        name: "postgresql")
    .AddRedis(
        builder.Configuration.GetConnectionString("Redis")
        ?? throw new InvalidOperationException(
            "Redis connection string is not configured."),
        name: "redis");

// ========================================
// Application database abstraction
// ========================================

builder.Services.AddScoped<IApplicationDbContext>(
    provider =>
        provider.GetRequiredService<ApplicationDbContext>());

// ========================================
// Password hashing
// ========================================

builder.Services.AddScoped<
    IPasswordHasher,
    BCryptPasswordHasher>();

// ========================================
// JWT token service
// ========================================

builder.Services.AddScoped<
    ITokenService,
    JwtTokenService>();

// ========================================
// Email service
// ========================================

builder.Services.AddScoped<
    IEmailService,
    DevelopmentEmailService>();

// ========================================
// Local file storage
// ========================================

builder.Services.Configure<StorageOptions>(
    builder.Configuration.GetSection("Storage"));

// Resolve a relative Storage:RootPath against the host content root so the
// runtime storage directory is deterministic regardless of the process
// working directory. Absolute roots (e.g. test temp dirs) pass through.
builder.Services.PostConfigure<StorageOptions>(
    options =>
    {
        if (!Path.IsPathRooted(options.RootPath))
        {
            options.RootPath = Path.GetFullPath(
                options.RootPath,
                builder.Environment.ContentRootPath);
        }
    });

builder.Services.AddScoped<
    IFileStorageService,
    LocalFileStorageService>();

// ========================================
// Document extraction provider
// ========================================

builder.Services.Configure<OcrOptions>(
    builder.Configuration.GetSection("Extraction"));

// OpenRouter provider configuration (Task 7C.1). Foundation only: the options
// class carries safe, non-secret defaults so booting with the default Mock
// provider and no API key stays untouched.
builder.Services.Configure<OpenRouterOptions>(
    builder.Configuration.GetSection("Extraction:OpenRouter"));

// OpenRouter extraction provider foundation (Task 7C.3.1). The typed client
// follows the project's existing HttpClient convention (see Razorpay) and
// carries an explicit, cautious resilience policy that retries ONLY clearly
// retryable failures - network/transport errors, HTTP 429 and HTTP 5xx - so
// an ordinary 4xx is never re-fired and a single model call is not blindly
// repeated and charged more than once. HttpClient.Timeout is the sole request
// deadline, sourced from RequestTimeoutSeconds (no second timeout). The
// endpoint, timeout and retry budget are all read lazily from IOptions when
// the client/handler is built (Task 7C.3.4) so no value comes from a startup
// snapshot.
builder.Services.AddOpenRouterExtractionProvider();

builder.Services.AddScoped<MockDocumentExtractionService>();
builder.Services.AddScoped<LocalDocumentExtractionService>();
builder.Services.AddScoped<IPdfTextExtractor, PdfTextExtractor>();
builder.Services.AddScoped<IImageOcrExtractor, TesseractImageOcrExtractor>();
builder.Services.AddScoped<IPdfPageRenderer, PdfiumPdfPageRenderer>();
builder.Services.AddScoped<ScannedPdfOcrService>();

// Process-wide cap on simultaneous heavy OCR operations (Task 6B.7). The gate
// is shared by every Local extraction so concurrent scans queue for a slot,
// regardless of which request scope owns the current operation.
builder.Services.AddSingleton<OcrWorkloadGate>();

// Deterministic document classifier used by the Local extraction provider
// (Task 6C). Stateless so singleton is correct.
builder.Services.AddSingleton<
    IDocumentClassifier,
    RuleBasedDocumentClassifier>();

// Deterministic type-specific document field schemas (Task 7B.1). Static
// immutable metadata so singleton is correct.
builder.Services.AddSingleton<
    IDocumentTypeSchemaProvider,
    DocumentTypeSchemaProvider>();

// Applies the type-specific details schema to Details payloads (Task 7B.5).
// Stateless and depends only on the singleton schema provider.
builder.Services.AddSingleton<DocumentDetailsValidator>();

// Deterministic, schema-driven type-specific field extraction for the Local
// extraction provider (Task 7B.5). Stateless; depends only on the schema
// provider and validator, both singletons.
builder.Services.AddSingleton<
    IDocumentDetailsExtractor,
    RuleBasedDocumentDetailsExtractor>();

// "Extraction:Provider" selects the provider (default "Mock"); provider names
// compare case-insensitively, matching the configuration conventions used
// across the codebase.
// "Local" runs the in-process extractor that reads the text layer of
// selectable PDFs (Task 6B.3), recognises raster JPEG/PNG images (6B.4) and
// rasterises scanned PDFs through local OCR (Task 6B.5).
builder.Services.AddScoped<IDocumentExtractionService>(serviceProvider =>
{
    var provider = serviceProvider
        .GetRequiredService<IOptions<OcrOptions>>()
        .Value
        .Provider;

    var selectsLocal = OcrOptions.ProviderLocal.Equals(
        provider,
        StringComparison.OrdinalIgnoreCase);

    if (selectsLocal)
    {
        return serviceProvider.GetRequiredService<LocalDocumentExtractionService>();
    }

    var selectsOpenRouter = OcrOptions.ProviderOpenRouter.Equals(
        provider,
        StringComparison.OrdinalIgnoreCase);

    if (selectsOpenRouter)
    {
        // Task 7C.3.1: OpenRouter is opt-in. An incomplete or unconfigured
        // selection is a configuration error, never a silent fallback to the
        // Mock provider. The missing settings are named; values — including
        // the API key — are never logged or echoed.
        var openRouter = serviceProvider
            .GetRequiredService<IOptions<OpenRouterOptions>>()
            .Value;

        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(openRouter.Endpoint))
        {
            missing.Add("Extraction:OpenRouter:Endpoint");
        }

        if (string.IsNullOrWhiteSpace(openRouter.Model))
        {
            missing.Add("Extraction:OpenRouter:Model");
        }

        if (string.IsNullOrWhiteSpace(openRouter.ApiKey))
        {
            missing.Add("Extraction:OpenRouter:ApiKey");
        }

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                "The OpenRouter extraction provider is selected but its " +
                "configuration is incomplete. Missing settings: " +
                string.Join(", ", missing) +
                ". Configure them before enabling the OpenRouter provider.");
        }

        return serviceProvider.GetRequiredService<OpenRouterDocumentExtractionService>();
    }

    return serviceProvider.GetRequiredService<MockDocumentExtractionService>();
});

// ========================================
// MediatR / CQRS
// ========================================

builder.Services.AddMediatR(
    cfg =>
        cfg.RegisterServicesFromAssembly(
            typeof(LoginCommand).Assembly));

// ========================================
// FluentValidation
// ========================================

builder.Services.AddValidatorsFromAssembly(
    typeof(LoginCommand).Assembly);

builder.Services.AddTransient(
    typeof(IPipelineBehavior<,>),
    typeof(ValidationBehaviour<,>));

// ========================================
// Current User
// ========================================

builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<
    ICurrentUserService,
    CurrentUserService>();

// ========================================
// JWT Authentication
// ========================================

builder.Services
    .AddAuthentication(
        JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtSettings =
            builder.Configuration.GetSection("Jwt");

        var secret = jwtSettings["Secret"]
            ?? throw new InvalidOperationException(
                "JWT Secret is not configured.");

        var issuer = jwtSettings["Issuer"]
            ?? throw new InvalidOperationException(
                "JWT Issuer is not configured.");

        var audience = jwtSettings["Audience"]
            ?? throw new InvalidOperationException(
                "JWT Audience is not configured.");

        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,

                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(secret)),

                ValidateIssuer = true,
                ValidIssuer = issuer,

                ValidateAudience = true,
                ValidAudience = audience,

                ValidateLifetime = true,

                ClockSkew = TimeSpan.Zero
            };
    });


//CORS 

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// ========================================
// Controllers
// ========================================

builder.Services
    .AddControllers()
    .AddMvcOptions(options =>
    {
        options.Filters.Add<ApiResponseFilter>();
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter());
    });


// ========================================
// SignalR + Redis backplane
// ========================================

builder.Services
    .AddSignalR()
    .AddStackExchangeRedis(
        builder.Configuration.GetConnectionString("Redis")
        ?? throw new InvalidOperationException(
            "Redis connection string is not configured."),
        options =>
        {
            options.Configuration.ChannelPrefix =
                StackExchange.Redis.RedisChannel.Literal(
                    "PROManagementSystem:signalr");
        });

builder.Services.AddScoped<
    IRealtimeNotificationService,
    RealtimeNotificationService>();

// P10.6-B: scoped so a completion handler and the command handler that owns its
// transaction share one queue per request; the owner flushes it after commit.
builder.Services.AddScoped<
    IPendingNotificationPushQueue,
    PendingNotificationPushQueue>();
// ========================================
// Swagger
// ========================================

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition(
        "Bearer",
        new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description =
                "Enter your JWT access token."
        });

    options.AddSecurityRequirement(document =>
        new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(
                "Bearer",
                document)] = []
        });

    // Documents the Agent PATCH document request body (OpenAPI-only,
    // because the endpoint parses the raw JSON body manually).
    options.OperationFilter<AgentPatchRequestBodyFilter>();
});

// ========================================
// Rate limiting (H2)
// ========================================
// Anonymous authentication endpoints share one fixed-window budget per
// client IP. Invalid or non-positive configuration falls back to safe
// positive defaults so the endpoint can never become unlimited. The options
// are resolved from the container (not captured eagerly) so hosts can
// post-configure them, which the rate-limit tests rely on.
builder.Services.Configure<AuthenticationRateLimitOptions>(options =>
{
    var resolved =
        AuthenticationRateLimiting.Resolve(builder.Configuration);

    options.PermitLimit = resolved.PermitLimit;
    options.WindowSeconds = resolved.WindowSeconds;
    options.QueueLimit = resolved.QueueLimit;
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode =
        StatusCodes.Status429TooManyRequests;

    options.AddPolicy(
        AuthenticationRateLimiting.PolicyName,
        httpContext =>
        {
            var limits = httpContext.RequestServices
                .GetRequiredService<
                    IOptionsMonitor<AuthenticationRateLimitOptions>>()
                .CurrentValue;

            return RateLimitPartition.GetFixedWindowLimiter(
                AuthenticationRateLimiting.ResolvePartitionKey(
                    httpContext),
                _ => AuthenticationRateLimiting.ToLimiterOptions(
                    limits));
        });

    options.OnRejected = async (context, cancellationToken) =>
    {
        var response = context.HttpContext.Response;
        response.ContentType = "application/json";

        // Fixed-window leases do not always surface Retry-After metadata, so
        // fall back to the configured window to always tell clients when to
        // retry without leaking any account information.
        var retryAfter = context.Lease.TryGetMetadata(
            MetadataName.RetryAfter,
            out var metadata)
            ? metadata
            : TimeSpan.FromSeconds(
                context.HttpContext.RequestServices
                    .GetRequiredService<
                        IOptionsMonitor<AuthenticationRateLimitOptions>>()
                    .CurrentValue.WindowSeconds);

        response.Headers.RetryAfter = Math.Max(
                1,
                (int)Math.Ceiling(retryAfter.TotalSeconds))
            .ToString(CultureInfo.InvariantCulture);

        var payload = ApiResponse<object>.FailureResponse(
            AuthenticationRateLimiting.ErrorCode,
            AuthenticationRateLimiting.ErrorMessage);

        payload.StatusCode =
            StatusCodes.Status429TooManyRequests;

        await response.WriteAsJsonAsync(payload, cancellationToken);
    };
});

// ========================================
// Build
// ========================================

var app = builder.Build();


// ========================================
// Hangfire
// ========================================
using (var scope = app.Services.CreateScope())
{
    var recurringJobManager =
        scope.ServiceProvider
            .GetRequiredService<IRecurringJobManager>();

    recurringJobManager.AddOrUpdate<DocumentExpiryJob>(
        "document-expiry-scanner",
        job => job.ExecuteAsync(CancellationToken.None),
        Cron.Daily);

    // Bounded overdue recovery. Deliberately a separate job from
    // "document-expiry-scanner" rather than another mode of it, so the
    // proactive 90-day window above keeps exactly the semantics it has today.
    // The two windows cannot collide — proactive is expiry >= now, overdue is
    // expiry < now — so sharing Cron.Daily is safe.
    recurringJobManager.AddOrUpdate<DocumentOverdueSweepJob>(
        "document-overdue-sweep",
        job => job.ExecuteAsync(CancellationToken.None),
        Cron.Daily);

    recurringJobManager.AddOrUpdate<ExtractionExpiryCleanupJob>(
        "extraction-expiry-cleanup",
        job => job.ExecuteAsync(CancellationToken.None),
        Cron.Daily);
}

// ========================================
// Development mock data seeding
// ========================================
// Seeds the realistic mock client company ("FAMIL TECHNOLOGY SOLUTIONS
// L.L.C") used for manual end-to-end testing. Guarded by BOTH the
// Development environment AND the Seeding:EnableMockClientData flag, which
// exists only in appsettings.Local.json. Production never seeds, and the
// seeder is idempotent so restarts are safe.
if (app.Environment.IsDevelopment() &&
    app.Configuration.GetValue<bool?>(
        "Seeding:EnableMockClientData") == true)
{
    using var seedScope = app.Services.CreateScope();
    var seedDbContext =
        seedScope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

    MockClientCompanySeeder.Seed(
        seedDbContext,
        parsedAutomationUserId);

    MockFamilfaClientSeeder.Seed(seedDbContext);
}

// ========================================
// Automation system user seeding
// ========================================
// The automated document-expiry job records its actor
// (RenewalAutomation:UserId) on renewal_task_histories.ChangedBy, a required
// foreign key to the users table, so the configured actor must be a real
// user row wherever that job can run. In development the mock-data seeder
// above already creates the account; everywhere else the app idempotently
// seeds this single system account on boot. Test hosts run under
// Development and rely on their own seed harness, which keeps the
// pre-migration host boot from touching the schema.
if (!app.Environment.IsDevelopment())
{
    using var automationSeedScope = app.Services.CreateScope();
    var automationSeedDbContext =
        automationSeedScope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

    var resolvedAutomationUserId =
        app.Configuration["RenewalAutomation:UserId"];

    if (!Guid.TryParse(
            resolvedAutomationUserId,
            out var seededAutomationUserId))
    {
        throw new InvalidOperationException(
            "RenewalAutomation:UserId is not configured correctly.");
    }

    AutomationUserSeeder.Seed(
        automationSeedDbContext,
        seededAutomationUserId);
}

// ========================================
// Serilog request logging
// ========================================

app.UseSerilogRequestLogging();

// ========================================
// Swagger
// ========================================

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ========================================
// HTTP pipeline
// ========================================

// Explicit routing guarantees endpoint metadata (including the auth
// rate-limit policy) is resolved before the rate limiter runs.
app.UseRouting();

app.UseCors("Frontend");

app.UseHttpsRedirection();

// Catch exceptions from everything below this point
app.UseMiddleware<GlobalExceptionMiddleware>();
// Authenticate JWT
app.UseAuthentication();

// Add UserId / Role / RequestId to logging context
app.UseMiddleware<UserContextLoggingMiddleware>();
// Check authorization policies/roles
app.UseAuthorization();

// Enforce the anonymous authentication rate-limit policy
app.UseRateLimiter();

// Hangfire dashboard (after auth)
app.UseHangfireDashboard("/hangfire",
    new DashboardOptions
    {
        DashboardTitle = "PRO Management System - Hangfire",
        Authorization = new[]
        {
            new HangfireDashboardAuthorizationFilter()
        }
    });

// Controllers
app.MapControllers();

app.MapHub<NotificationHub>(
    "/hubs/notifications");

app.MapHealthChecks(
    "/api/v1/health",
    new HealthCheckOptions
    {
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType =
                "application/json";

            var response = new
            {
                status = report.Status.ToString(),
                checks = report.Entries.Select(entry => new
                {
                    name = entry.Key,
                    status = entry.Value.Status.ToString(),
                    description = entry.Value.Description,
                    duration = entry.Value.Duration.ToString()
                })
            };

            await context.Response.WriteAsync(
                JsonSerializer.Serialize(response));
        }
    });
app.Run();

public partial class Program { }