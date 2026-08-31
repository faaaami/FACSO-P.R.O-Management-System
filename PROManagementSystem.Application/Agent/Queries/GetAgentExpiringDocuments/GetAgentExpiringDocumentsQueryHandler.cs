using MediatR;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Documents.Queries.GetExpiringDocuments;

namespace PROManagementSystem.Application.Agent.Queries.GetAgentExpiringDocuments;

public sealed class GetAgentExpiringDocumentsQueryHandler
    : IRequestHandler<
        GetAgentExpiringDocumentsQuery,
        GetExpiringDocumentsResponseDto>
{
    private static readonly int[] s_allowedDays = { 30, 60, 90 };

    private readonly IRenewalTaskReadRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public GetAgentExpiringDocumentsQueryHandler(
        IRenewalTaskReadRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<GetExpiringDocumentsResponseDto> Handle(
        GetAgentExpiringDocumentsQuery request,
        CancellationToken cancellationToken)
    {
        if (!s_allowedDays.Contains(request.Days))
        {
            throw new ArgumentException(
                "Days must be one of: 30, 60, 90.");
        }

        if (request.Page < 1)
        {
            throw new ArgumentException(
                "Page must be greater than or equal to 1.");
        }

        var agentId = _currentUser.UserId!.Value;

        return await _repository.GetAgentExpiringDocumentsAsync(
            agentId,
            request.Days,
            request.Page,
            request.PageSize,
            request.IncludeExpired,
            cancellationToken);
    }
}
