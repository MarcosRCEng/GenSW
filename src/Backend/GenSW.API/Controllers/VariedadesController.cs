using GenSW.API.Contracts.Varieties;
using GenSW.Application.Species;
using GenSW.Application.Varieties;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GenSW.API.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/variedades")]
public sealed class VariedadesController(IVariedadeService variedades) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<VariedadeResponse>> Create(CreateVariedadeRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await variedades.CreateAsync(new CreateVariedadeCommand(request.EspecieId, request.Nome), cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, ToResponse(result));
        }
        catch (VariedadeDuplicateException exception) { return Conflict(ToDuplicateProblem(exception)); }
        catch (EspecieNotFoundException) { return NotFound(); }
        catch (ArgumentException exception) { return BadRequest(ToInvalidDataProblem(exception)); }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<VariedadeResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await variedades.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(ToResponse(result));
    }

    [HttpGet]
    public async Task<ActionResult<VariedadesListResponse>> List(
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
            var result = await variedades.ListAsync(new VariedadeListQuery(page, pageSize, search, especieId, ativo, sort, descending), cancellationToken);
            return Ok(new VariedadesListResponse(result.Items.Select(ToResponse).ToArray(), result.Page, result.PageSize, result.TotalItems, result.TotalPages));
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid query", Detail = exception.Message, Status = StatusCodes.Status400BadRequest });
        }
    }

    [HttpPut("{id:guid}")]
    public Task<ActionResult<VariedadeResponse>> Update(Guid id, UpdateVariedadeRequest request, CancellationToken cancellationToken)
        => ExecuteMutation(() => variedades.UpdateAsync(id, new UpdateVariedadeCommand(request.EspecieId, request.Nome), cancellationToken));

    [HttpPatch("{id:guid}/ativo")]
    public Task<ActionResult<VariedadeResponse>> SetActive(Guid id, UpdateVariedadeStatusRequest request, CancellationToken cancellationToken)
        => ExecuteMutation(() => variedades.SetActiveAsync(id, request.Ativo, cancellationToken));

    private async Task<ActionResult<VariedadeResponse>> ExecuteMutation(Func<Task<VariedadeResult>> action)
    {
        try { return Ok(ToResponse(await action())); }
        catch (VariedadeNotFoundException) { return NotFound(); }
        catch (EspecieNotFoundException) { return NotFound(); }
        catch (VariedadeDuplicateException exception) { return Conflict(ToDuplicateProblem(exception)); }
        catch (ArgumentException exception) { return BadRequest(ToInvalidDataProblem(exception)); }
    }

    private static ProblemDetails ToDuplicateProblem(VariedadeDuplicateException exception) => new()
    {
        Title = "Variety already exists",
        Detail = exception.Message,
        Status = StatusCodes.Status409Conflict,
    };

    private static ProblemDetails ToInvalidDataProblem(ArgumentException exception) => new()
    {
        Title = "Invalid variety data",
        Detail = exception.Message,
        Status = StatusCodes.Status400BadRequest,
    };

    private static bool TryParseSort(string value, out VariedadeSortField sort) => value switch
    {
        "nome" => Assign(VariedadeSortField.Nome, out sort),
        "ativo" => Assign(VariedadeSortField.Ativo, out sort),
        "createdAtUtc" => Assign(VariedadeSortField.CreatedAtUtc, out sort),
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

    private static VariedadeResponse ToResponse(VariedadeResult result) => new(
        result.Id,
        result.EspecieId,
        result.Nome,
        result.Ativo,
        result.CreatedAtUtc,
        result.UpdatedAtUtc,
        new EspecieResumoResponse(result.Especie.Id, result.Especie.NomeComum, result.Especie.Ativo));
}
