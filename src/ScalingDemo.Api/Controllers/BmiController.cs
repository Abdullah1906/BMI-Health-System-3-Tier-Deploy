using Microsoft.AspNetCore.Mvc;
using ScalingDemo.Api.Models;
using ScalingDemo.Api.Services;

namespace ScalingDemo.Api.Controllers;

[ApiController]
[Route("api/bmi")]
public sealed class BmiController(IBmiService service) : ControllerBase
{
    /// <summary>Calculates BMI, stores the result and returns it with a healthy weight range.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(BmiResultDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<BmiResultDto>> Create(BmiRequest request, CancellationToken ct)
    {
        var result = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Record.Id }, result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<BmiRecordDto>> GetById(int id, CancellationToken ct)
    {
        var record = await service.GetByIdAsync(id, ct);
        return record is null ? NotFound() : Ok(record);
    }

    /// <summary>History, newest first.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<BmiRecordDto>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken ct = default) =>
        Ok(await service.GetPagedAsync(page, pageSize, ct));

    [HttpGet("stats")]
    public async Task<ActionResult<BmiStatsDto>> GetStats(CancellationToken ct) =>
        Ok(await service.GetStatsAsync(ct));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) =>
        await service.DeleteAsync(id, ct) ? NoContent() : NotFound();
}
