using MediatR;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.ServiceFeeInvoices.Queries.GetServiceFeeInvoiceById;

public sealed class GetServiceFeeInvoiceByIdQueryHandler
    : IRequestHandler<
        GetServiceFeeInvoiceByIdQuery,
        GetServiceFeeInvoiceByIdResponseDto>
{
    private readonly IServiceFeeInvoiceReadRepository
        _repository;

    public GetServiceFeeInvoiceByIdQueryHandler(
        IServiceFeeInvoiceReadRepository repository)
    {
        _repository = repository;
    }

    public async Task<GetServiceFeeInvoiceByIdResponseDto> Handle(
        GetServiceFeeInvoiceByIdQuery request,
        CancellationToken cancellationToken)
    {
        var invoice =
            await _repository.GetByIdAsync(
                request.Id,
                cancellationToken);

        if (invoice is null)
        {
            throw new Common.Exceptions.NotFoundException(
                "Service fee invoice was not found.");
        }

        return invoice;
    }
}
