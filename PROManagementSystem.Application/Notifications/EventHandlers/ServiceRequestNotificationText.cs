using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.DomainEvents;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Notifications.EventHandlers;

/// <summary>
/// Plain-language wording for service-request notifications: the company,
/// the service and the person it is for, never internal ids.
/// </summary>
internal static class ServiceRequestNotificationText
{
    public sealed record Details(string CompanyName, string Subject);

    public static async Task<Details> DescribeAsync(
        IApplicationDbContext context,
        ServiceRequestSubmittedEvent request,
        CancellationToken cancellationToken)
    {
        var companyName = await context.ClientCompanies
            .Where(x => x.Id == request.ClientCompanyId)
            .Select(x => x.CompanyName)
            .FirstOrDefaultAsync(cancellationToken);

        var serviceName = await context.ServiceRequests
            .Where(x => x.Id == request.ServiceRequestId &&
                        x.ServiceDefinitionId != null)
            .Join(
                context.ServiceDefinitions,
                r => r.ServiceDefinitionId,
                d => (Guid?)d.Id,
                (r, d) => d.Name)
            .FirstOrDefaultAsync(cancellationToken);

        var employeeName = request.EmployeeId.HasValue
            ? await context.Employees
                .Where(x => x.Id == request.EmployeeId.Value)
                .Select(x => x.FullName)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        var service = !string.IsNullOrWhiteSpace(serviceName)
            ? serviceName
            : TypeLabel(request.Type);

        var subject = string.IsNullOrWhiteSpace(employeeName)
            ? service
            : $"{service} for {employeeName}";

        return new Details(
            string.IsNullOrWhiteSpace(companyName) ? "A client" : companyName,
            subject);
    }

    private static string TypeLabel(ServiceRequestType type) => type switch
    {
        ServiceRequestType.NewVisa => "a new visa",
        ServiceRequestType.EarlyRenewal => "an early renewal",
        _ => "a service"
    };
}
