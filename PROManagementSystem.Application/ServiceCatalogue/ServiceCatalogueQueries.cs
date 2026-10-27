using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Exceptions;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.ServiceCatalogue;

// ---------------------------------------------------------------- list (staff)

public sealed record GetServiceDefinitionsQuery(
    ServiceCategory? Category,
    string? Search,
    bool IncludeInactive) : IRequest<IReadOnlyList<ServiceDefinitionListItemDto>>;

public sealed class GetServiceDefinitionsQueryHandler
    : IRequestHandler<GetServiceDefinitionsQuery, IReadOnlyList<ServiceDefinitionListItemDto>>
{
    private readonly IApplicationDbContext _context;

    public GetServiceDefinitionsQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<IReadOnlyList<ServiceDefinitionListItemDto>> Handle(
        GetServiceDefinitionsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.ServiceDefinitions.AsNoTracking();

        if (!request.IncludeInactive)
        {
            query = query.Where(x => x.IsActive);
        }

        if (request.Category.HasValue)
        {
            query = query.Where(x => x.Category == request.Category.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(x =>
                x.Name.ToLower().Contains(term) ||
                x.Code.ToLower().Contains(term) ||
                (x.NameArabic != null && x.NameArabic.ToLower().Contains(term)) ||
                (x.Authority != null && x.Authority.ToLower().Contains(term)));
        }

        return await query
            .OrderBy(x => x.Category).ThenBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new ServiceDefinitionListItemDto(
                x.Id, x.Code, x.Name, x.NameArabic, x.Category, x.Authority, x.SubjectType,
                x.DefaultServiceFee, x.EstimatedGovernmentFee, x.ServiceFeeVatable,
                x.EstimatedDays, x.IsCounterService, x.RenewsDocumentType, x.IsActive, x.SortOrder,
                x.Steps.Count(s => !s.IsDeleted),
                x.RequiredDocuments.Count(d => !d.IsDeleted)))
            .ToListAsync(cancellationToken);
    }
}

// ---------------------------------------------------------------- detail

public sealed record GetServiceDefinitionByIdQuery(Guid Id) : IRequest<ServiceDefinitionDto>;

public sealed class GetServiceDefinitionByIdQueryHandler
    : IRequestHandler<GetServiceDefinitionByIdQuery, ServiceDefinitionDto>
{
    private readonly IApplicationDbContext _context;

    public GetServiceDefinitionByIdQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<ServiceDefinitionDto> Handle(
        GetServiceDefinitionByIdQuery request, CancellationToken cancellationToken)
    {
        var service = await _context.ServiceDefinitions
            .AsNoTracking()
            .Include(x => x.Steps)
            .Include(x => x.RequiredDocuments)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Service was not found.");

        return ServiceCatalogueMapping.ToDto(service);
    }
}

// ---------------------------------------------------------------- public catalogue (all roles)

public sealed record GetPublicServiceCatalogueQuery(ServiceCategory? Category)
    : IRequest<IReadOnlyList<PublicServiceDto>>;

public sealed class GetPublicServiceCatalogueQueryHandler
    : IRequestHandler<GetPublicServiceCatalogueQuery, IReadOnlyList<PublicServiceDto>>
{
    private readonly IApplicationDbContext _context;

    public GetPublicServiceCatalogueQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<IReadOnlyList<PublicServiceDto>> Handle(
        GetPublicServiceCatalogueQuery request, CancellationToken cancellationToken)
    {
        var query = _context.ServiceDefinitions.AsNoTracking().Where(x => x.IsActive);

        if (request.Category.HasValue)
        {
            query = query.Where(x => x.Category == request.Category.Value);
        }

        var services = await query
            .Include(x => x.RequiredDocuments)
            .OrderBy(x => x.Category).ThenBy(x => x.SortOrder).ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return services.Select(x => new PublicServiceDto(
                x.Id, x.Code, x.Name, x.NameArabic, x.Category, x.Description, x.SubjectType,
                x.EstimatedDays,
                x.RequiredDocuments.OrderBy(d => d.Order).Select(d => d.Name).ToList()))
            .ToList();
    }
}

// ---------------------------------------------------------------- contract pricing

public sealed record GetContractServicePricesQuery(Guid ServiceContractId)
    : IRequest<IReadOnlyList<ContractServicePriceDto>>;

public sealed class GetContractServicePricesQueryHandler
    : IRequestHandler<GetContractServicePricesQuery, IReadOnlyList<ContractServicePriceDto>>
{
    private readonly IApplicationDbContext _context;

    public GetContractServicePricesQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<IReadOnlyList<ContractServicePriceDto>> Handle(
        GetContractServicePricesQuery request, CancellationToken cancellationToken)
    {
        var contractExists = await _context.ServiceContracts
            .AnyAsync(x => x.Id == request.ServiceContractId, cancellationToken);

        if (!contractExists)
        {
            throw new NotFoundException("Service contract was not found.");
        }

        return await _context.ContractServicePrices
            .AsNoTracking()
            .Where(x => x.ServiceContractId == request.ServiceContractId)
            .OrderBy(x => x.ServiceDefinition!.Category).ThenBy(x => x.ServiceDefinition!.Name)
            .Select(x => new ContractServicePriceDto(
                x.Id, x.ServiceContractId, x.ServiceDefinitionId,
                x.ServiceDefinition!.Code, x.ServiceDefinition.Name, x.ServiceDefinition.Category,
                x.ServiceDefinition.DefaultServiceFee, x.ServiceFee, x.IncludedInRetainer))
            .ToListAsync(cancellationToken);
    }
}

internal static class ServiceCatalogueMapping
{
    public static ServiceDefinitionDto ToDto(ServiceDefinition x) => new(
        x.Id, x.Code, x.Name, x.NameArabic, x.Category, x.Authority, x.Description, x.SubjectType,
        x.DefaultServiceFee, x.EstimatedGovernmentFee, x.ServiceFeeVatable, x.EstimatedDays,
        x.IsCounterService, x.RenewsDocumentType, x.IsActive, x.SortOrder,
        x.Steps.Where(s => !s.IsDeleted).OrderBy(s => s.Order)
            .Select(s => new ServiceStepTemplateDto(s.Id, s.Order, s.Name, s.Description)).ToList(),
        x.RequiredDocuments.Where(d => !d.IsDeleted).OrderBy(d => d.Order)
            .Select(d => new ServiceRequiredDocumentDto(d.Id, d.Order, d.Name, d.IsMandatory)).ToList());
}
