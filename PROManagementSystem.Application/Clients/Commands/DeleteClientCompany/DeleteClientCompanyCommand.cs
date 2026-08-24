using MediatR;

namespace PROManagementSystem.Application.Clients.Commands.DeleteClientCompany;

public sealed record DeleteClientCompanyCommand(
    Guid Id
) : IRequest<DeleteClientCompanyResponseDto>;