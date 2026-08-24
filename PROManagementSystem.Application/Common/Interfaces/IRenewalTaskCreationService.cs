namespace PROManagementSystem.Application.Common.Interfaces;

public interface IRenewalTaskCreationService
{
    Task<Guid?> CreateAsync(
        Guid documentId,
        Guid changedBy,
        CancellationToken cancellationToken);
}