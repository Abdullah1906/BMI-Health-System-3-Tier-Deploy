namespace ScalingDemo.Api.Services;

public static class BmiCategories
{
    public const string Underweight = "Underweight";
    public const string Normal = "Normal";
    public const string Overweight = "Overweight";
    public const string Obese = "Obese";
}

public sealed record BmiCalculation(
    decimal Bmi,
    string Category,
    decimal HealthyWeightMinKg,
    decimal HealthyWeightMaxKg);

/// <summary>Pure BMI maths using the adult WHO cut-offs. No I/O, so it is trivial to unit test.</summary>
public static class BmiCalculator
{
    private const decimal HealthyBmiMin = 18.5m;
    private const decimal HealthyBmiMax = 24.9m;

    private static readonly Dictionary<string, string> AdviceByCategory = new()
    {
        [BmiCategories.Underweight] =
            "Your BMI is below the healthy range. A balanced, nutrient-rich diet can help; consider speaking with a healthcare professional.",
        [BmiCategories.Normal] =
            "Your BMI is within the healthy range. Keep up balanced eating and regular physical activity.",
        [BmiCategories.Overweight] =
            "Your BMI is above the healthy range. Small, steady changes to diet and activity can make a difference.",
        [BmiCategories.Obese] =
            "Your BMI is well above the healthy range. Consider talking to a healthcare professional about a plan that suits you."
    };

    public static BmiCalculation Calculate(decimal heightCm, decimal weightKg)
    {
        var heightM = heightCm / 100m;
        var heightSquared = heightM * heightM;

        var bmi = Math.Round(weightKg / heightSquared, 2, MidpointRounding.AwayFromZero);

        return new BmiCalculation(
            bmi,
            Classify(bmi),
            Math.Round(HealthyBmiMin * heightSquared, 1, MidpointRounding.AwayFromZero),
            Math.Round(HealthyBmiMax * heightSquared, 1, MidpointRounding.AwayFromZero));
    }

    public static string Classify(decimal bmi) => bmi switch
    {
        < 18.5m => BmiCategories.Underweight,
        < 25m => BmiCategories.Normal,
        < 30m => BmiCategories.Overweight,
        _ => BmiCategories.Obese
    };

    public static string AdviceFor(string category) =>
        AdviceByCategory.GetValueOrDefault(category, string.Empty);
}
