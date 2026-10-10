namespace ScalingDemo.Api.Models;

/// <summary>Values the service hands to the repository for insertion.</summary>
public sealed record NewBmiRecord(
    string? Name,
    int Age,
    decimal HeightCm,
    decimal WeightKg,
    decimal Bmi,
    string Category);

public sealed record BmiRecordDto(
    int Id,
    string? Name,
    int Age,
    decimal HeightCm,
    decimal WeightKg,
    decimal Bmi,
    string Category,
    DateTime CreatedAtUtc);

public sealed record BmiResultDto(
    BmiRecordDto Record,
    decimal HealthyWeightMinKg,
    decimal HealthyWeightMaxKg,
    string Advice);

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record BmiStatsDto(
    int Total,
    decimal AverageBmi,
    int Underweight,
    int Normal,
    int Overweight,
    int Obese);
