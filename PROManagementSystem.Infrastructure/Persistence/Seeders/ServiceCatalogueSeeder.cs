using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Infrastructure.Persistence.Seeders;

/// <summary>
/// Seeds a starter catalogue of common UAE PRO services (visa, Emirates ID,
/// MOHRE, company, typing, attestation, medical/insurance) the first time the
/// catalogue is empty. Everything is editable in Admin → Services.
///
/// Fees are deliberately 0: government fees change often and differ by
/// emirate, free zone and category, and service fees are the agency's own
/// decision. Set both before quoting.
/// </summary>
public static class ServiceCatalogueSeeder
{
    private sealed record SeedItem(
        string Code,
        string Name,
        ServiceCategory Category,
        string Authority,
        ServiceSubjectType Subject,
        int? Days,
        string Description,
        string[] Steps,
        string[] Documents,
        DocumentType? Renews = null,
        bool Counter = false,
        string? Arabic = null);

    private static readonly string[] PassportPhoto =
        ["Passport copy (valid at least 6 months)", "Passport-size photo (white background)"];

    private static readonly SeedItem[] Catalogue =
    [
        // ------------------------------------------------------------ Visa
        new("VISA-EMP-NEW", "New employment visa", ServiceCategory.Visa, "MOHRE / ICP / GDRFA", ServiceSubjectType.Person, 14,
            "Full new-hire residence process for a mainland employee: work permit, entry permit, medical, Emirates ID and residence.",
            ["Collect documents", "MOHRE offer letter / work permit application", "Work permit approved and fees paid",
             "Entry permit issued", "Change of status / entry stamp", "Medical fitness test", "Emirates ID biometrics",
             "Labour contract signed", "Residence visa issued"],
            [..PassportPhoto, "Attested educational certificate (skilled roles)", "Current visa / entry stamp page (if in UAE)",
             "Signed offer letter", "Company trade licence and establishment card"],
            Arabic: "تأشيرة عمل جديدة"),
        new("VISA-EMP-RENEW", "Employment visa renewal", ServiceCategory.Visa, "MOHRE / ICP / GDRFA", ServiceSubjectType.Person, 7,
            "Renewal of an employee's residence visa and work permit.",
            ["Collect documents", "Work permit / labour contract renewal", "Medical fitness test",
             "Residence renewal application", "Emirates ID biometrics (if required)", "Residence visa renewed"],
            [..PassportPhoto, "Current Emirates ID", "Health insurance certificate"],
            Renews: DocumentType.Visa, Arabic: "تجديد تأشيرة عمل"),
        new("VISA-CANCEL", "Residence visa cancellation", ServiceCategory.Visa, "MOHRE / ICP / GDRFA", ServiceSubjectType.Person, 3,
            "Cancels the work permit and residence of an employee leaving the company.",
            ["Collect signed cancellation form", "Work permit cancellation (MOHRE)", "Residence cancellation (ICP / GDRFA)",
             "Cancellation confirmation shared with employee"],
            ["Passport copy", "Emirates ID", "Signed cancellation form", "End-of-service settlement (if applicable)"],
            Arabic: "إلغاء الإقامة"),
        new("VISA-FAMILY-NEW", "Family (dependent) residence visa", ServiceCategory.Visa, "ICP / GDRFA", ServiceSubjectType.Person, 10,
            "Residence for a spouse, child or parent sponsored by a UAE resident.",
            ["Collect documents", "Entry permit application", "Change of status / entry stamp", "Medical fitness test (18+)",
             "Emirates ID biometrics", "Residence visa issued"],
            ["Sponsor passport, visa and Emirates ID", "Sponsor salary certificate or labour contract", "Tenancy contract (Ejari)",
             "Attested marriage certificate (spouse)", "Attested birth certificate (child)", ..PassportPhoto,
             "Health insurance certificate"],
            Arabic: "إقامة عائلية"),
        new("VISA-FAMILY-RENEW", "Family visa renewal", ServiceCategory.Visa, "ICP / GDRFA", ServiceSubjectType.Person, 7,
            "Renewal of a dependent's residence visa.",
            ["Collect documents", "Medical fitness test (18+)", "Residence renewal application", "Emirates ID biometrics", "Residence renewed"],
            ["Sponsor visa and Emirates ID", "Tenancy contract (Ejari)", ..PassportPhoto, "Health insurance certificate"],
            Renews: DocumentType.Visa),
        new("VISA-DOMESTIC", "Domestic worker visa", ServiceCategory.Visa, "MOHRE / ICP / GDRFA", ServiceSubjectType.Person, 14,
            "Residence for a domestic worker sponsored by an individual.",
            ["Collect documents", "Entry permit application", "Change of status / entry stamp", "Medical fitness test",
             "Emirates ID biometrics", "Domestic worker contract", "Residence visa issued"],
            ["Sponsor passport, visa and Emirates ID", "Tenancy contract (Ejari)", ..PassportPhoto, "Health insurance certificate"]),
        new("VISA-INVESTOR", "Partner / investor visa", ServiceCategory.Visa, "ICP / GDRFA", ServiceSubjectType.Person, 10,
            "Residence for a shareholder or partner of a UAE company.",
            ["Collect documents", "Entry permit application", "Change of status / entry stamp", "Medical fitness test",
             "Emirates ID biometrics", "Residence visa issued"],
            [..PassportPhoto, "Trade licence showing partner", "Memorandum of association", "Establishment card"]),
        new("VISA-GOLDEN", "Golden visa (long-term residence)", ServiceCategory.Visa, "ICP / GDRFA", ServiceSubjectType.Person, 30,
            "Long-term residence for eligible investors, professionals and talents.",
            ["Eligibility assessment", "Nomination / approval application", "Entry permit or status adjustment",
             "Medical fitness test", "Emirates ID biometrics", "Golden visa issued"],
            [..PassportPhoto, "Proof of eligibility (salary, property, investment or qualification)", "Health insurance certificate"],
            Arabic: "الإقامة الذهبية"),
        new("VISA-VISIT", "Visit / tourist visa", ServiceCategory.Visa, "ICP / GDRFA", ServiceSubjectType.Person, 3,
            "Short-term visit visa for a visitor outside the UAE.",
            ["Collect documents", "Visa application submitted", "Visa issued and shared"],
            [..PassportPhoto, "Return ticket / hotel booking (if required)"]),
        new("VISA-STATUS-CHANGE", "Change of status (inside UAE)", ServiceCategory.Visa, "ICP / GDRFA", ServiceSubjectType.Person, 2,
            "Adjusts status from visit / entry permit to residence without leaving the country.",
            ["Collect documents", "Status change application", "Status changed"],
            ["Passport copy", "Entry permit", "Current visit visa / entry stamp"]),
        new("VISA-EXTEND", "Visit visa extension", ServiceCategory.Visa, "ICP / GDRFA", ServiceSubjectType.Person, 2,
            "Extends an existing visit visa.",
            ["Extension application", "Extension approved"],
            ["Passport copy", "Current visit visa"]),
        new("VISA-FINES", "Fine settlement / overstay", ServiceCategory.Visa, "ICP / GDRFA", ServiceSubjectType.Person, 2,
            "Checks and settles immigration fines (overstay, late renewal).",
            ["Fine enquiry", "Fines paid", "Clearance confirmed"],
            ["Passport copy", "Visa / Emirates ID copy"]),

        // ------------------------------------------------------------ Emirates ID
        new("EID-NEW", "Emirates ID – new", ServiceCategory.EmiratesId, "ICP", ServiceSubjectType.Person, 7,
            "First Emirates ID for a new resident.",
            ["Application typed and paid", "Biometrics appointment", "Card issued and delivered"],
            [..PassportPhoto, "Entry permit / residence approval"]),
        new("EID-RENEW", "Emirates ID – renewal", ServiceCategory.EmiratesId, "ICP", ServiceSubjectType.Person, 7,
            "Renewal of an Emirates ID together with or after residence renewal.",
            ["Application typed and paid", "Biometrics (if required)", "Card issued and delivered"],
            [..PassportPhoto, "Current Emirates ID", "Valid residence visa"],
            Renews: DocumentType.EmiratesId, Arabic: "تجديد الهوية الإماراتية"),
        new("EID-REPLACE", "Emirates ID – replacement (lost / damaged)", ServiceCategory.EmiratesId, "ICP", ServiceSubjectType.Person, 5,
            "Replacement card for a lost or damaged Emirates ID.",
            ["Application typed and paid", "Card issued and delivered"],
            ["Passport copy", "Police report (if lost)"]),

        // ------------------------------------------------------------ MOHRE
        new("MOHRE-WP-NEW", "Work permit – new", ServiceCategory.Labour, "MOHRE", ServiceSubjectType.Person, 5,
            "New work permit, including for residents sponsored by family.",
            ["Offer letter typed and signed", "Work permit application", "Work permit approved"],
            [..PassportPhoto, "Visa / Emirates ID copy", "Attested educational certificate (skilled roles)"]),
        new("MOHRE-WP-RENEW", "Work permit / labour card renewal", ServiceCategory.Labour, "MOHRE", ServiceSubjectType.Person, 5,
            "Renews the MOHRE work permit (labour card).",
            ["Renewal application", "Fees paid", "Work permit renewed"],
            ["Passport copy", "Emirates ID copy", "Labour contract"],
            Renews: DocumentType.LaborCard),
        new("MOHRE-CONTRACT", "Labour contract – new / amendment", ServiceCategory.Labour, "MOHRE", ServiceSubjectType.Person, 3,
            "Issues or amends an employee's labour contract (salary, job title).",
            ["Contract typed", "Employee signature", "Contract approved"],
            ["Passport copy", "Emirates ID copy", "Agreed salary and job title"]),
        new("MOHRE-WP-CANCEL", "Work permit cancellation", ServiceCategory.Labour, "MOHRE", ServiceSubjectType.Person, 2,
            "Cancels a work permit only (residence handled separately).",
            ["Cancellation typed", "Employee signature", "Cancellation approved"],
            ["Passport copy", "Signed cancellation form"]),

        // ------------------------------------------------------------ Company
        new("CO-TL-NEW", "Company formation – new trade licence", ServiceCategory.CompanyLicensing, "DED / Free zone", ServiceSubjectType.Company, 21,
            "New company: trade name, initial approval, MOA, office lease and licence.",
            ["Activity and legal form agreed", "Trade name reservation", "Initial approval", "MOA drafted and signed",
             "Office lease / Ejari", "Licence fees paid", "Trade licence issued", "Establishment card"],
            ["Shareholder passport copies", "Shareholder photos", "Visa / Emirates ID (residents)", "Proposed trade names"]),
        new("CO-TL-RENEW", "Trade licence renewal", ServiceCategory.CompanyLicensing, "DED / Free zone", ServiceSubjectType.Company, 5,
            "Annual renewal of the company's trade licence.",
            ["Renewal application", "Ejari / lease confirmed", "Fees paid", "Licence renewed"],
            ["Current trade licence", "Valid Ejari / tenancy contract", "Owner / partner passport copies"],
            Renews: DocumentType.TradeLicense, Arabic: "تجديد الرخصة التجارية"),
        new("CO-TL-AMEND", "Trade licence amendment", ServiceCategory.CompanyLicensing, "DED / Free zone", ServiceSubjectType.Company, 7,
            "Change of activity, partners, manager, name or address.",
            ["Amendment application", "Approvals obtained", "Amended MOA / documents signed", "Amended licence issued"],
            ["Current trade licence", "MOA", "Passport copies of affected partners / manager"]),
        new("CO-EST-NEW", "Establishment card – new", ServiceCategory.CompanyLicensing, "ICP / GDRFA", ServiceSubjectType.Company, 5,
            "Immigration establishment card required to sponsor employees.",
            ["Application typed and paid", "Card issued"],
            ["Trade licence", "MOA", "Owner passport and Emirates ID"]),
        new("CO-EST-RENEW", "Establishment card renewal", ServiceCategory.CompanyLicensing, "ICP / GDRFA", ServiceSubjectType.Company, 3,
            "Renewal of the immigration establishment card.",
            ["Renewal application", "Fees paid", "Card renewed"],
            ["Renewed trade licence", "Current establishment card"],
            Renews: DocumentType.EstablishmentCard),
        new("CO-EJARI", "Ejari registration", ServiceCategory.CompanyLicensing, "RERA / Municipality", ServiceSubjectType.Company, 2,
            "Registers a tenancy contract.",
            ["Contract checked", "Ejari registered"],
            ["Signed tenancy contract", "Landlord title deed / passport", "Tenant trade licence or Emirates ID", "DEWA premise number"]),
        new("CO-CHAMBER", "Chamber of commerce registration / renewal", ServiceCategory.CompanyLicensing, "Chamber of Commerce", ServiceSubjectType.Company, 3,
            "Chamber membership and certificate.",
            ["Application submitted", "Fees paid", "Certificate issued"],
            ["Trade licence", "MOA"]),

        // ------------------------------------------------------------ Typing (counter)
        new("TYP-AMER", "Amer application typing", ServiceCategory.Typing, "GDRFA (Amer)", ServiceSubjectType.None, 0,
            "Typing and submission of an immigration application at the counter.",
            ["Application typed", "Fees paid and receipt printed"], ["Applicant documents as required by the application"], Counter: true),
        new("TYP-TASHEEL", "Tasheel / MOHRE typing", ServiceCategory.Typing, "MOHRE (Tasheel)", ServiceSubjectType.None, 0,
            "Typing of a MOHRE transaction at the counter.",
            ["Application typed", "Fees paid and receipt printed"], ["Applicant documents as required by the transaction"], Counter: true),
        new("TYP-ICP", "ICP smart services typing", ServiceCategory.Typing, "ICP", ServiceSubjectType.None, 0,
            "Typing of an ICP application at the counter.",
            ["Application typed", "Fees paid and receipt printed"], ["Applicant documents as required by the application"], Counter: true),
        new("TYP-GENERAL", "General typing and printing", ServiceCategory.Typing, "Typing centre", ServiceSubjectType.None, 0,
            "Letters, forms, printing and photocopying.",
            ["Typed / printed and handed over"], [], Counter: true),
        new("TYP-TRANSLATION", "Legal translation", ServiceCategory.Typing, "Typing centre", ServiceSubjectType.None, 2,
            "Certified Arabic / English translation of documents.",
            ["Document received", "Translation done", "Translation delivered"], ["Original document"], Counter: true),

        // ------------------------------------------------------------ Attestation
        new("ATT-MOFA", "MOFA attestation", ServiceCategory.Attestation, "MOFA", ServiceSubjectType.None, 5,
            "UAE Ministry of Foreign Affairs attestation of a document already attested abroad.",
            ["Original received", "Submitted to MOFA", "Attested document returned"], ["Original document (attested by embassy)"]),
        new("ATT-CERT", "Certificate attestation (full chain)", ServiceCategory.Attestation, "Home country / Embassy / MOFA", ServiceSubjectType.Person, 30,
            "Full attestation of an educational, marriage or birth certificate.",
            ["Original received", "Home-country attestation", "UAE embassy attestation", "MOFA attestation", "Returned to customer"],
            ["Original certificate", "Passport copy"]),

        // ------------------------------------------------------------ Medical & insurance
        new("MED-FITNESS", "Medical fitness test", ServiceCategory.MedicalInsurance, "DHA / Health authority", ServiceSubjectType.Person, 2,
            "Books and follows up the residence medical fitness test.",
            ["Application typed and paid", "Test done", "Result received"], [..PassportPhoto, "Entry permit / visa copy"]),
        new("INS-HEALTH", "Health insurance policy", ServiceCategory.MedicalInsurance, "Insurer", ServiceSubjectType.Person, 1,
            "Mandatory health insurance for residence.",
            ["Quote shared", "Policy issued"], ["Passport copy", "Visa / entry permit copy"]),
        new("INS-ILOE", "ILOE (unemployment) insurance", ServiceCategory.MedicalInsurance, "MOHRE / ILOE", ServiceSubjectType.Person, 1,
            "Involuntary loss of employment insurance subscription.",
            ["Subscription done", "Certificate shared"], ["Emirates ID copy"]),
    ];

    /// <returns>Number of services created (0 when the catalogue already had entries).</returns>
    public static int Seed(ApplicationDbContext context)
    {
        if (context.ServiceDefinitions.Any())
        {
            return 0;
        }

        var now = DateTime.UtcNow;
        var sort = 0;

        foreach (var item in Catalogue)
        {
            var service = new ServiceDefinition
            {
                Id = Guid.NewGuid(),
                Code = item.Code,
                Name = item.Name,
                NameArabic = item.Arabic,
                Category = item.Category,
                Authority = item.Authority,
                Description = item.Description,
                SubjectType = item.Subject,
                EstimatedDays = item.Days,
                IsCounterService = item.Counter,
                RenewsDocumentType = item.Renews,
                ServiceFeeVatable = true,
                IsActive = true,
                SortOrder = sort += 10,
                CreatedAt = now,
                UpdatedAt = now
            };

            var order = 1;
            foreach (var step in item.Steps)
            {
                service.Steps.Add(new ServiceStepTemplate
                {
                    Id = Guid.NewGuid(), ServiceDefinitionId = service.Id, Order = order++, Name = step,
                    CreatedAt = now, UpdatedAt = now
                });
            }

            order = 1;
            foreach (var doc in item.Documents)
            {
                service.RequiredDocuments.Add(new ServiceRequiredDocument
                {
                    Id = Guid.NewGuid(), ServiceDefinitionId = service.Id, Order = order++, Name = doc,
                    IsMandatory = !doc.Contains("(if", StringComparison.OrdinalIgnoreCase) &&
                                  !doc.Contains("(skilled", StringComparison.OrdinalIgnoreCase),
                    CreatedAt = now, UpdatedAt = now
                });
            }

            context.ServiceDefinitions.Add(service);
        }

        context.SaveChanges();
        return Catalogue.Length;
    }
}
