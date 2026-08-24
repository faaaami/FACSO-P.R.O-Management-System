namespace PROManagementSystem.Application.Auth.Commands.Login;

public sealed class LoginResponseDto
{
    public string AccessToken { get; set; } = string.Empty;

    public string RefreshToken { get; set; } = string.Empty;

    public int ExpiresIn { get; set; }

    public LoginUserDto User { get; set; } = new();
}

public sealed class LoginUserDto
{
    public Guid Id { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public Guid? ClientCompanyId { get; set; }
}