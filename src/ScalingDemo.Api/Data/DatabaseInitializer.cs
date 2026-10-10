using System.Data;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace ScalingDemo.Api.Data;

/// <summary>
/// Creates the database and runs Database/*.sql in the background, retrying until SQL Server is reachable.
/// The API starts serving immediately, so /health stays green and Step 1 of the lab (no database) still works.
///
/// Several replicas start at the same time, so the work is serialised with a SQL Server application lock.
/// All scripts are idempotent, which makes running them again on every start harmless.
/// </summary>
public sealed partial class DatabaseInitializer(
    IDbConnectionFactory connectionFactory,
    ILogger<DatabaseInitializer> logger) : BackgroundService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connectionString = connectionFactory.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            logger.LogInformation("ConnectionStrings:BmiDb is not set; skipping database initialisation.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await InitializeAsync(connectionString, stoppingToken);
                logger.LogInformation("Database is initialised.");
                return;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Database initialisation failed; retrying in {Delay}s.", RetryDelay.TotalSeconds);
                await Task.Delay(RetryDelay, stoppingToken);
            }
        }
    }

    private static async Task InitializeAsync(string connectionString, CancellationToken ct)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        var databaseName = builder.InitialCatalog;
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException("The connection string must name a database (Database=...).");
        }

        // Start in master because the target database may not exist yet.
        // Pooling is off so closing the connection really ends the session and releases the app lock.
        builder.InitialCatalog = "master";
        builder.ConnectTimeout = 10;
        builder.Pooling = false;

        await using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync(ct);

        await AcquireInitLockAsync(connection, ct);
        await EnsureDatabaseAsync(connection, databaseName, ct);
        await connection.ChangeDatabaseAsync(databaseName, ct);
        await RunScriptsAsync(connection, ct);
    }

    private static async Task AcquireInitLockAsync(SqlConnection connection, CancellationToken ct)
    {
        await using var command = new SqlCommand(
            """
            DECLARE @result INT;
            EXEC @result = sp_getapplock
                @Resource = N'bmi-db-init', @LockMode = N'Exclusive',
                @LockOwner = N'Session', @LockTimeout = 120000;
            SELECT @result;
            """,
            connection)
        { CommandTimeout = 150 };

        // 0 = granted immediately, 1 = granted after waiting, negative = failed.
        if (Convert.ToInt32(await command.ExecuteScalarAsync(ct)) < 0)
        {
            throw new InvalidOperationException("Could not acquire the database initialisation lock.");
        }
    }

    private static async Task EnsureDatabaseAsync(SqlConnection connection, string databaseName, CancellationToken ct)
    {
        await using var command = new SqlCommand(
            """
            IF DB_ID(@name) IS NULL
            BEGIN
                DECLARE @sql NVARCHAR(400) = N'CREATE DATABASE ' + QUOTENAME(@name);
                EXEC (@sql);
            END
            """,
            connection)
        { CommandTimeout = 60 };

        command.Parameters.Add("@name", SqlDbType.NVarChar, 128).Value = databaseName;
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task RunScriptsAsync(SqlConnection connection, CancellationToken ct)
    {
        var scriptsDirectory = Path.Combine(AppContext.BaseDirectory, "Database");
        var scripts = Directory.EnumerateFiles(scriptsDirectory, "*.sql").Order(StringComparer.Ordinal).ToList();
        if (scripts.Count == 0)
        {
            throw new FileNotFoundException($"No .sql scripts found in '{scriptsDirectory}'.");
        }

        foreach (var script in scripts)
        {
            var text = await File.ReadAllTextAsync(script, ct);

            // SqlClient cannot run 'GO' (a client-tool keyword), so split on it and run each batch.
            foreach (var batch in GoSeparator().Split(text))
            {
                if (string.IsNullOrWhiteSpace(batch))
                {
                    continue;
                }

                await using var command = new SqlCommand(batch, connection) { CommandTimeout = 60 };
                await command.ExecuteNonQueryAsync(ct);
            }
        }
    }

    [GeneratedRegex(@"^[ \t]*GO[ \t]*\r?$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex GoSeparator();
}
