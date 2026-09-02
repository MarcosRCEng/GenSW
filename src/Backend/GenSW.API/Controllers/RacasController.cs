using GenSW.API.Contracts.Breeds;
using GenSW.Application.Breeds;
using GenSW.Application.Species;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GenSW.API.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/racas")]
public sealed class RacasController(IRacaService racas) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(RacaResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RacaResponse>> Create(CreateRacaRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await racas.CreateAsync(new CreateRacaCommand(request.EspecieId, request.Nome), cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, ToResponse(result));
        }
        catch (RacaDuplicateException exception) { return Conflict(ToDuplicateProblem(exception)); }
        catch (EspecieNotFoundException) { return NotFound(); }
        catch (ArgumentException exception) { return BadRequest(ToInvalidDataProblem(exception)); }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RacaResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await racas.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(ToResponse(result));
    }

    [HttpGet]
    public async Task<ActionResult<RacasListResponse>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        [FromQuery] Guid? especieId = null,
        [FromQuery] bool? ativo = null,
        [FromQuery] string sortBy = "nome",
        [FromQuery] string sortDirection = "asc",
        CancellationToken cancellationToken = default)
    {
        if (!TryParseSort(sortBy, out var sort) || !TryParseDirection(sortDirection, out var descending))
        {
            return BadRequest(new ProblemDetails { Title = "Invalid sorting", Status = StatusCodes.Status400BadRequest });
        }

        try
        {
            var result = await racas.ListAsync(new RacaListQuery(page, pageSize, search, especieId, ativo, sort, descending), cancellationToken);
            return Ok(new RacasListResponse(result.Items.Select(ToResponse).ToArray(), result.Page, result.PageSize, result.TotalItems, result.TotalPages));
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid query", Detail = exception.Message, Status = StatusCodes.Status400BadRequest });
        }
    }

    [HttpPut("{id:guid}")]
    public Task<ActionResult<RacaResponse>> Update(Guid id, UpdateRacaRequest request, CancellationToken cancellationToken)
        => ExecuteMutation(() => racas.UpdateAsync(id, new UpdateRacaCommand(request.EspecieId, request.Nome), cancellationToken));

    [HttpPatch("{id:guid}/ativo")]
    public Task<ActionResult<RacaResponse>> SetActive(Guid id, UpdateRacaStatusRequest request, CancellationToken cancellationToken)
        => ExecuteMutation(() => racas.SetActiveAsync(id, request.Ativo, cancellationToken));

    private async Task<ActionResult<RacaResponse>> ExecuteMutation(Func<Task<RacaResult>> action)
    {
        try { return Ok(ToResponse(await action())); }
        catch (RacaNotFoundException) { return NotFound(); }
        catch (EspecieNotFoundException) { return NotFound(); }
        catch (RacaDuplicateException exception) { return Conflict(ToDuplicateProblem(exception)); }
        catch (ArgumentException exception) { return BadRequest(ToInvalidDataProblem(exception)); }
    }

    private static ProblemDetails ToDuplicateProblem(RacaDuplicateException exception) => new()
    {
        Title = "Breed already exists",
        Detail = exception.Message,
        Status = StatusCodes.Status409Conflict,
    };

    private static ProblemDetails ToInvalidDataProblem(ArgumentException exception) => new()
    {
        Title = "Invalid breed data",
        Detail = exception.Message,
        Status = StatusCodes.Status400BadRequest,
    };

    private static bool TryParseSort(string value, out RacaSortField sort) => value switch
    {
        "nome" => Assign(RacaSortField.Nome, out sort),
        "ativo" => Assign(RacaSortField.Ativo, out sort),
        "createdAtUtc" => Assign(RacaSortField.CreatedAtUtc, out sort),
        _ => Assign(default, out sort, false),
    };

    private static bool TryParseDirection(string value, out bool descending) => value switch
    {
        "asc" => Assign(false, out descending),
        "desc" => Assign(true, out descending),
        _ => Assign(false, out descending, false),
    };

    private static bool Assign<T>(T value, out T output, bool success = true)
    {
        output = value;
        return success;
    }

    private static RacaResponse ToResponse(RacaResult result) => new(
        result.Id,
        result.EspecieId,
        result.Nome,
        result.Ativo,
        result.CreatedAtUtc,
        result.UpdatedAtUtc,
        new EspecieResumoResponse(result.Especie.Id, result.Especie.NomeComum, result.Especie.Ativo));
}
