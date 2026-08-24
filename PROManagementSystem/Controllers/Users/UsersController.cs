using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PROManagementSystem.Api.Common;
using PROManagementSystem.Application.Users.Commands.ChangePassword;
using PROManagementSystem.Application.Users.Commands.UpdateMyProfile;
using PROManagementSystem.Application.Users.Queries.GetMyProfile;

namespace PROManagementSystem.Api.Controllers.Users;

[ApiController]
[Route("api/v1/users")]
[Authorize]
public sealed class UsersController : ControllerBase
{
    private readonly ISender _sender;

    public UsersController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMyProfile(
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GetMyProfileQuery(),
            cancellationToken);

        return Ok(
            ApiResponse<GetMyProfileResponseDto>.SuccessResponse(
                result));
    }

    [HttpPatch("me")]
    public async Task<IActionResult> UpdateMyProfile(
    [FromBody] UpdateMyProfileCommand command,
    CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            command,
            cancellationToken);

        return Ok(
            ApiResponse<UpdateMyProfileResponseDto>.SuccessResponse(
                result));
    }

    [HttpPost("change-password")]
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