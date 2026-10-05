using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PROManagementSystem.Api.Common;
using PROManagementSystem.Application.Common.Jobs;

namespace PROManagementSystem.Api.Controllers.Jobs;

[ApiController]
[Route("api/v1/admin")]
[Authorize(Roles = "Admin")]
public sealed class JobsController : ControllerBase
{
    private readonly IBackgroundJobClient _backgroundJobClient;

    public JobsController(
        IBackgroundJobClient backgroundJobClient)
    {
        _backgroundJobClient = backgroundJobClient;
    }

    [HttpPost("jobs/run-expiry-check")]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public IActionResult RunExpiryCheck()
    {
        _backgroundJobClient.Enqueue<DocumentExpiryJob>(
            job => job.ExecuteAsync(CancellationToken.None));

        return Ok(
            ApiResponse<string>.SuccessResponse(
                "Document expiry check triggered successfully."));
    }

    [HttpPost("jobs/run-extraction-expiry-cleanup")]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public IActionResult RunExtractionExpiryCleanup()
    {
        _backgroundJobClient.Enqueue<ExtractionExpiryCleanupJob>(
            job => job.ExecuteAsync(CancellationToken.None));

        return Ok(
            ApiResponse<string>.SuccessResponse(
                "Extraction expiry cleanup triggered successfully."));
    }

    /// <summary>
    /// Queues the bounded overdue recovery sweep. Admin-only by the
    /// class-level [Authorize(Roles = "Admin")], and it takes no parameters —
    /// the recovery window is the job's own constant, so there is no
    /// request-supplied document, company or window to abuse.
    /// </summary>
    [HttpPost("jobs/run-overdue-sweep")]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public IActionResult RunOverdueSweep()
    {
        _backgroundJobClient.Enqueue<DocumentOverdueSweepJob>(
            job => job.ExecuteAsync(CancellationToken.None));

        return Ok(
            ApiResponse<string>.SuccessResponse(
                "Document overdue sweep triggered successfully."));
    }
}
