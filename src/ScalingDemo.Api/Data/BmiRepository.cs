using System.Data;
using Microsoft.Data.SqlClient;
using ScalingDemo.Api.Models;

namespace ScalingDemo.Api.Data;

public interface IBmiRepository
{
    Task<BmiRecordDto> CreateAsync(NewBmiRecord record, CancellationToken ct);
    Task<BmiRecordDto?> GetByIdAsync(int id, CancellationToken ct);
    Task<PagedResult<BmiRecordDto>> GetPagedAsync(int page, int pageSize, CancellationToken ct);
    Task<BmiStatsDto> GetStatsAsync(CancellationToken ct);
    Task<bool> DeleteAsync(int id, CancellationToken ct);
    Task<bool> IsReadyAsync(CancellationToken ct);
}

/// <summary>ADO.NET repository. Every operation is a parameterised stored-procedure call.</summary>
public sealed class BmiRepository(IDbConnectionFactory connectionFactory) : IBmiRepository
{
    public async Task<BmiRecordDto> CreateAsync(NewBmiRecord record, CancellationToken ct)
    {
        await using var connection = await connectionFactory.OpenAsync(ct);
        await using var command = CreateProcedureCommand(connection, "dbo.SP_Bmi_Create");

        command.Parameters.Add("@Name", SqlDbType.NVarChar, 100).Value = (object?)record.Name ?? DBNull.Value;
        command.Parameters.Add("@Age", SqlDbType.Int).Value = record.Age;
        AddDecimal(command, "@HeightCm", 5, 1, record.HeightCm);
        AddDecimal(command, "@WeightKg", 5, 1, record.WeightKg);
        AddDecimal(command, "@Bmi", 5, 2, record.Bmi);
        command.Parameters.Add("@Category", SqlDbType.NVarChar, 20).Value = record.Category;

        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            throw new InvalidOperationException("SP_Bmi_Create did not return the inserted row.");
        }

        return MapRecord(reader);
    }

    public async Task<BmiRecordDto?> GetByIdAsync(int id, CancellationToken ct)
    {
        await using var connection = await connectionFactory.OpenAsync(ct);
        await using var command = CreateProcedureCommand(connection, "dbo.SP_Bmi_GetById");

        command.Parameters.Add("@Id", SqlDbType.Int).Value = id;

        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapRecord(reader) : null;
    }

    public async Task<PagedResult<BmiRecordDto>> GetPagedAsync(int page, int pageSize, CancellationToken ct)
    {
        await using var connection = await connectionFactory.OpenAsync(ct);
        await using var command = CreateProcedureCommand(connection, "dbo.SP_Bmi_GetAll");

        command.Parameters.Add("@PageNumber", SqlDbType.Int).Value = page;
        command.Parameters.Add("@PageSize", SqlDbType.Int).Value = pageSize;

        await using var reader = await command.ExecuteReaderAsync(ct);

        // Result set 1: total count.
        await reader.ReadAsync(ct);
        var totalCount = reader.GetInt32(0);

        // Result set 2: the page itself.
        await reader.NextResultAsync(ct);
        var items = new List<BmiRecordDto>(pageSize);
        while (await reader.ReadAsync(ct))
        {
            items.Add(MapRecord(reader));
        }

        return new PagedResult<BmiRecordDto>(items, page, pageSize, totalCount);
    }

    public async Task<BmiStatsDto> GetStatsAsync(CancellationToken ct)
    {
        await using var connection = await connectionFactory.OpenAsync(ct);
        await using var command = CreateProcedureCommand(connection, "dbo.SP_Bmi_GetStats");

        await using var reader = await command.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);

        return new BmiStatsDto(
            Total: reader.GetInt32(reader.GetOrdinal("Total")),
            AverageBmi: Math.Round(reader.GetDecimal(reader.GetOrdinal("AverageBmi")), 2),
            Underweight: reader.GetInt32(reader.GetOrdinal("Underweight")),
            Normal: reader.GetInt32(reader.GetOrdinal("Normal")),
            Overweight: reader.GetInt32(reader.GetOrdinal("Overweight")),
            Obese: reader.GetInt32(reader.GetOrdinal("Obese")));
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct)
    {
        await using var connection = await connectionFactory.OpenAsync(ct);
        await using var command = CreateProcedureCommand(connection, "dbo.SP_Bmi_Delete");

        command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
        var rowsAffected = command.Parameters.Add("@RowsAffected", SqlDbType.Int);
        rowsAffected.Direction = ParameterDirection.Output;

        await command.ExecuteNonQueryAsync(ct);

        return (int)rowsAffected.Value > 0;
    }

    /// <summary>True once the schema created by the init scripts exists. Used by /health/ready.</summary>
    public async Task<bool> IsReadyAsync(CancellationToken ct)
    {
        await using var connection = await connectionFactory.OpenAsync(ct);
        await using var command = new SqlCommand(
            "SELECT CASE WHEN OBJECT_ID(N'dbo.SP_Bmi_GetStats', N'P') IS NULL THEN 0 ELSE 1 END",
            connection);

        return Convert.ToInt32(await command.ExecuteScalarAsync(ct)) == 1;
    }

    private static SqlCommand CreateProcedureCommand(SqlConnection connection, string procedureName) =>
        new(procedureName, connection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 30
        };

    private static void AddDecimal(SqlCommand command, string name, byte precision, byte scale, decimal value)
    {
        var parameter = command.Parameters.Add(name, SqlDbType.Decimal);
        parameter.Precision = precision;
        parameter.Scale = scale;
        parameter.Value = value;
    }

    private static BmiRecordDto MapRecord(SqlDataReader reader)
    {
        var nameOrdinal = reader.GetOrdinal("Name");

        return new BmiRecordDto(
            Id: reader.GetInt32(reader.GetOrdinal("Id")),
            Name: reader.IsDBNull(nameOrdinal) ? null : reader.GetString(nameOrdinal),
            Age: reader.GetInt32(reader.GetOrdinal("Age")),
            HeightCm: reader.GetDecimal(reader.GetOrdinal("HeightCm")),
            WeightKg: reader.GetDecimal(reader.GetOrdinal("WeightKg")),
            Bmi: reader.GetDecimal(reader.GetOrdinal("Bmi")),
            Category: reader.GetString(reader.GetOrdinal("Category")),
            // DATETIME2 comes back with Kind=Unspecified; mark it UTC so JSON carries the 'Z'.
            CreatedAtUtc: DateTime.SpecifyKind(reader.GetDateTime(reader.GetOrdinal("CreatedAtUtc")), DateTimeKind.Utc));
    }
}
