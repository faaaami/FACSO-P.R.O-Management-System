using MediatR;

namespace PROManagementSystem.Application.Tasks.Queries.GetAgentServiceFeeInvoice;

public sealed record GetAgentServiceFeeInvoiceQuery(
    Guid TaskId
) : IRequest<GetAgentServiceFeeInvoiceResponseDto?>;
