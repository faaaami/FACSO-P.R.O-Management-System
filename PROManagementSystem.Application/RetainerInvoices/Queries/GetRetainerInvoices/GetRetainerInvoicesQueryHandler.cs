using MediatR;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.RetainerInvoices.Queries.GetRetainerInvoices;

public sealed class GetRetainerInvoicesQueryHandler
    : IRequestHandler<
        GetRetainerInvoicesQuery,
        GetRetainerInvoicesResponseDto>
{
    private readonly IRetainerInvoiceReadRepository _repository;

    public GetRetainerInvoicesQueryHandler(
        IRetainerInvoiceReadRepository repository)
    {
        _repository = repository;
    }

    public async Task<GetRetainerInvoicesResponseDto> Handle(
        GetRetainerInvoicesQuery request,
        CancellationToken cancellationToken)
    {
        return await _repository.GetPagedAsync(
            request.ClientCompanyId,
            request.ServiceContractId,
            request.Status,
            request.Page,
            request.PageSize,
            cancellationToken);
    }
}
