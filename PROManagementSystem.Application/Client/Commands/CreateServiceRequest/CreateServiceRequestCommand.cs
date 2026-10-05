using MediatR;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Client.Commands.CreateServiceRequest;

public sealed record CreateServiceRequestCommand(
    ServiceRequestType Type,
    Guid? EmployeeId,
    Guid? EntityId,
    string? Notes
) : IRequest<CreateServiceRequestResponseDto>;