using MediatR;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Client.Queries.GetMyDocuments;

public sealed record GetMyDocumentsQuery(
    int Page = 1,
    int PageSize = 20,
    DocumentType? Type = null,
    DocumentStatus? Status = null,
    DateTime? ExpiresFrom = null,
    DateTime? ExpiresTo = null,
    DocumentOwnerType? OwnerType = null
) : IRequest<GetClientDocumentsResponseDto>;