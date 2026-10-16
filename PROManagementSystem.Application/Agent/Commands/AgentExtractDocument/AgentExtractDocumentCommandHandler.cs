using MediatR;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Documents.Commands.ExtractDocument;
using PROManagementSystem.Application.Documents.Common;

namespace PROManagementSystem.Application.Agent.Commands.AgentExtractDocument;

/// <summary>
/// Same as the Admin extraction, but the draft records the agent as its
/// creator: agents only see and confirm their own drafts.
/// </summary>
public sealed class AgentExtractDocumentCommandHandler
    : IRequestHandler<AgentExtractDocumentCommand, ExtractDocumentResponseDto>
{
    private readonly ExtractionDraftCreator _drafts;
    private readonly ICurrentUserService _currentUser;

    public AgentExtractDocumentCommandHandler(
        ExtractionDraftCreator drafts,
        ICurrentUserService currentUser)
    {
        _drafts = drafts;
        _currentUser = currentUser;
    }

    public Task<ExtractDocumentResponseDto> Handle(
        AgentExtractDocumentCommand request,
        CancellationToken cancellationToken)
        => _drafts.CreateAsync(
            new ExtractionUpload(
                request.FileStream,
                request.FileName,
                request.ContentType,
                request.FileSize,
                request.DocumentType),
            createdBy: _currentUser.UserId!.Value,
            cancellationToken);
}
