using MediatR;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Documents.Queries.GetExpiringDocuments;

namespace PROManagementSystem.Application.Agent.Queries.GetAgentExpiringDocuments;

public sealed class GetAgentExpiringDocumentsQueryHandler
    : IRequestHandler<
        GetAgentExpiringDocumentsQuery,
        GetExpiringDocumentsResponseDto>
{
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
        // Days is validated by GetAgentExpiringDocumentsQueryValidator, which
        // is the single authority for the 1..365 range. The pipeline behaviour
        // runs that validator before this handler, so a Days value reaching
        // here has already been accepted. This handler previously also
        // re-checked Days against a hard-coded { 30, 60, 90 } whitelist, which
        // contradicted the validator: any value the validator permitted outside
        // those three constants was rejected further down with a 400 whose
        // message disagreed with the validator's own. The repository window is
        // parameterised (@Days * INTERVAL '1 day'), so arbitrary widths are
        // supported. The Agent UI still offers only 30/60/90; that is a product
        // choice about which windows to surface, not an API restriction.
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
