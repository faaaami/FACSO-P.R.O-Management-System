using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.GovFeeDisbursements.Commands.UpdateGovFeeDisbursementStatus;

public sealed record UpdateGovFeeDisbursementStatusRequest(
    GovFeeDisbursementStatus Status
);
