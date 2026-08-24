using FluentValidation;
using Hangfire;
using Hangfire.PostgreSql;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Npgsql;
using PROManagementSystem.Api.Common;
using PROManagementSystem.Api.Common.Middlewares;
using PROManagementSystem.Api.Common.Services;
using PROManagementSystem.Api.Hangfire;
using PROManagementSystem.Api.Hubs;
using PROManagementSystem.Application.Auth.Commands.Login;
using PROManagementSystem.Application.Clients.Queries.GetClientCompanies;
using PROManagementSystem.Application.Common.Behaviours;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Common.Jobs;
using PROManagementSystem.Application.Common.Services;
using PROManagementSystem.Infrastructure.AI;
using PROManagementSystem.Infrastructure.Email;
using PROManagementSystem.Infrastructure.Identity;
using PROManagementSystem.Infrastructure.Persistence;
using PROManagementSystem.Infrastructure.Persistence.Repositories;
using PROManagementSystem.Infrastructure.Pdf;
using Serilog;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog();

// ========================================
// Database
// ========================================

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString(
            "DefaultConnection")));

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
    IDocumentExtractionService,
    MockDocumentExtractionService>();


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
    new DocumentExpiryJob(
        sp.GetRequiredService<IDocumentExpiryReadRepository>(),
        sp.GetRequiredService<IRenewalTaskCreationService>(),
        sp.GetRequiredService<ILogger<DocumentExpiryJob>>(),
        parsedAutomationUserId));
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
// SignalR
// ========================================

builder.Services.AddSignalR();

builder.Services.AddScoped<
    IRealtimeNotificationService,
    RealtimeNotificationService>();
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

app.UseHttpsRedirection();

// Catch exceptions from everything below this point
app.UseMiddleware<GlobalExceptionMiddleware>();
// Authenticate JWT
app.UseAuthentication();

// Add UserId / Role / RequestId to logging context
app.UseMiddleware<UserContextLoggingMiddleware>();
// Check authorization policies/roles
app.UseAuthorization();

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