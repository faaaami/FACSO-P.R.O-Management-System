using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Customers;

public sealed record WalkInCustomerCreatedDto(
    Guid ClientCompanyId,
    string CustomerCode,
    string Name,
    CustomerType CustomerType,
    Guid ClientEntityId,
    Guid? PersonId);

public sealed record CustomerLookupPersonDto(
    Guid Id,
    string FullName,
    PersonType PersonType,
    string? PassportNumber,
    string? EmiratesIdNumber,
    string? Mobile);

public sealed record CustomerLookupItemDto(
    Guid ClientCompanyId,
    string? CustomerCode,
    string Name,
    CustomerType CustomerType,
    string? Phone,
    string? Email,
    string? TradeLicenseNumber,
    bool IsActive,
    IReadOnlyList<CustomerLookupPersonDto> MatchedPersons);

public sealed record CustomerProfileDto(
    Guid ClientCompanyId,
    string? CustomerCode,
    string Name,
    CustomerType CustomerType,
    string? TaxRegistrationNumber,
    string? ContactPersonName,
    string? Notes);

public sealed record PersonDto(
    Guid Id,
    Guid ClientEntityId,
    Guid ClientCompanyId,
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
    Guid? SponsorId,
    string? SponsorName,
    string? RelationshipToSponsor,
    bool IsActive,
    int DependentCount);
