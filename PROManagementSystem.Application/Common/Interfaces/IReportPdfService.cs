using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Common.Interfaces;

public interface IReportPdfService
{
    byte[] GenerateClientServiceReport(
        ClientServiceReportDto report);
}
