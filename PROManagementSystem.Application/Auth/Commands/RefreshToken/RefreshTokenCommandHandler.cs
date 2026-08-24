using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Exceptions;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Entities;

namespace PROManagementSystem.Application.Auth.Commands.RefreshToken;

public sealed class RefreshTokenCommandHandler
    : IRequestHandler<RefreshTokenCommand, RefreshTokenResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ITokenService _tokenService;

    public RefreshTokenCommandHandler(
        IApplicationDbContext context,
        ITokenService tokenService)
    {
        _context = context;
        _tokenService = tokenService;
    }

    public async Task<RefreshTokenResponseDto> Handle(
        RefreshTokenCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Find the user by refresh token
        var user = await _context.Users
            .FirstOrDefaultAsync(
                x => x.RefreshToken == request.RefreshToken,
                cancellationToken);

        if (user is null)
        {
            throw new UnauthorizedAccessException(
                "Invalid refresh token.");
        }

        // 2. Validate the refresh token
        var isValid = _tokenService.ValidateRefreshToken(
            user,
            request.RefreshToken);

        if (!isValid)
        {
            throw new UnauthorizedAccessException(
                "Invalid or expired refresh token.");
        }

        // 3. Make sure the account is still active
        if (!user.IsActive)
        {
            throw new ForbiddenAccessException(
                "Your account is inactive.");
        }

        // 4. Generate a new access token
        var accessToken = _tokenService.GenerateAccessToken(user);

        // 5. Generate a new refresh token
        var newRefreshToken =
            _tokenService.GenerateRefreshToken();

        // 6. Rotate the refresh token
        user.RefreshToken = newRefreshToken;

        user.RefreshTokenExpiry =
            DateTime.UtcNow.AddDays(7);

        user.UpdatedAt = DateTime.UtcNow;

        // 7. Save the new refresh token
        await _context.SaveChangesAsync(
            cancellationToken);

        // 8. Return the new token pair
        return new RefreshTokenResponseDto(
            accessToken,
            newRefreshToken,
            60);
    }
}