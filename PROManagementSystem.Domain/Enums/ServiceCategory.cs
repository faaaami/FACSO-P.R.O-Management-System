namespace PROManagementSystem.Domain.Enums;

public enum ServiceCategory
{
    /// <summary>Residence, entry permits, visit visas, status change, cancellation (ICP / GDRFA).</summary>
    Visa = 1,

    EmiratesId = 2,

    /// <summary>Work permits, labour contracts, MOHRE services.</summary>
    Labour = 3,

    /// <summary>Trade licence, establishment card, Ejari, chamber (DED / free zones).</summary>
    CompanyLicensing = 4,

    /// <summary>Counter typing of applications (Amer, Tasheel, ICP, typing centre).</summary>
    Typing = 5,

    /// <summary>Attestation and legal translation.</summary>
    Attestation = 6,

    /// <summary>Medical fitness tests and health insurance.</summary>
    MedicalInsurance = 7,

    Other = 99
}
