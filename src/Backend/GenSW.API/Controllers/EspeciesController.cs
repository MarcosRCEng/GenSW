using GenSW.API.Contracts.Species;
using GenSW.Application.Species;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GenSW.API.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/especies")]
public sealed class EspeciesController(IEspecieService especies) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(EspecieResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<EspecieResponse>> Create(CreateEspecieRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await especies.CreateAsync(new CreateEspecieCommand(request.NomeComum, request.NomeCientifico), cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, ToResponse(result));
        }
        catch (EspecieDuplicateException exception) { return Conflict(ToDuplicateProblem(exception)); }
        catch (ArgumentException exception) { return BadRequest(ToInvalidDataProblem(exception)); }
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EspecieResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EspecieResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await especies.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(ToResponse(result));
    }

    [HttpGet]
    [ProducesResponseType(typeof(EspeciesListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<EspeciesListResponse>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        [FromQuery] bool? ativo = null,
        [FromQuery] string sortBy = "nomeComum",
        [FromQuery] string sortDirection = "asc",
        CancellationToken cancellationToken = default)
    {
        if (!TryParseSort(sortBy, out var sort) || !TryParseDirection(sortDirection, out var descending))
        {
            return BadRequest(new ProblemDetails { Title = "Invalid sorting", Status = StatusCodes.Status400BadRequest });
        }

        try
        {
            var result = await especies.ListAsync(new EspecieListQuery(page, pageSize, search, ativo, sort, descending), cancellationToken);
            return Ok(new EspeciesListResponse(result.Items.Select(ToResponse).ToArray(), result.Page, result.PageSize, result.TotalItems, result.TotalPages));
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid query", Detail = exception.Message, Status = StatusCodes.Status400BadRequest });
        }
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(EspecieResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<EspecieResponse>> Update(Guid id, UpdateEspecieRequest request, CancellationToken cancellationToken)
        => ExecuteMutation(() => especies.UpdateAsync(id, new UpdateEspecieCommand(request.NomeComum, request.NomeCientifico), cancellationToken));

    [HttpPatch("{id:guid}/ativo")]
    [ProducesResponseType(typeof(EspecieResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<EspecieResponse>> SetActive(Guid id, UpdateEspecieStatusRequest request, CancellationToken cancellationToken)
        => ExecuteMutation(() => especies.SetActiveAsync(id, request.Ativo, cancellationToken));

    private async Task<ActionResult<EspecieResponse>> ExecuteMutation(Func<Task<EspecieResult>> action)
    {
        try { return Ok(ToResponse(await action())); }
        catch (EspecieNotFoundException) { return NotFound(); }
        catch (EspecieDuplicateException exception) { return Conflict(ToDuplicateProblem(exception)); }
        catch (ArgumentException exception) { return BadRequest(ToInvalidDataProblem(exception)); }
    }

    private static ProblemDetails ToDuplicateProblem(EspecieDuplicateException exception) => new()
    {
        Title = "Species already exists",
        Detail = exception.Field == EspecieDuplicateField.NomeComum
            ? "A species with the same common name already exists."
            : "A species with the same scientific name already exists.",
        Status = StatusCodes.Status409Conflict,
    };

    private static ProblemDetails ToInvalidDataProblem(ArgumentException exception) => new()
    {
        Title = "Invalid species data",
        Detail = exception.Message,
        Status = StatusCodes.Status400BadRequest,
    };

    private static bool TryParseSort(string value, out EspecieSortField sort) => value switch
    {
        "nomeComum" => Assign(EspecieSortField.NomeComum, out sort),
        "nomeCientifico" => Assign(EspecieSortField.NomeCientifico, out sort),
        "ativo" => Assign(EspecieSortField.Ativo, out sort),
        "createdAtUtc" => Assign(EspecieSortField.CreatedAtUtc, out sort),
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

    private static EspecieResponse ToResponse(EspecieResult result) => new(
        result.Id,
        result.NomeComum,
        result.NomeCientifico,
        result.Ativo,
        result.CreatedAtUtc,
        result.UpdatedAtUtc);
}
