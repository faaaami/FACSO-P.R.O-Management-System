using Dapper;
using Npgsql;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Users.Queries.GetMyProfile;

namespace PROManagementSystem.Infrastructure.Persistence.Repositories;

public sealed class UserReadRepository : IUserReadRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public UserReadRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<GetMyProfileResponseDto?> GetByIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
    SELECT
        id,
        full_name AS FullName,
        email,
        phone,
        role,
        is_active AS IsActive,
        email_verified AS EmailVerified,
        client_company_id AS ClientCompanyId
    FROM users
    WHERE id = @UserId
      AND is_deleted = false
    LIMIT 1;
    """;

        await using var connection =
            await _dataSource.OpenConnectionAsync(
                cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<GetMyProfileResponseDto>(
            new CommandDefinition(
                sql,
                new { UserId = userId },
                cancellationToken: cancellationToken));
    }
}