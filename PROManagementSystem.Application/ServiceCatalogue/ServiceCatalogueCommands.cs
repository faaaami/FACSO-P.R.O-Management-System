using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Exceptions;
using PROManagementSystem.Application.Common.Helpers;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.ServiceCatalogue;

/// <summary>Editable fields of a catalogue service (create and update share them).</summary>
public sealed record ServiceDefinitionInput(
    string Code,
    string Name,
    string? NameArabic,
    ServiceCategory Category,
    string? Authority,
    string? Description,
    ServiceSubjectType SubjectType,
    decimal DefaultServiceFee,
    decimal EstimatedGovernmentFee,
    bool ServiceFeeVatable,
    int? EstimatedDays,
    bool IsCounterService,
    DocumentType? RenewsDocumentType,
    int SortOrder,
    IReadOnlyList<ServiceStepTemplateDto>? Steps,
    IReadOnlyList<ServiceRequiredDocumentDto>? RequiredDocuments);

public sealed class ServiceDefinitionInputValidator : AbstractValidator<ServiceDefinitionInput>
{
    public ServiceDefinitionInputValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50)
            .Matches("^[A-Za-z0-9-]+$").WithMessage("Code may contain only letters, digits and dashes.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameArabic).MaximumLength(200);
        RuleFor(x => x.Authority).MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.SubjectType).IsInEnum();
        RuleFor(x => x.RenewsDocumentType).IsInEnum().When(x => x.RenewsDocumentType.HasValue);
        RuleFor(x => x.DefaultServiceFee).GreaterThanOrEqualTo(0).LessThan(10_000_000);
        RuleFor(x => x.EstimatedGovernmentFee).GreaterThanOrEqualTo(0).LessThan(10_000_000);
        RuleFor(x => x.EstimatedDays).InclusiveBetween(0, 365).When(x => x.EstimatedDays.HasValue);
        RuleFor(x => x.Steps).Must(x => x == null || x.Count <= 50).WithMessage("At most 50 steps.");
        RuleForEach(x => x.Steps).ChildRules(step =>
        {
            step.RuleFor(s => s.Name).NotEmpty().MaximumLength(200);
            step.RuleFor(s => s.Description).MaximumLength(1000);
        });
        RuleFor(x => x.RequiredDocuments).Must(x => x == null || x.Count <= 50).WithMessage("At most 50 documents.");
        RuleForEach(x => x.RequiredDocuments).ChildRules(doc =>
        {
            doc.RuleFor(d => d.Name).NotEmpty().MaximumLength(200);
        });
    }
}

// ---------------------------------------------------------------- create

public sealed record CreateServiceDefinitionCommand(ServiceDefinitionInput Input) : IRequest<ServiceDefinitionDto>;

public sealed class CreateServiceDefinitionCommandValidator : AbstractValidator<CreateServiceDefinitionCommand>
{
    public CreateServiceDefinitionCommandValidator()
    {
        RuleFor(x => x.Input).NotNull().SetValidator(new ServiceDefinitionInputValidator());
    }
}

public sealed class CreateServiceDefinitionCommandHandler
    : IRequestHandler<CreateServiceDefinitionCommand, ServiceDefinitionDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CreateServiceDefinitionCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<ServiceDefinitionDto> Handle(
        CreateServiceDefinitionCommand request, CancellationToken cancellationToken)
    {
        var code = request.Input.Code.Trim().ToUpperInvariant();

        if (await _context.ServiceDefinitions.AnyAsync(x => x.Code == code, cancellationToken))
        {
            throw new ConflictException($"A service with code '{code}' already exists.");
        }

        var now = DateTime.UtcNow;
        var service = new ServiceDefinition { Id = Guid.NewGuid(), CreatedAt = now };
        ServiceDefinitionWriter.Apply(_context, service, request.Input, code, now);
        _context.ServiceDefinitions.Add(service);

        _context.AddAudit(_currentUser.UserId, "ServiceDefinitionCreated", "ServiceDefinition",
            service.Id, $"Service '{service.Name}' ({service.Code}) was added to the catalogue.");

        await _context.SaveChangesAsync(cancellationToken);
        return ServiceCatalogueMapping.ToDto(service);
    }
}

// ---------------------------------------------------------------- update

public sealed record UpdateServiceDefinitionCommand(Guid Id, ServiceDefinitionInput Input) : IRequest<ServiceDefinitionDto>;

public sealed class UpdateServiceDefinitionCommandValidator : AbstractValidator<UpdateServiceDefinitionCommand>
{
    public UpdateServiceDefinitionCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Input).NotNull().SetValidator(new ServiceDefinitionInputValidator());
    }
}

public sealed class UpdateServiceDefinitionCommandHandler
    : IRequestHandler<UpdateServiceDefinitionCommand, ServiceDefinitionDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public UpdateServiceDefinitionCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<ServiceDefinitionDto> Handle(
        UpdateServiceDefinitionCommand request, CancellationToken cancellationToken)
    {
        var service = await _context.ServiceDefinitions
            .Include(x => x.Steps)
            .Include(x => x.RequiredDocuments)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Service was not found.");

        var code = request.Input.Code.Trim().ToUpperInvariant();

        if (code != service.Code &&
            await _context.ServiceDefinitions.AnyAsync(x => x.Code == code && x.Id != service.Id, cancellationToken))
        {
            throw new ConflictException($"A service with code '{code}' already exists.");
        }

        // Templates are replaced wholesale. Existing jobs keep the copies they
        // were created with, so changing a template never rewrites history.
        _context.ServiceStepTemplates.RemoveRange(service.Steps);
        _context.ServiceRequiredDocuments.RemoveRange(service.RequiredDocuments);
        service.Steps.Clear();
        service.RequiredDocuments.Clear();

        var now = DateTime.UtcNow;
        ServiceDefinitionWriter.Apply(_context, service, request.Input, code, now);

        _context.AddAudit(_currentUser.UserId, "ServiceDefinitionUpdated", "ServiceDefinition",
            service.Id, $"Service '{service.Name}' ({service.Code}) was updated.");

        await _context.SaveChangesAsync(cancellationToken);
        return ServiceCatalogueMapping.ToDto(service);
    }
}

// ---------------------------------------------------------------- activate / deactivate

public sealed record SetServiceDefinitionActiveCommand(Guid Id, bool IsActive) : IRequest<ServiceDefinitionDto>;

public sealed class SetServiceDefinitionActiveCommandHandler
    : IRequestHandler<SetServiceDefinitionActiveCommand, ServiceDefinitionDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public SetServiceDefinitionActiveCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<ServiceDefinitionDto> Handle(
        SetServiceDefinitionActiveCommand request, CancellationToken cancellationToken)
    {
        var service = await _context.ServiceDefinitions
            .Include(x => x.Steps)
            .Include(x => x.RequiredDocuments)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Service was not found.");

        if (service.IsActive != request.IsActive)
        {
            service.IsActive = request.IsActive;
            service.UpdatedAt = DateTime.UtcNow;

            _context.AddAudit(_currentUser.UserId,
                request.IsActive ? "ServiceDefinitionActivated" : "ServiceDefinitionDeactivated",
                "ServiceDefinition", service.Id,
                $"Service '{service.Name}' was {(request.IsActive ? "activated" : "deactivated")}.");

            await _context.SaveChangesAsync(cancellationToken);
        }

        return ServiceCatalogueMapping.ToDto(service);
    }
}

// ---------------------------------------------------------------- contract pricing

public sealed record UpsertContractServicePriceCommand(
    Guid ServiceContractId,
    Guid ServiceDefinitionId,
    decimal ServiceFee,
    bool IncludedInRetainer) : IRequest<ContractServicePriceDto>;

public sealed class UpsertContractServicePriceCommandValidator : AbstractValidator<UpsertContractServicePriceCommand>
{
    public UpsertContractServicePriceCommandValidator()
    {
        RuleFor(x => x.ServiceContractId).NotEmpty();
        RuleFor(x => x.ServiceDefinitionId).NotEmpty();
        RuleFor(x => x.ServiceFee).GreaterThanOrEqualTo(0).LessThan(10_000_000);
    }
}

public sealed class UpsertContractServicePriceCommandHandler
    : IRequestHandler<UpsertContractServicePriceCommand, ContractServicePriceDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public UpsertContractServicePriceCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<ContractServicePriceDto> Handle(
        UpsertContractServicePriceCommand request, CancellationToken cancellationToken)
    {
        var contract = await _context.ServiceContracts
            .FirstOrDefaultAsync(x => x.Id == request.ServiceContractId, cancellationToken)
            ?? throw new NotFoundException("Service contract was not found.");

        var service = await _context.ServiceDefinitions
            .FirstOrDefaultAsync(x => x.Id == request.ServiceDefinitionId, cancellationToken)
            ?? throw new NotFoundException("Service was not found.");

        var now = DateTime.UtcNow;
        var price = await _context.ContractServicePrices
            .FirstOrDefaultAsync(x => x.ServiceContractId == contract.Id &&
                                      x.ServiceDefinitionId == service.Id, cancellationToken);

        if (price is null)
        {
            price = new ContractServicePrice
            {
                Id = Guid.NewGuid(),
                ServiceContractId = contract.Id,
                ServiceDefinitionId = service.Id,
                CreatedAt = now
            };
            _context.ContractServicePrices.Add(price);
        }

        price.ServiceFee = request.IncludedInRetainer ? 0 : decimal.Round(request.ServiceFee, 2);
        price.IncludedInRetainer = request.IncludedInRetainer;
        price.UpdatedAt = now;

        _context.AddAudit(_currentUser.UserId, "ContractServicePriceSet", "ServiceContract", contract.Id,
            request.IncludedInRetainer
                ? $"'{service.Name}' is included in the retainer of contract {contract.ContractNumber}."
                : $"'{service.Name}' is priced at AED {price.ServiceFee:0.00} on contract {contract.ContractNumber}.");

        await _context.SaveChangesAsync(cancellationToken);

        return new ContractServicePriceDto(price.Id, contract.Id, service.Id, service.Code, service.Name,
            service.Category, service.DefaultServiceFee, price.ServiceFee, price.IncludedInRetainer);
    }
}

public sealed record DeleteContractServicePriceCommand(Guid ServiceContractId, Guid ServiceDefinitionId) : IRequest;

public sealed class DeleteContractServicePriceCommandHandler : IRequestHandler<DeleteContractServicePriceCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public DeleteContractServicePriceCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task Handle(DeleteContractServicePriceCommand request, CancellationToken cancellationToken)
    {
        var price = await _context.ContractServicePrices
            .FirstOrDefaultAsync(x => x.ServiceContractId == request.ServiceContractId &&
                                      x.ServiceDefinitionId == request.ServiceDefinitionId, cancellationToken)
            ?? throw new NotFoundException("No contract price exists for this service.");

        _context.ContractServicePrices.Remove(price);
        _context.AddAudit(_currentUser.UserId, "ContractServicePriceRemoved", "ServiceContract",
            request.ServiceContractId, "A contract service price was removed; the default fee applies again.");
        await _context.SaveChangesAsync(cancellationToken);
    }
}

internal static class ServiceDefinitionWriter
{
    public static void Apply(
        IApplicationDbContext context, ServiceDefinition service, ServiceDefinitionInput input, string code, DateTime now)
    {
        service.Code = code;
        service.Name = input.Name.Trim();
        service.NameArabic = Clean(input.NameArabic);
        service.Category = input.Category;
        service.Authority = Clean(input.Authority);
        service.Description = Clean(input.Description);
        service.SubjectType = input.SubjectType;
        service.DefaultServiceFee = decimal.Round(input.DefaultServiceFee, 2);
        service.EstimatedGovernmentFee = decimal.Round(input.EstimatedGovernmentFee, 2);
        service.ServiceFeeVatable = input.ServiceFeeVatable;
        service.EstimatedDays = input.EstimatedDays;
        service.IsCounterService = input.IsCounterService;
        service.RenewsDocumentType = input.RenewsDocumentType;
        service.SortOrder = input.SortOrder;
        service.UpdatedAt = now;

        var order = 1;
        foreach (var step in input.Steps ?? [])
        {
            // Added through the DbSet so EF inserts them even when the parent
            // is already tracked (a preset key would otherwise read as an update).
            context.ServiceStepTemplates.Add(new ServiceStepTemplate
            {
                Id = Guid.NewGuid(),
                ServiceDefinitionId = service.Id,
                Order = order++,
                Name = step.Name.Trim(),
                Description = Clean(step.Description),
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        order = 1;
        foreach (var doc in input.RequiredDocuments ?? [])
        {
            context.ServiceRequiredDocuments.Add(new ServiceRequiredDocument
            {
                Id = Guid.NewGuid(),
                ServiceDefinitionId = service.Id,
                Order = order++,
                Name = doc.Name.Trim(),
                IsMandatory = doc.IsMandatory,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
