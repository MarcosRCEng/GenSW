using GenSW.API.Contracts.AnimalIdentifications;
using GenSW.Application.Animals;
using GenSW.Application.Animals.Identificacoes;
using GenSW.Domain.Animals;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GenSW.API.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/animais/{animalId:guid}/identificacoes")]
public sealed class IdentificacoesAnimaisController(IIdentificacaoAnimalService identificacoes) : ControllerBase
{
    private const string InvalidDataDetail = "The submitted identification data is invalid.";
    private const string InvalidQueryDetail = "The supplied identification list query is invalid.";
    private const string DuplicateDetail = "The physical identification already exists.";
    private const string PrincipalConflictDetail = "The principal physical identification could not be updated.";

    [HttpPost]
    [ProducesResponseType(typeof(IdentificacaoAnimalResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<IdentificacaoAnimalResponse>> Create(Guid animalId,
        CreateIdentificacaoAnimalRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await identificacoes.CreateAsync(animalId,
                new CreateIdentificacaoAnimalCommand(request.Tipo, request.DescricaoTipo, request.Valor,
                    request.Principal, request.DataAplicacao, request.Observacao), cancellationToken);
            return CreatedAtAction(nameof(GetById), new { animalId, identificacaoId = result.Id }, ToResponse(result));
        }
        catch (AnimalNotFoundException) { return NotFound(); }
        catch (IdentificacaoAnimalDuplicateException) { return Conflict(ConflictProblem("Duplicate physical identification", DuplicateDetail)); }
        catch (IdentificacaoAnimalPrincipalConflictException) { return Conflict(ConflictProblem("Principal physical identification conflict", PrincipalConflictDetail)); }
        catch (ArgumentException) { return BadRequest(InvalidDataProblem()); }
        catch (InvalidOperationException) { return BadRequest(InvalidDataProblem()); }
    }

    [HttpGet]
    [ProducesResponseType(typeof(IdentificacoesAnimalListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IdentificacoesAnimalListResponse>> List(Guid animalId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        [FromQuery] TipoIdentificacaoAnimal? tipo = null, [FromQuery] string? valor = null,
        [FromQuery] bool? ativo = null, [FromQuery] bool? principal = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await identificacoes.ListByAnimalAsync(animalId,
                new IdentificacaoAnimalListQuery(page, pageSize, tipo, valor, ativo, principal), cancellationToken);
            return Ok(new IdentificacoesAnimalListResponse(result.Items.Select(ToResponse).ToArray(), result.Page,
                result.PageSize, result.TotalItems, result.TotalPages));
        }
        catch (AnimalNotFoundException) { return NotFound(); }
        catch (ArgumentException) { return BadRequest(InvalidQueryProblem()); }
    }

    [HttpGet("{identificacaoId:guid}")]
    [ProducesResponseType(typeof(IdentificacaoAnimalResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<IdentificacaoAnimalResponse>> GetById(Guid animalId, Guid identificacaoId,
        CancellationToken cancellationToken) => ExecuteRead(() => identificacoes.GetByAnimalAsync(animalId, identificacaoId, cancellationToken));

    [HttpPatch("{identificacaoId:guid}")]
    [ProducesResponseType(typeof(IdentificacaoAnimalResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<IdentificacaoAnimalResponse>> UpdateMetadata(Guid animalId, Guid identificacaoId,
        UpdateIdentificacaoAnimalMetadataRequest request, CancellationToken cancellationToken) =>
        ExecuteMutation(() => identificacoes.UpdateMetadataAsync(animalId, identificacaoId,
            new UpdateIdentificacaoAnimalMetadataCommand(request.HasDataAplicacao, request.DataAplicacao,
                request.HasObservacao, request.Observacao), cancellationToken));

    [HttpPatch("{identificacaoId:guid}/ativo")]
    [ProducesResponseType(typeof(IdentificacaoAnimalResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<IdentificacaoAnimalResponse>> SetAtivo(Guid animalId, Guid identificacaoId,
        UpdateIdentificacaoAnimalAtivoRequest request, CancellationToken cancellationToken) =>
        ExecuteMutation(() => identificacoes.SetAtivoAsync(animalId, identificacaoId,
            new SetIdentificacaoAnimalAtivoCommand(request.Ativo), cancellationToken));

    [HttpPatch("{identificacaoId:guid}/principal")]
    [ProducesResponseType(typeof(IdentificacaoAnimalResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public Task<ActionResult<IdentificacaoAnimalResponse>> SetPrincipal(Guid animalId, Guid identificacaoId,
        UpdateIdentificacaoAnimalPrincipalRequest request, CancellationToken cancellationToken) =>
        ExecuteMutation(() => identificacoes.SetPrincipalAsync(animalId, identificacaoId,
            new SetIdentificacaoAnimalPrincipalCommand(request.Principal), cancellationToken));

    private async Task<ActionResult<IdentificacaoAnimalResponse>> ExecuteRead(Func<Task<IdentificacaoAnimalResult>> action)
    {
        try { return Ok(ToResponse(await action())); }
        catch (AnimalNotFoundException) { return NotFound(); }
        catch (IdentificacaoAnimalNotFoundException) { return NotFound(); }
    }

    private async Task<ActionResult<IdentificacaoAnimalResponse>> ExecuteMutation(Func<Task<IdentificacaoAnimalResult>> action)
    {
        try { return Ok(ToResponse(await action())); }
        catch (AnimalNotFoundException) { return NotFound(); }
        catch (IdentificacaoAnimalNotFoundException) { return NotFound(); }
        catch (IdentificacaoAnimalDuplicateException) { return Conflict(ConflictProblem("Duplicate physical identification", DuplicateDetail)); }
        catch (IdentificacaoAnimalPrincipalConflictException) { return Conflict(ConflictProblem("Principal physical identification conflict", PrincipalConflictDetail)); }
        catch (ArgumentException) { return BadRequest(InvalidDataProblem()); }
        catch (InvalidOperationException) { return BadRequest(InvalidDataProblem()); }
    }

    private static IdentificacaoAnimalResponse ToResponse(IdentificacaoAnimalResult result) => new(result.Id,
        result.AnimalId, result.Tipo, result.DescricaoTipo, result.Valor, result.Principal, result.DataAplicacao,
        result.Observacao, result.Ativo, result.CreatedAtUtc, result.UpdatedAtUtc);

    private static ProblemDetails InvalidDataProblem() => new()
    {
        Title = "Invalid physical identification data", Detail = InvalidDataDetail, Status = StatusCodes.Status400BadRequest,
    };

    private static ProblemDetails InvalidQueryProblem() => new()
    {
        Title = "Invalid identification query", Detail = InvalidQueryDetail, Status = StatusCodes.Status400BadRequest,
    };

    private static ProblemDetails ConflictProblem(string title, string detail) => new()
    {
        Title = title, Detail = detail, Status = StatusCodes.Status409Conflict,
    };
}
