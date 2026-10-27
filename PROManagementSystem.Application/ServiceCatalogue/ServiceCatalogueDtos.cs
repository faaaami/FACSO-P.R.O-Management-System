using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.ServiceCatalogue;

public sealed record ServiceStepTemplateDto(Guid? Id, int Order, string Name, string? Description);

public sealed record ServiceRequiredDocumentDto(Guid? Id, int Order, string Name, bool IsMandatory);

public sealed record ServiceDefinitionListItemDto(
    Guid Id,
    string Code,
    string Name,
    string? NameArabic,
    ServiceCategory Category,
    string? Authority,
    ServiceSubjectType SubjectType,
    decimal DefaultServiceFee,
    decimal EstimatedGovernmentFee,
    bool ServiceFeeVatable,
    int? EstimatedDays,
    bool IsCounterService,
    DocumentType? RenewsDocumentType,
    bool IsActive,
    int SortOrder,
    int StepCount,
    int RequiredDocumentCount);

public sealed record ServiceDefinitionDto(
    Guid Id,
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
    bool IsActive,
    int SortOrder,
    IReadOnlyList<ServiceStepTemplateDto> Steps,
    IReadOnlyList<ServiceRequiredDocumentDto> RequiredDocuments);

/// <summary>What clients see of the catalogue: no internal pricing.</summary>
public sealed record PublicServiceDto(
    Guid Id,
    string Code,
    string Name,
    string? NameArabic,
    ServiceCategory Category,
    string? Description,
    ServiceSubjectType SubjectType,
    int? EstimatedDays,
    IReadOnlyList<string> RequiredDocuments);

public sealed record ContractServicePriceDto(
    Guid Id,
    Guid ServiceContractId,
    Guid ServiceDefinitionId,
    string ServiceCode,
    string ServiceName,
    ServiceCategory Category,
    decimal DefaultServiceFee,
    decimal ServiceFee,
    bool IncludedInRetainer);
