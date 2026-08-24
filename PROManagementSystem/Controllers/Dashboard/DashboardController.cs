using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PROManagementSystem.Api.Common;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Api.Controllers.Dashboard;

[ApiController]
[Route("api/v1/dashboard")]
[Authorize]
public sealed class DashboardController : ControllerBase
{
    private readonly IDashboardReadRepository _readRepository;

    public DashboardController(
        IDashboardReadRepository readRepository)
    {
        _readRepository = readRepository;
    }

    [HttpGet("summary")]
    [ProducesResponseType(typeof(ApiResponse<DashboardSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetSummary(
        CancellationToken cancellationToken)
    {
        var result = await _readRepository.GetSummaryAsync(
            cancellationToken);

        return Ok(ApiResponse<DashboardSummaryDto>.SuccessResponse(result));
    }

    [HttpGet("staff-workload")]
    [ProducesResponseType(typeof(ApiResponse<DashboardStaffWorkloadDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetStaffWorkload(
        CancellationToken cancellationToken)
    {
        var result = await _readRepository.GetStaffWorkloadAsync(
            cancellationToken);

        return Ok(ApiResponse<DashboardStaffWorkloadDto>.SuccessResponse(result));
    }

    [HttpGet("activity-feed")]
    [ProducesResponseType(typeof(ApiResponse<DashboardActivityFeedResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetActivityFeed(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _readRepository.GetActivityFeedAsync(
            page, pageSize, cancellationToken);

        return Ok(ApiResponse<DashboardActivityFeedResponseDto>.SuccessResponse(result));
    }

    [HttpGet("revenue")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<DashboardRevenueDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetRevenue(
        [FromQuery] string period = "month",
        CancellationToken cancellationToken = default)
    {
        var result = await _readRepository.GetRevenueAsync(
            period, cancellationToken);

        return Ok(ApiResponse<DashboardRevenueDto>.SuccessResponse(result));
    }

    [HttpGet("expiry-alerts/documents")]
    [ProducesResponseType(typeof(ApiResponse<List<DashboardExpiryAlertDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetExpiringDocuments(
        [FromQuery] int days = 90,
        CancellationToken cancellationToken = default)
    {
        var result = await _readRepository.GetExpiringDocumentsAsync(
            days, cancellationToken);

        return Ok(ApiResponse<List<DashboardExpiryAlertDto>>.SuccessResponse(result));
    }

    [HttpGet("expiry-alerts/employees")]
    [ProducesResponseType(typeof(ApiResponse<List<DashboardExpiryAlertDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetExpiringEmployees(
        [FromQuery] int days = 90,
        CancellationToken cancellationToken = default)
    {
        var result = await _readRepository.GetExpiringEmployeesAsync(
            days, cancellationToken);

        return Ok(ApiResponse<List<DashboardExpiryAlertDto>>.SuccessResponse(result));
    }
}
