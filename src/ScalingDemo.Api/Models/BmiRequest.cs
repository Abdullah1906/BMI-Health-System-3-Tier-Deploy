using System.ComponentModel.DataAnnotations;

namespace ScalingDemo.Api.Models;

public sealed class BmiRequest
{
    [StringLength(100)]
    public string? Name { get; init; }

    // BMI categories used here are the adult (WHO) ones, so 18+ only.
    [Range(18, 120)]
    public int Age { get; init; }

    [Range(50.0, 260.0)]
    public decimal HeightCm { get; init; }

    [Range(10.0, 500.0)]
    public decimal WeightKg { get; init; }
}
