using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Exceptions;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Customers;

// ---------------------------------------------------------------- counter lookup

/// <summary>
/// Finds customers by name, customer code, phone, email, TRN or trade licence,
/// and by the passport / Emirates ID / mobile / name of a person under them.
/// </summary>
public sealed record LookupCustomersQuery(string Term, CustomerType? CustomerType, int Limit = 20)
    : IRequest<IReadOnlyList<CustomerLookupItemDto>>;

public sealed class LookupCustomersQueryHandler
    : IRequestHandler<LookupCustomersQuery, IReadOnlyList<CustomerLookupItemDto>>
{
    private readonly IApplicationDbContext _context;

    public LookupCustomersQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<IReadOnlyList<CustomerLookupItemDto>> Handle(
        LookupCustomersQuery request, CancellationToken cancellationToken)
    {
        var term = (request.Term ?? string.Empty).Trim();

        if (term.Length < 2)
        {
            return [];
        }

        var limit = Math.Clamp(request.Limit, 1, 50);
        var like = term.ToLower();
        var digits = new string(term.Where(char.IsAsciiDigit).ToArray());
        var digitsLike = digits.Length >= 4 ? digits : null;

        var companies = _context.ClientCompanies.AsNoTracking();

        if (request.CustomerType.HasValue)
        {
            companies = companies.Where(x => x.CustomerType == request.CustomerType.Value);
        }

        var personMatches = await _context.Employees.AsNoTracking()
            .Where(p =>
                p.FullName.ToLower().Contains(like) ||
                (p.PassportNumber != null && p.PassportNumber.ToLower().Contains(like)) ||
                (p.EmiratesIdNumber != null && digitsLike != null &&
                    p.EmiratesIdNumber.Replace("-", "").Contains(digitsLike)) ||
                (p.Mobile != null && digitsLike != null && p.Mobile.Replace(" ", "").Replace("-", "").Contains(digitsLike)))
            .Select(p => new
            {
                CompanyId = p.ClientEntity.ClientCompanyId,
                Person = new CustomerLookupPersonDto(p.Id, p.FullName, p.PersonType, p.PassportNumber,
                    p.EmiratesIdNumber, p.Mobile)
            })
            .Take(100)
            .ToListAsync(cancellationToken);

        var personCompanyIds = personMatches.Select(x => x.CompanyId).Distinct().ToList();

        var results = await companies
            .Where(c =>
                c.CompanyName.ToLower().Contains(like) ||
                (c.CustomerCode != null && c.CustomerCode.ToLower().Contains(like)) ||
                (c.Email != null && c.Email.ToLower().Contains(like)) ||
                (c.TradeLicenseNumber != null && c.TradeLicenseNumber.ToLower().Contains(like)) ||
                (c.TaxRegistrationNumber != null && c.TaxRegistrationNumber.ToLower().Contains(like)) ||
                (c.Phone != null && digitsLike != null && c.Phone.Replace(" ", "").Replace("-", "").Contains(digitsLike)) ||
                personCompanyIds.Contains(c.Id))
            .OrderBy(c => c.CompanyName)
            .Take(limit)
            .Select(c => new
            {
                c.Id, c.CustomerCode, c.CompanyName, c.CustomerType, c.Phone, c.Email,
                c.TradeLicenseNumber, c.IsActive
            })
            .ToListAsync(cancellationToken);

        return results.Select(c => new CustomerLookupItemDto(
                c.Id, c.CustomerCode, c.CompanyName, c.CustomerType, c.Phone, c.Email,
                c.TradeLicenseNumber, c.IsActive,
                personMatches.Where(p => p.CompanyId == c.Id).Select(p => p.Person).Take(5).ToList()))
            .ToList();
    }
}

// ---------------------------------------------------------------- customer profile

public sealed record GetCustomerProfileQuery(Guid ClientCompanyId) : IRequest<CustomerProfileDto>;

public sealed class GetCustomerProfileQueryHandler : IRequestHandler<GetCustomerProfileQuery, CustomerProfileDto>
{
    private readonly IApplicationDbContext _context;

    public GetCustomerProfileQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<CustomerProfileDto> Handle(GetCustomerProfileQuery request, CancellationToken cancellationToken)
    {
        return await _context.ClientCompanies.AsNoTracking()
            .Where(x => x.Id == request.ClientCompanyId)
            .Select(x => new CustomerProfileDto(x.Id, x.CustomerCode, x.CompanyName, x.CustomerType,
                x.TaxRegistrationNumber, x.ContactPersonName, x.Notes))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Client company was not found.");
    }
}

// ---------------------------------------------------------------- persons

public sealed record GetPersonByIdQuery(Guid Id) : IRequest<PersonDto>;

public sealed class GetPersonByIdQueryHandler : IRequestHandler<GetPersonByIdQuery, PersonDto>
{
    private readonly IApplicationDbContext _context;

    public GetPersonByIdQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<PersonDto> Handle(GetPersonByIdQuery request, CancellationToken cancellationToken)
    {
        return await PersonProjection.Project(_context, _context.Employees.Where(x => x.Id == request.Id))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Person was not found.");
    }
}

public sealed record GetCustomerPersonsQuery(Guid ClientCompanyId, PersonType? PersonType)
    : IRequest<IReadOnlyList<PersonDto>>;

public sealed class GetCustomerPersonsQueryHandler : IRequestHandler<GetCustomerPersonsQuery, IReadOnlyList<PersonDto>>
{
    private readonly IApplicationDbContext _context;

    public GetCustomerPersonsQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<IReadOnlyList<PersonDto>> Handle(GetCustomerPersonsQuery request, CancellationToken cancellationToken)
    {
        if (!await _context.ClientCompanies.AnyAsync(x => x.Id == request.ClientCompanyId, cancellationToken))
        {
            throw new NotFoundException("Client company was not found.");
        }

        var query = _context.Employees.Where(x => x.ClientEntity.ClientCompanyId == request.ClientCompanyId);

        if (request.PersonType.HasValue)
        {
            query = query.Where(x => x.PersonType == request.PersonType.Value);
        }

        return await PersonProjection.Project(_context, query.OrderBy(x => x.FullName)).ToListAsync(cancellationToken);
    }
}

internal static class PersonProjection
{
    public static IQueryable<PersonDto> Project(IApplicationDbContext context, IQueryable<Domain.Entities.Employee> query) =>
        query.AsNoTracking().Select(x => new PersonDto(
            x.Id, x.ClientEntityId, x.ClientEntity.ClientCompanyId, x.PersonType, x.FullName, x.FullNameArabic,
            x.Gender, x.Nationality, x.DateOfBirth, x.PassportNumber, x.EmiratesIdNumber, x.UidNumber,
            x.MohrePersonCode, x.Mobile, x.Email, x.JobTitle, x.SponsorId,
            x.Sponsor != null ? x.Sponsor.FullName : null,
            x.RelationshipToSponsor, x.IsActive,
            context.Employees.Count(d => d.SponsorId == x.Id)));
}
