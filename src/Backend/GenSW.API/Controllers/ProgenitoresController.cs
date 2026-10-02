using GenSW.API.Contracts.AnimalEvolution;
using GenSW.Application.Animals.Filiacoes;
using GenSW.Domain.Animals;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace GenSW.API.Controllers;

[ApiController, Authorize, AnimalEvolution, Route("api/v1/animais/{animalId:guid}/progenitores-elegiveis")]
public sealed class ProgenitoresController(IProgenitorQuery query) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search(Guid animalId, [FromQuery] TipoFiliacaoAnimal tipoFiliacao,
        [FromQuery] string? search = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default) =>
        Ok(EvolutionResponse.Map(await query.SearchAsync(animalId, tipoFiliacao, search, page, pageSize, ct)));
}
