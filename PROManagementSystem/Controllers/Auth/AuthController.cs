using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PROManagementSystem.Api.Common;
using PROManagementSystem.Application.Auth.Commands.Login;
using PROManagementSystem.Application.Auth.Commands.Register;
using PROManagementSystem.Application.Auth.Commands.Logout;
using PROManagementSystem.Application.Auth.Commands.ForgotPassword;
using PROManagementSystem.Application.Auth.Commands.RefreshToken;
using PROManagementSystem.Application.Auth.Commands.VerifyEmail;
using PROManagementSystem.Application.Auth.Commands.ResetPassword;

namespace PROManagementSystem.Api.Controllers.Auth;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly ISender _sender;

    public AuthController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            command,
            cancellationToken);

        return Ok(
            ApiResponse<LoginResponseDto>.SuccessResponse(
                result));
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register(
        [FromBody] RegisterCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            command,
            cancellationToken);

        return Ok(
            ApiResponse<RegisterResponseDto>.SuccessResponse(
                result));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh(
    [FromBody] RefreshTokenCommand command,
    CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            command,
            cancellationToken);

        return Ok(
            ApiResponse<RefreshTokenResponseDto>.SuccessResponse(
                result));
    }


    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(
     [FromBody] LogoutCommand command,
     CancellationToken cancellationToken)
    {
        await _sender.Send(
            command,
            cancellationToken);

        return Ok(
            ApiResponse<object>.SuccessResponse(null));
    }

    [HttpPost("verify-email")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyEmail(
    [FromBody] VerifyEmailCommand command,
    CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            command,
            cancellationToken);

        return Ok(
            ApiResponse<VerifyEmailResponseDto>.SuccessResponse(
                result));
    }


    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword(
    [FromBody] ForgotPasswordCommand command,
    CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            command,
            cancellationToken);

        return Ok(
            ApiResponse<ForgotPasswordResponseDto>.SuccessResponse(
                result));
    }


    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword(
    [FromBody] ResetPasswordCommand command,
    CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            command,
            cancellationToken);

        return Ok(
            ApiResponse<ResetPasswordResponseDto>.SuccessResponse(
                result));
    }
}