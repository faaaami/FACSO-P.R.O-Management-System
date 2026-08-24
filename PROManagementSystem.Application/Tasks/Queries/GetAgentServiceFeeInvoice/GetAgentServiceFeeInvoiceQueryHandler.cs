using MediatR;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Tasks.Queries.GetAgentServiceFeeInvoice;

public sealed class GetAgentServiceFeeInvoiceQueryHandler
    : IRequestHandler<
        GetAgentServiceFeeInvoiceQuery,
        GetAgentServiceFeeInvoiceResponseDto?>
{
    private readonly IRenewalTaskReadRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public GetAgentServiceFeeInvoiceQueryHandler(
        IRenewalTaskReadRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<GetAgentServiceFeeInvoiceResponseDto?> Handle(
        GetAgentServiceFeeInvoiceQuery request,
        CancellationToken cancellationToken)
    {
        var agentId = _currentUser.UserId!.Value;

        return await _repository.GetServiceFeeInvoiceByTaskIdAsync(
            request.TaskId,
            agentId,
            cancellationToken);
    }
}
