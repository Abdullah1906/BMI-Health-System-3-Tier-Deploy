using Microsoft.Data.SqlClient;

namespace ScalingDemo.Api.Data;

public sealed class DatabaseNotConfiguredException()
    : Exception("The connection string 'ConnectionStrings:BmiDb' is not configured.");

public interface IDbConnectionFactory
{
    /// <summary>Null or empty when no database is configured (e.g. Step 1 of the lab runs the API alone).</summary>
    string? ConnectionString { get; }

    Task<SqlConnection> OpenAsync(CancellationToken ct = default);
}

public sealed class SqlConnectionFactory(IConfiguration configuration) : IDbConnectionFactory
{
    public string? ConnectionString { get; } = configuration.GetConnectionString("BmiDb");

    public async Task<SqlConnection> OpenAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            throw new DatabaseNotConfiguredException();
        }

        var connection = new SqlConnection(ConnectionString);
        try
        {
            await connection.OpenAsync(ct);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}
