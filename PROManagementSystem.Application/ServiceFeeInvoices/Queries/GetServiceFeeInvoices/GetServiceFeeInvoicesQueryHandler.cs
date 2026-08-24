using MediatR;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.ServiceFeeInvoices.Queries.GetServiceFeeInvoices;

public sealed class GetServiceFeeInvoicesQueryHandler
    : IRequestHandler<
        GetServiceFeeInvoicesQuery,
        GetServiceFeeInvoicesResponseDto>
{
    private readonly IServiceFeeInvoiceReadRepository
        _repository;

    public GetServiceFeeInvoicesQueryHandler(
        IServiceFeeInvoiceReadRepository repository)
    {
        _repository = repository;
    }

    public async Task<GetServiceFeeInvoicesResponseDto> Handle(
        GetServiceFeeInvoicesQuery request,
        CancellationToken cancellationToken)
    {
        return await _repository.GetPagedAsync(
            request.ClientCompanyId,
            request.RenewalTaskId,
            request.Status,
            request.Page,
            request.PageSize,
            cancellationToken);
    }
}
