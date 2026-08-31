using MediatR;
using PROManagementSystem.Application.Documents.Queries.GetExpiringDocuments;

namespace PROManagementSystem.Application.Agent.Queries.GetAgentExpiringDocuments;

public sealed record GetAgentExpiringDocumentsQuery(
    int Days,
    int Page,
    int PageSize,
    bool IncludeExpired
) : IRequest<GetExpiringDocumentsResponseDto>;
