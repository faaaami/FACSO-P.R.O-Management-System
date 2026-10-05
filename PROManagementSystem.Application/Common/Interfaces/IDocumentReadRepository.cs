using PROManagementSystem.Application.Client.Queries.GetMyDocuments;
using PROManagementSystem.Application.Documents.Queries.GetDocumentById;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Common.Interfaces;

public interface IDocumentReadRepository
{
    Task<GetDocumentByIdResponseDto?> GetByIdAsync(
        Guid documentId,
        bool includeDeleted,
        CancellationToken cancellationToken);
    Task<GetDocumentByIdResponseDto?> GetByIdForCompanyAsync(
        Guid clientCompanyId,
        Guid documentId,
        CancellationToken cancellationToken);
    Task<GetClientDocumentsResponseDto> GetByCompanyIdAsync(
        Guid clientCompanyId,
        DocumentType? type,
        DocumentStatus? status,
        DateTime? expiresFrom,
        DateTime? expiresTo,
        DocumentOwnerType? ownerType,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}