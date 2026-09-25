using GenSW.API.Contracts.AnimalIdentifications;
using GenSW.Application.Animals.Identificacoes;
using GenSW.Domain.Animals;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GenSW.API.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/identificacoes-animal")]
public sealed class IdentificacoesAnimalController(IIdentificacaoAnimalService identificacoes) : ControllerBase
{
    private const string InvalidQueryDetail = "The supplied identification list query is invalid.";

    [HttpGet]
    [ProducesResponseType(typeof(IdentificacoesAnimalGlobalListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IdentificacoesAnimalGlobalListResponse>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] TipoIdentificacaoAnimal? tipo = null,
        [FromQuery] string? valor = null,
        [FromQuery] bool? ativo = null,
        [FromQuery] bool? principal = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await identificacoes.ListGlobalAsync(
                new IdentificacaoAnimalListQuery(page, pageSize, tipo, valor, ativo, principal), cancellationToken);

            return Ok(new IdentificacoesAnimalGlobalListResponse(
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
                Title = "Invalid identification query",
                Detail = InvalidQueryDetail,
                Status = StatusCodes.Status400BadRequest,
            });
        }
    }

    private static IdentificacaoAnimalGlobalResponse ToResponse(IdentificacaoAnimalGlobalResult result) =>
        new(ToResponse(result.Identificacao),
            new IdentificacaoAnimalAnimalResumoResponse(result.Animal.Id, result.Animal.CodigoInterno, result.Animal.Nome));

    private static IdentificacaoAnimalResponse ToResponse(IdentificacaoAnimalResult result) => new(
        result.Id,
        result.AnimalId,
        result.Tipo,
        result.DescricaoTipo,
        result.Valor,
        result.Principal,
        result.DataAplicacao,
        result.Observacao,
        result.Ativo,
        result.CreatedAtUtc,
        result.UpdatedAtUtc);
}
