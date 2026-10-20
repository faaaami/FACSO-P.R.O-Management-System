using PROManagementSystem.Domain.Enums;
using PROMangementSystem.Domain.Common;

namespace PROManagementSystem.Domain.Entities;

/// <summary>
/// One service the agency sells (e.g. "New employment visa", "Emirates ID
/// renewal", "Amer typing"). Defines default pricing, the documents to collect
/// and the steps a job for this service goes through.
/// </summary>
public sealed class ServiceDefinition : BaseEntity
{
    /// <summary>Short unique code, e.g. VISA-EMP-NEW.</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? NameArabic { get; set; }

    public ServiceCategory Category { get; set; }

    /// <summary>Authority the work is filed with (ICP, GDRFA, MOHRE, DED, ...).</summary>
    public string? Authority { get; set; }

    public string? Description { get; set; }

    public ServiceSubjectType SubjectType { get; set; } = ServiceSubjectType.Person;

    /// <summary>The agency's own fee, before VAT.</summary>
    public decimal DefaultServiceFee { get; set; }

    /// <summary>Typical government fees, for quoting only. Actual fees are recorded per job.</summary>
    public decimal EstimatedGovernmentFee { get; set; }

    /// <summary>Whether the service fee carries VAT (government fees are disbursements).</summary>
    public bool ServiceFeeVatable { get; set; } = true;

    public int? EstimatedDays { get; set; }

    /// <summary>Quick counter service (typing, printing) usually finished while the customer waits.</summary>
    public bool IsCounterService { get; set; }

    /// <summary>When set, completing a job for this service renews a document of this type.</summary>
    public DocumentType? RenewsDocumentType { get; set; }

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }

    public ICollection<ServiceStepTemplate> Steps { get; set; } = new List<ServiceStepTemplate>();

    public ICollection<ServiceRequiredDocument> RequiredDocuments { get; set; } = new List<ServiceRequiredDocument>();
}
