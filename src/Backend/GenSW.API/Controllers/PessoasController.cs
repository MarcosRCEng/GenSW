using GenSW.API.Contracts.People;
using GenSW.Application.People;
using GenSW.Domain.People;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GenSW.API.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/pessoas")]
public sealed class PessoasController(IPessoaService pessoas) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(PessoaResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PessoaResponse>> Create(CreatePessoaRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await pessoas.CreateAsync(new CreatePessoaCommand(request.TipoPessoa, request.Nome, request.NomeFantasia), cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, ToResponse(result));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid person data", Detail = exception.Message, Status = StatusCodes.Status400BadRequest });
        }
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PessoaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PessoaResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await pessoas.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(ToResponse(result));
    }

    [HttpGet]
    [ProducesResponseType(typeof(PessoasListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PessoasListResponse>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        [FromQuery] TipoPessoa? tipoPessoa = null,
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
            var result = await pessoas.ListAsync(new PessoaListQuery(page, pageSize, search, tipoPessoa, ativo, sort, descending), cancellationToken);
            return Ok(new PessoasListResponse(result.Items.Select(ToResponse).ToArray(), result.Page, result.PageSize, result.TotalItems, result.TotalPages));
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid query", Detail = exception.Message, Status = StatusCodes.Status400BadRequest });
        }
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(PessoaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<PessoaResponse>> Update(Guid id, UpdatePessoaRequest request, CancellationToken cancellationToken)
        => ExecuteMutation(() => pessoas.UpdateAsync(id, new UpdatePessoaCommand(request.Nome, request.NomeFantasia), cancellationToken));

    [HttpPatch("{id:guid}/ativo")]
    [ProducesResponseType(typeof(PessoaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<PessoaResponse>> SetActive(Guid id, UpdatePessoaStatusRequest request, CancellationToken cancellationToken)
        => ExecuteMutation(() => pessoas.SetActiveAsync(id, request.Ativo, cancellationToken));

    private async Task<ActionResult<PessoaResponse>> ExecuteMutation(Func<Task<PessoaResult>> action)
    {
        try { return Ok(ToResponse(await action())); }
        catch (PessoaNotFoundException) { return NotFound(); }
        catch (PessoaInactiveException) { return Conflict(); }
        catch (ArgumentException exception) { return BadRequest(new ProblemDetails { Title = "Invalid person data", Detail = exception.Message, Status = StatusCodes.Status400BadRequest }); }
    }

    private static bool TryParseSort(string value, out PessoaSortField sort) => value.ToLowerInvariant() switch
    {
        "nome" => Assign(PessoaSortField.Nome, out sort),
        "tipopessoa" => Assign(PessoaSortField.TipoPessoa, out sort),
        "ativo" => Assign(PessoaSortField.Ativo, out sort),
        "createdatutc" => Assign(PessoaSortField.CreatedAtUtc, out sort),
        _ => Assign(default, out sort, false),
    };

    private static bool TryParseDirection(string value, out bool descending) => value.ToLowerInvariant() switch
    {
        "asc" => Assign(false, out descending), "desc" => Assign(true, out descending), _ => Assign(false, out descending, false),
    };

    private static bool Assign<T>(T value, out T output, bool success = true) { output = value; return success; }
    private static PessoaResponse ToResponse(PessoaResult result) => new(result.Id, result.TipoPessoa, result.Nome, result.NomeFantasia, result.Ativo, result.CreatedAtUtc, result.UpdatedAtUtc);
}
