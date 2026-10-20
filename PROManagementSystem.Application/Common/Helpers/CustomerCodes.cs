using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Common.Models;

namespace PROManagementSystem.Application.Common.Helpers;

public static class CustomerCodes
{
    /// <summary>Next counter code, e.g. C-000124.</summary>
    public static async Task<string> NextAsync(IApplicationDbContext context, CancellationToken cancellationToken)
    {
        var value = await context.NextSequenceValueAsync(SequenceNames.CustomerCode, cancellationToken);
        return $"C-{value:D6}";
    }
}
