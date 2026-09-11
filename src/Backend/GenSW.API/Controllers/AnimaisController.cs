using GenSW.API.Contracts.Animals;
using GenSW.Application.Animals;
using GenSW.Application.Breeds;
using GenSW.Application.Species;
using GenSW.Application.Varieties;
using GenSW.Domain.Animals;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GenSW.API.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/animais")]
public sealed class AnimaisController(IAnimalService animais) : ControllerBase
{
    private const string DuplicateCodeConflictTitle = "Animal code already exists";
    private const string DuplicateCodeConflictDetail = "The animal code is already in use.";
    private const string AutomaticCollisionConflictTitle = "Automatic animal code allocation failed";
    private const string AutomaticCollisionConflictDetail = "The automatic animal code allocation limit was reached.";
    private const string AutomaticSequenceConflictTitle = "Animal code sequence is exhausted";
    private const string AutomaticSequenceConflictDetail = "No more automatic animal codes are available.";
    private const string InvalidDataDetail = "The submitted animal data is invalid.";
    private const string InvalidQueryDetail = "The supplied list query is invalid.";

    [HttpPost]
    [ProducesResponseType(typeof(AnimalResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AnimalResponse>> Create(CreateAnimalRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await animais.CreateAsync(
                new CreateAnimalCommand(
                    request.CodigoInterno, request.Nome, request.EspecieId, request.RacaId, request.VariedadeId,
                    request.Sexo, request.DataNascimento, request.Escopo),
                cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, ToResponse(result));
        }
        catch (EspecieNotFoundException) { return NotFound(); }
        catch (RacaNotFoundException) { return NotFound(); }
        catch (VariedadeNotFoundException) { return NotFound(); }
        catch (AnimalDuplicateException) { return Conflict(ToConflictProblem(DuplicateCodeConflictTitle, DuplicateCodeConflictDetail)); }
        catch (AnimalAutomaticCodeCollisionLimitExceededException) { return Conflict(ToConflictProblem(AutomaticCollisionConflictTitle, AutomaticCollisionConflictDetail)); }
        catch (AnimalCodeSequenceExhaustedException) { return Conflict(ToConflictProblem(AutomaticSequenceConflictTitle, AutomaticSequenceConflictDetail)); }
        catch (ArgumentException) { return BadRequest(ToInvalidDataProblem()); }
    }

    [HttpGet]
    [ProducesResponseType(typeof(AnimalsListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AnimalsListResponse>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        [FromQuery] Guid? especieId = null,
        [FromQuery] Guid? racaId = null,
        [FromQuery] Guid? variedadeId = null,
        [FromQuery] SexoAnimal? sexo = null,
        [FromQuery] EscopoAnimal? escopo = null,
        [FromQuery] bool? ativo = null,
        [FromQuery] string sortBy = "codigoInterno",
        [FromQuery] string sortDirection = "asc",
        CancellationToken cancellationToken = default)
    {
        if (!TryParseSort(sortBy, out var sort) || !TryParseDirection(sortDirection, out var descending))
        {
            return BadRequest(new ProblemDetails { Title = "Invalid sorting", Status = StatusCodes.Status400BadRequest });
        }

        try
        {
            var result = await animais.ListAsync(
                new AnimalListQuery(
                    page, pageSize, search, especieId, racaId, variedadeId, sexo, escopo, ativo, sort, descending),
                cancellationToken);
            return Ok(new AnimalsListResponse(
                result.Items.Select(ToResponse).ToArray(),
                result.Page,
                result.PageSize,
                result.TotalItems,
                result.TotalPages));
        }
        catch (ArgumentException)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid query",
                Detail = InvalidQueryDetail,
                Status = StatusCodes.Status400BadRequest,
            });
        }
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AnimalResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AnimalResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await animais.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(ToResponse(result));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(AnimalResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public Task<ActionResult<AnimalResponse>> Update(Guid id, UpdateAnimalRequest request, CancellationToken cancellationToken)
        => ExecuteMutation(() => animais.UpdateAsync(
            id,
            new UpdateAnimalCommand(
                request.CodigoInterno, request.Nome, request.EspecieId, request.RacaId, request.VariedadeId,
                request.Sexo, request.DataNascimento, request.Escopo),
            cancellationToken));

    [HttpPatch("{id:guid}/ativo")]
    [ProducesResponseType(typeof(AnimalResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public Task<ActionResult<AnimalResponse>> SetActive(Guid id, UpdateAnimalStatusRequest request, CancellationToken cancellationToken)
        => ExecuteMutation(() => animais.SetActiveAsync(id, request.Ativo, cancellationToken));

    private async Task<ActionResult<AnimalResponse>> ExecuteMutation(Func<Task<AnimalResult>> action)
    {
        try { return Ok(ToResponse(await action())); }
        catch (AnimalNotFoundException) { return NotFound(); }
        catch (EspecieNotFoundException) { return NotFound(); }
        catch (RacaNotFoundException) { return NotFound(); }
        catch (VariedadeNotFoundException) { return NotFound(); }
        catch (AnimalDuplicateException) { return Conflict(ToConflictProblem(DuplicateCodeConflictTitle, DuplicateCodeConflictDetail)); }
        catch (AnimalAutomaticCodeCollisionLimitExceededException) { return Conflict(ToConflictProblem(AutomaticCollisionConflictTitle, AutomaticCollisionConflictDetail)); }
        catch (AnimalCodeSequenceExhaustedException) { return Conflict(ToConflictProblem(AutomaticSequenceConflictTitle, AutomaticSequenceConflictDetail)); }
        catch (ArgumentException) { return BadRequest(ToInvalidDataProblem()); }
    }

    private static ProblemDetails ToInvalidDataProblem() => new()
    {
        Title = "Invalid animal data",
        Detail = InvalidDataDetail,
        Status = StatusCodes.Status400BadRequest,
    };

    private static ProblemDetails ToConflictProblem(string title, string detail) => new()
    {
        Title = title,
        Detail = detail,
        Status = StatusCodes.Status409Conflict,
    };

    private static bool TryParseSort(string value, out AnimalSortField sort) => value switch
    {
        "codigoInterno" => Assign(AnimalSortField.CodigoInterno, out sort),
        "nome" => Assign(AnimalSortField.Nome, out sort),
        "sexo" => Assign(AnimalSortField.Sexo, out sort),
        "escopo" => Assign(AnimalSortField.Escopo, out sort),
        "ativo" => Assign(AnimalSortField.Ativo, out sort),
        "createdAtUtc" => Assign(AnimalSortField.CreatedAtUtc, out sort),
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

    private static AnimalResponse ToResponse(AnimalResult result) => new(
        result.Id,
        result.CodigoInterno,
        result.Nome,
        result.EspecieId,
        result.RacaId,
        result.VariedadeId,
        result.Sexo,
        result.DataNascimento,
        result.Escopo,
        result.Ativo,
        result.CreatedAtUtc,
        result.UpdatedAtUtc,
        new AnimalEspecieResumoResponse(result.Especie.Id, result.Especie.NomeComum, result.Especie.Ativo),
        result.Raca is null ? null : new AnimalRacaResumoResponse(result.Raca.Id, result.Raca.Nome, result.Raca.Ativo),
        result.Variedade is null ? null : new AnimalVariedadeResumoResponse(result.Variedade.Id, result.Variedade.Nome, result.Variedade.Ativo));
}
