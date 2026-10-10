using ScalingDemo.Api.Data;
using ScalingDemo.Api.Models;

namespace ScalingDemo.Api.Services;

public interface IBmiService
{
    Task<BmiResultDto> CreateAsync(BmiRequest request, CancellationToken ct);
    Task<BmiRecordDto?> GetByIdAsync(int id, CancellationToken ct);
    Task<PagedResult<BmiRecordDto>> GetPagedAsync(int page, int pageSize, CancellationToken ct);
    Task<BmiStatsDto> GetStatsAsync(CancellationToken ct);
    Task<bool> DeleteAsync(int id, CancellationToken ct);
}

public sealed class BmiService(IBmiRepository repository) : IBmiService
{
    private const int MaxPageSize = 50;

    public async Task<BmiResultDto> CreateAsync(BmiRequest request, CancellationToken ct)
    {
        // The columns hold one decimal place, so calculate from exactly what gets stored.
        var heightCm = Math.Round(request.HeightCm, 1, MidpointRounding.AwayFromZero);
        var weightKg = Math.Round(request.WeightKg, 1, MidpointRounding.AwayFromZero);
        var name = string.IsNullOrWhiteSpace(request.Name) ? null : request.Name.Trim();

        var calculation = BmiCalculator.Calculate(heightCm, weightKg);

        var record = await repository.CreateAsync(
            new NewBmiRecord(name, request.Age, heightCm, weightKg, calculation.Bmi, calculation.Category),
            ct);

        return new BmiResultDto(
            record,
            calculation.HealthyWeightMinKg,
            calculation.HealthyWeightMaxKg,
            BmiCalculator.AdviceFor(calculation.Category));
    }

    public Task<BmiRecordDto?> GetByIdAsync(int id, CancellationToken ct) =>
        repository.GetByIdAsync(id, ct);

    public Task<PagedResult<BmiRecordDto>> GetPagedAsync(int page, int pageSize, CancellationToken ct) =>
        repository.GetPagedAsync(Math.Max(page, 1), Math.Clamp(pageSize, 1, MaxPageSize), ct);

    public Task<BmiStatsDto> GetStatsAsync(CancellationToken ct) =>
        repository.GetStatsAsync(ct);

    public Task<bool> DeleteAsync(int id, CancellationToken ct) =>
        repository.DeleteAsync(id, ct);
}
