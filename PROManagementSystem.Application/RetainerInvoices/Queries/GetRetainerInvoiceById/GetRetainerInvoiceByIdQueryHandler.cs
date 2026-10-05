using MediatR;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.RetainerInvoices.Queries.GetRetainerInvoiceById;

public sealed class GetRetainerInvoiceByIdQueryHandler
    : IRequestHandler<
        GetRetainerInvoiceByIdQuery,
        GetRetainerInvoiceByIdResponseDto>
{
    private readonly IRetainerInvoiceReadRepository _repository;

    public GetRetainerInvoiceByIdQueryHandler(
        IRetainerInvoiceReadRepository repository)
    {
        _repository = repository;
    }

    public async Task<GetRetainerInvoiceByIdResponseDto> Handle(
        GetRetainerInvoiceByIdQuery request,
        CancellationToken cancellationToken)
    {
        var invoice =
            await _repository.GetByIdAsync(
                request.Id,
                request.ClientCompanyId,
                cancellationToken);

        if (invoice is null)
        {
            throw new Common.Exceptions.NotFoundException(
                "Retainer invoice was not found.");
        }

        return invoice;
    }
}
