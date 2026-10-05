using MediatR;
using PROManagementSystem.Application.Documents.Queries.GetExpiringDocuments;

namespace PROManagementSystem.Application.Client.Queries.GetMyExpiringDocuments;

public sealed record GetMyExpiringDocumentsQuery(
    int Days = 30,
    int Page = 1,
    int PageSize = 20,
    bool IncludeExpired = false
) : IRequest<GetExpiringDocumentsResponseDto>;