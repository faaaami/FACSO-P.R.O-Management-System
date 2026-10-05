using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PROManagementSystem.Api.Common;
using PROManagementSystem.Application.Users.Commands.ChangePassword;
using PROManagementSystem.Application.Users.Commands.UpdateMyProfile;
using PROManagementSystem.Application.Users.Queries.GetMyProfile;

namespace PROManagementSystem.Api.Controllers.Agent;

[ApiController]
[Route("api/v1/agent")]
[Authorize(Roles = "Agent")]
public sealed class AgentAccountController : ControllerBase
{
    private readonly ISender _sender;

    public AgentAccountController(ISender sender)
    {
        _sender = sender;
    }

    // ================================================================
    // GET /api/v1/agent/me
    // ================================================================

    [HttpGet("me")]
    [ProducesResponseType(typeof(ApiResponse<GetMyProfileResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetMe(
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GetMyProfileQuery(),
            cancellationToken);

        return Ok(
            ApiResponse<GetMyProfileResponseDto>
                .SuccessResponse(result));
    }

    // ================================================================
    // PATCH /api/v1/agent/me
    // ================================================================

    [HttpPatch("me")]
    [ProducesResponseType(typeof(ApiResponse<UpdateMyProfileResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateMe(
        [FromBody] UpdateMyProfileCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            command,
            cancellationToken);

        return Ok(
            ApiResponse<UpdateMyProfileResponseDto>
                .SuccessResponse(result));
    }

    // ================================================================
    // PATCH /api/v1/agent/me/change-password
    // ================================================================

    [HttpPatch("me/change-password")]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordCommand command,
        CancellationToken cancellationToken)
    {
        await _sender.Send(
            command,
            cancellationToken);

        return Ok(
            ApiResponse<string>.SuccessResponse(
                "Password changed successfully."));
    }
}
