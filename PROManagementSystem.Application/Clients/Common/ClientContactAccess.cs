using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Exceptions;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Entities;

namespace PROManagementSystem.Application.Clients.Common;

/// <summary>
/// Who may invite contacts for a client company: any Admin, or an Agent who
/// has at least one renewal task for that company assigned to them (the same
/// scope that decides which clients an Agent can see).
/// </summary>
internal static class ClientContactAccess
{
    public static async Task<ClientCompany> GetInvitableCompanyAsync(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        Guid clientCompanyId,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAdmin &&
            !string.Equals(currentUser.Role, "Agent", StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenAccessException(
                "Only staff can invite client contacts.");
        }

        var company = await context.ClientCompanies
            .FirstOrDefaultAsync(
                x => x.Id == clientCompanyId,
                cancellationToken);

        if (company is null)
        {
            throw new KeyNotFoundException(
                "Client company was not found.");
        }

        if (currentUser.IsAdmin)
        {
            return company;
        }

        var agentId = currentUser.UserId
            ?? throw new UnauthorizedAccessException(
                "Authenticated user was not found.");

        var isAssigned = await context.RenewalTasks
            .AnyAsync(
                x => x.ClientCompanyId == clientCompanyId &&
                     x.AssignedStaffId == agentId,
                cancellationToken);

        // Same 404 as a missing company, so an Agent cannot probe for
        // clients outside their scope.
        if (!isAssigned)
        {
            throw new KeyNotFoundException(
                "Client company was not found.");
        }

        return company;
    }
}
