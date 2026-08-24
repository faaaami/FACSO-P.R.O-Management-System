namespace PROMangementSystem.Application.Common.Interfaces;

public interface IDbConnectionFactory
{
    Task<System.Data.Common.DbConnection> CreateConnectionAsync(
        CancellationToken cancellationToken = default);
}