using MediatR;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Agent.Queries.GetAgentDocuments;

public sealed record GetAgentDocumentsQuery(
    DocumentType? Type,
    DocumentStatus? Status,
    DateTime? ExpiresBefore,
    int Page,
    int PageSize
) : IRequest<GetAgentDocumentsResponseDto>;
