using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PROManagementSystem.Api.Common;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Api.Controllers.Dashboard;

[ApiController]
[Route("api/v1/dashboard")]
[Authorize(Roles = "Admin")]
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

    [HttpGet("activity-feed")]
    [ProducesResponseType(typeof(ApiResponse<DashboardActivityFeedResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetActivityFeed(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
        {
            return BadRequest(
                ApiResponse<object>.FailureResponse(
                    "INVALID_PAGE",
                    "Page must be greater than or equal to 1."));
        }

        if (pageSize is < 1 or > 100)
        {
            return BadRequest(
                ApiResponse<object>.FailureResponse(
                    "INVALID_PAGE_SIZE",
                    "Page size must be between 1 and 100."));
        }

        var result = await _readRepository.GetActivityFeedAsync(
            page, pageSize, cancellationToken);

        return Ok(ApiResponse<DashboardActivityFeedResponseDto>.SuccessResponse(result));
    }

    [HttpGet("revenue")]
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
        if (days is < 1 or > 365)
        {
            return BadRequest(
                ApiResponse<object>.FailureResponse(
                    "INVALID_DAYS",
                    "Days must be between 1 and 365."));
        }

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
        if (days is < 1 or > 365)
        {
            return BadRequest(
                ApiResponse<object>.FailureResponse(
                    "INVALID_DAYS",
                    "Days must be between 1 and 365."));
        }

        var result = await _readRepository.GetExpiringEmployeesAsync(
            days, cancellationToken);

        return Ok(ApiResponse<List<DashboardExpiryAlertDto>>.SuccessResponse(result));
    }
}
