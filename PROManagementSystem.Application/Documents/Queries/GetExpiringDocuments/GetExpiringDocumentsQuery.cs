using MediatR;

namespace PROManagementSystem.Application.Documents.Queries.GetExpiringDocuments;

public sealed record GetExpiringDocumentsQuery(
    int Days,
    int Page,
    int PageSize,
    bool IncludeExpired
) : IRequest<GetExpiringDocumentsResponseDto>;