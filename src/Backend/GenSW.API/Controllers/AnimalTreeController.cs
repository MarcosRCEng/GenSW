using GenSW.API.Contracts.AnimalEvolution;
using GenSW.Application.Animals.Tree;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace GenSW.API.Controllers;

[ApiController, Authorize, AnimalEvolution, Route("api/v1/animais/{animalId:guid}/arvore")]
public sealed class AnimalTreeController(IAnimalTreeQuery query) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(Guid animalId, [FromQuery] int ascendentes = 3, [FromQuery] int descendentes = 1, CancellationToken ct = default) =>
        Ok(EvolutionResponse.Map(await query.GetAsync(animalId, ascendentes, descendentes, ct)));
    [HttpGet("relacoes")]
    public async Task<IActionResult> Relations(Guid animalId, [FromQuery] string direcao, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(EvolutionResponse.Map(await query.RelationsAsync(animalId, direcao, page, pageSize, ct)));
}
