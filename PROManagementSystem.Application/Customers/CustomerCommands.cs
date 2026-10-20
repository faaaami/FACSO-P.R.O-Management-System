using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Exceptions;
using PROManagementSystem.Application.Common.Helpers;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Common.Validation;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Customers;

// ---------------------------------------------------------------- walk-in registration

/// <summary>
/// Registers a walk-in customer at the counter in one step. A company gets
/// its customer record and one licence (entity); an individual additionally
/// gets a person record for themselves so documents and jobs can hang off it.
/// </summary>
public sealed record CreateWalkInCustomerCommand(
    CustomerType CustomerType,
    string Name,
    string? Mobile,
    string? Email,
    string? Emirate,
    string? Address,
    string? TaxRegistrationNumber,
    string? ContactPersonName,
    string? TradeLicenseNumber,
    string? Notes,
    // Individual only
    string? Nationality,
    string? PassportNumber,
    string? EmiratesIdNumber,
    DateTime? DateOfBirth,
    string? Gender) : IRequest<WalkInCustomerCreatedDto>;

public sealed class CreateWalkInCustomerCommandValidator : AbstractValidator<CreateWalkInCustomerCommand>
{
    public CreateWalkInCustomerCommandValidator()
    {
        RuleFor(x => x.CustomerType)
            .Must(x => x is CustomerType.WalkInCompany or CustomerType.WalkInIndividual)
            .WithMessage("Customer type must be WalkInCompany or WalkInIndividual.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Mobile).MaximumLength(30);
        RuleFor(x => x.Email).MaximumLength(320).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Emirate).MaximumLength(100);
        RuleFor(x => x.Address).MaximumLength(500);
        RuleFor(x => x.ContactPersonName).MaximumLength(200);
        RuleFor(x => x.TradeLicenseNumber).MaximumLength(100);
        RuleFor(x => x.Notes).MaximumLength(2000);
        RuleFor(x => x.TaxRegistrationNumber)
            .Matches("^[0-9]{15}$").WithMessage("TRN must be 15 digits.")
            .When(x => !string.IsNullOrWhiteSpace(x.TaxRegistrationNumber));
        RuleFor(x => x.Mobile).NotEmpty()
            .When(x => string.IsNullOrWhiteSpace(x.Email))
            .WithMessage("Enter a mobile number or an email so the customer can be reached.");
        RuleFor(x => x.Nationality).MaximumLength(100);
        RuleFor(x => x.PassportNumber).MaximumLength(50);
        RuleFor(x => x.EmiratesIdNumber)
            .Must(EmiratesIdNumber.IsValidFormat).WithMessage(EmiratesIdNumber.FormatMessage)
            .When(x => !string.IsNullOrWhiteSpace(x.EmiratesIdNumber));
        RuleFor(x => x.Gender).Must(g => g is "M" or "F").When(x => !string.IsNullOrWhiteSpace(x.Gender))
            .WithMessage("Gender must be M or F.");
        RuleFor(x => x.DateOfBirth).LessThan(DateTime.UtcNow.Date).When(x => x.DateOfBirth.HasValue);
    }
}

public sealed class CreateWalkInCustomerCommandHandler
    : IRequestHandler<CreateWalkInCustomerCommand, WalkInCustomerCreatedDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CreateWalkInCustomerCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<WalkInCustomerCreatedDto> Handle(
        CreateWalkInCustomerCommand request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var name = request.Name.Trim();
        var isIndividual = request.CustomerType == CustomerType.WalkInIndividual;

        var company = new ClientCompany
        {
            Id = Guid.NewGuid(),
            CompanyName = name,
            CustomerType = request.CustomerType,
            CustomerCode = await CustomerCodes.NextAsync(_context, cancellationToken),
            Phone = Text.Clean(request.Mobile),
            Email = Text.Clean(request.Email)?.ToLowerInvariant(),
            Emirate = Text.Clean(request.Emirate),
            Address = Text.Clean(request.Address),
            TradeLicenseNumber = isIndividual ? null : Text.Clean(request.TradeLicenseNumber),
            TaxRegistrationNumber = Text.Clean(request.TaxRegistrationNumber),
            ContactPersonName = Text.Clean(request.ContactPersonName),
            Notes = Text.Clean(request.Notes),
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        var entity = new ClientEntity
        {
            Id = Guid.NewGuid(),
            ClientCompanyId = company.Id,
            EntityName = isIndividual ? $"{name} (personal file)" : name,
            TradeLicenseNumber = company.TradeLicenseNumber,
            Emirate = company.Emirate,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        _context.ClientCompanies.Add(company);
        _context.ClientEntities.Add(entity);

        Employee? person = null;

        if (isIndividual)
        {
            person = new Employee
            {
                Id = Guid.NewGuid(),
                ClientEntityId = entity.Id,
                PersonType = PersonType.Individual,
                FullName = name,
                Nationality = Text.Clean(request.Nationality),
                PassportNumber = Text.Clean(request.PassportNumber)?.ToUpperInvariant(),
                EmiratesIdNumber = EmiratesIdNumber.Normalize(request.EmiratesIdNumber),
                DateOfBirth = request.DateOfBirth.HasValue
                    ? DateTime.SpecifyKind(request.DateOfBirth.Value.Date, DateTimeKind.Utc)
                    : null,
                Gender = Text.Clean(request.Gender),
                Mobile = company.Phone,
                Email = company.Email,
                HireDate = DateTime.SpecifyKind(now.Date, DateTimeKind.Utc),
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            };
            _context.Employees.Add(person);
        }

        _context.AddAudit(_currentUser.UserId, "WalkInCustomerCreated", "ClientCompany", company.Id,
            $"Walk-in {(isIndividual ? "individual" : "company")} customer '{name}' ({company.CustomerCode}) was registered.");

        await _context.SaveChangesAsync(cancellationToken);

        return new WalkInCustomerCreatedDto(company.Id, company.CustomerCode!, name, company.CustomerType,
            entity.Id, person?.Id);
    }
}

// ---------------------------------------------------------------- customer profile

public sealed record UpdateCustomerProfileCommand(
    Guid ClientCompanyId,
    CustomerType CustomerType,
    string? TaxRegistrationNumber,
    string? ContactPersonName,
    string? Notes) : IRequest<CustomerProfileDto>;

public sealed class UpdateCustomerProfileCommandValidator : AbstractValidator<UpdateCustomerProfileCommand>
{
    public UpdateCustomerProfileCommandValidator()
    {
        RuleFor(x => x.ClientCompanyId).NotEmpty();
        RuleFor(x => x.CustomerType).IsInEnum();
        RuleFor(x => x.TaxRegistrationNumber)
            .Matches("^[0-9]{15}$").WithMessage("TRN must be 15 digits.")
            .When(x => !string.IsNullOrWhiteSpace(x.TaxRegistrationNumber));
        RuleFor(x => x.ContactPersonName).MaximumLength(200);
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

public sealed class UpdateCustomerProfileCommandHandler
    : IRequestHandler<UpdateCustomerProfileCommand, CustomerProfileDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public UpdateCustomerProfileCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<CustomerProfileDto> Handle(UpdateCustomerProfileCommand request, CancellationToken cancellationToken)
    {
        var company = await _context.ClientCompanies
            .FirstOrDefaultAsync(x => x.Id == request.ClientCompanyId, cancellationToken)
            ?? throw new NotFoundException("Client company was not found.");

        company.CustomerType = request.CustomerType;
        company.TaxRegistrationNumber = Text.Clean(request.TaxRegistrationNumber);
        company.ContactPersonName = Text.Clean(request.ContactPersonName);
        company.Notes = Text.Clean(request.Notes);
        company.CustomerCode ??= await CustomerCodes.NextAsync(_context, cancellationToken);
        company.UpdatedAt = DateTime.UtcNow;

        _context.AddAudit(_currentUser.UserId, "CustomerProfileUpdated", "ClientCompany", company.Id,
            $"Customer profile of '{company.CompanyName}' was updated (type {company.CustomerType}).");

        await _context.SaveChangesAsync(cancellationToken);

        return new CustomerProfileDto(company.Id, company.CustomerCode, company.CompanyName, company.CustomerType,
            company.TaxRegistrationNumber, company.ContactPersonName, company.Notes);
    }
}

// ---------------------------------------------------------------- persons

public sealed record PersonInput(
    PersonType PersonType,
    string FullName,
    string? FullNameArabic,
    string? Gender,
    string? Nationality,
    DateTime? DateOfBirth,
    string? PassportNumber,
    string? EmiratesIdNumber,
    string? UidNumber,
    string? MohrePersonCode,
    string? Mobile,
    string? Email,
    string? JobTitle,
    DateTime? HireDate,
    Guid? SponsorId,
    string? RelationshipToSponsor);

public sealed class PersonInputValidator : AbstractValidator<PersonInput>
{
    public PersonInputValidator()
    {
        RuleFor(x => x.PersonType).IsInEnum();
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.FullNameArabic).MaximumLength(200);
        RuleFor(x => x.Gender).Must(g => g is "M" or "F").When(x => !string.IsNullOrWhiteSpace(x.Gender))
            .WithMessage("Gender must be M or F.");
        RuleFor(x => x.Nationality).MaximumLength(100);
        RuleFor(x => x.DateOfBirth).LessThan(DateTime.UtcNow.Date).When(x => x.DateOfBirth.HasValue);
        RuleFor(x => x.PassportNumber).MaximumLength(50);
        RuleFor(x => x.EmiratesIdNumber)
            .Must(EmiratesIdNumber.IsValidFormat).WithMessage(EmiratesIdNumber.FormatMessage)
            .When(x => !string.IsNullOrWhiteSpace(x.EmiratesIdNumber));
        RuleFor(x => x.UidNumber).MaximumLength(30);
        RuleFor(x => x.MohrePersonCode).MaximumLength(30);
        RuleFor(x => x.Mobile).MaximumLength(30);
        RuleFor(x => x.Email).MaximumLength(320).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.JobTitle).MaximumLength(150);
        RuleFor(x => x.HireDate).LessThanOrEqualTo(DateTime.UtcNow.Date.AddDays(1)).When(x => x.HireDate.HasValue)
            .WithMessage("Hire date cannot be in the future.");
        RuleFor(x => x.RelationshipToSponsor).MaximumLength(50);
        RuleFor(x => x.SponsorId).NotEmpty()
            .When(x => x.PersonType is PersonType.Dependent or PersonType.DomesticWorker)
            .WithMessage("Dependents and domestic workers need a sponsor.");
    }
}

public sealed record CreatePersonCommand(Guid ClientEntityId, PersonInput Input) : IRequest<PersonDto>;

public sealed class CreatePersonCommandValidator : AbstractValidator<CreatePersonCommand>
{
    public CreatePersonCommandValidator()
    {
        RuleFor(x => x.ClientEntityId).NotEmpty();
        RuleFor(x => x.Input).NotNull().SetValidator(new PersonInputValidator());
    }
}

public sealed class CreatePersonCommandHandler : IRequestHandler<CreatePersonCommand, PersonDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ISender _sender;

    public CreatePersonCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser, ISender sender)
    {
        _context = context;
        _currentUser = currentUser;
        _sender = sender;
    }

    public async Task<PersonDto> Handle(CreatePersonCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.ClientEntities
            .FirstOrDefaultAsync(x => x.Id == request.ClientEntityId, cancellationToken)
            ?? throw new NotFoundException("Client entity was not found.");

        await PersonWriter.EnsureSponsorAsync(_context, request.Input.SponsorId, entity.ClientCompanyId, null, cancellationToken);

        var now = DateTime.UtcNow;
        var person = new Employee { Id = Guid.NewGuid(), ClientEntityId = entity.Id, IsActive = true, CreatedAt = now };
        PersonWriter.Apply(person, request.Input, now);
        _context.Employees.Add(person);

        _context.AddAudit(_currentUser.UserId, "PersonCreated", "Employee", person.Id,
            $"{person.PersonType} '{person.FullName}' was registered.", person.Id);

        await _context.SaveChangesAsync(cancellationToken);
        return await _sender.Send(new GetPersonByIdQuery(person.Id), cancellationToken);
    }
}

public sealed record UpdatePersonCommand(Guid Id, PersonInput Input) : IRequest<PersonDto>;

public sealed class UpdatePersonCommandValidator : AbstractValidator<UpdatePersonCommand>
{
    public UpdatePersonCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Input).NotNull().SetValidator(new PersonInputValidator());
    }
}

public sealed class UpdatePersonCommandHandler : IRequestHandler<UpdatePersonCommand, PersonDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ISender _sender;

    public UpdatePersonCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser, ISender sender)
    {
        _context = context;
        _currentUser = currentUser;
        _sender = sender;
    }

    public async Task<PersonDto> Handle(UpdatePersonCommand request, CancellationToken cancellationToken)
    {
        var person = await _context.Employees
            .Include(x => x.ClientEntity)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Person was not found.");

        await PersonWriter.EnsureSponsorAsync(_context, request.Input.SponsorId,
            person.ClientEntity.ClientCompanyId, person.Id, cancellationToken);

        PersonWriter.Apply(person, request.Input, DateTime.UtcNow);

        _context.AddAudit(_currentUser.UserId, "PersonUpdated", "Employee", person.Id,
            $"{person.PersonType} '{person.FullName}' was updated.", person.Id);

        await _context.SaveChangesAsync(cancellationToken);
        return await _sender.Send(new GetPersonByIdQuery(person.Id), cancellationToken);
    }
}

internal static class PersonWriter
{
    public static async Task EnsureSponsorAsync(
        IApplicationDbContext context, Guid? sponsorId, Guid clientCompanyId, Guid? selfId,
        CancellationToken cancellationToken)
    {
        if (!sponsorId.HasValue)
        {
            return;
        }

        if (sponsorId == selfId)
        {
            throw new ConflictException("A person cannot sponsor themselves.");
        }

        var sponsorCompanyId = await context.Employees
            .Where(x => x.Id == sponsorId.Value)
            .Select(x => (Guid?)x.ClientEntity.ClientCompanyId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Sponsor was not found.");

        if (sponsorCompanyId != clientCompanyId)
        {
            throw new ConflictException("The sponsor must belong to the same customer.");
        }
    }

    public static void Apply(Employee person, PersonInput input, DateTime now)
    {
        person.PersonType = input.PersonType;
        person.FullName = input.FullName.Trim();
        person.FullNameArabic = Text.Clean(input.FullNameArabic);
        person.Gender = Text.Clean(input.Gender);
        person.Nationality = Text.Clean(input.Nationality);
        person.DateOfBirth = input.DateOfBirth.HasValue
            ? DateTime.SpecifyKind(input.DateOfBirth.Value.Date, DateTimeKind.Utc)
            : null;
        person.PassportNumber = Text.Clean(input.PassportNumber)?.ToUpperInvariant();
        person.EmiratesIdNumber = EmiratesIdNumber.Normalize(input.EmiratesIdNumber);
        person.UidNumber = Text.Clean(input.UidNumber);
        person.MohrePersonCode = Text.Clean(input.MohrePersonCode);
        person.Mobile = Text.Clean(input.Mobile);
        person.Email = Text.Clean(input.Email)?.ToLowerInvariant();
        person.JobTitle = Text.Clean(input.JobTitle);
        // HireDate is required by the schema. For people who are not employees
        // (dependents, individuals) it records when they were registered.
        var hireDate = input.HireDate ?? (person.HireDate == default ? now : person.HireDate);
        person.HireDate = DateTime.SpecifyKind(hireDate.Date, DateTimeKind.Utc);
        person.SponsorId = input.SponsorId;
        person.RelationshipToSponsor = input.SponsorId.HasValue ? Text.Clean(input.RelationshipToSponsor) : null;
        person.UpdatedAt = now;
    }
}

internal static class Text
{
    public static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
