using System.Security.Claims;
using GenSW.Application.Formulation;
using GenSW.Domain.Formulation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GenSW.API.Controllers;

[ApiController, Authorize, Formulation, Route("api/v1")]
public sealed class FormulationController(FormulationService service) : ControllerBase
{
    private Guid Actor => Guid.Parse(User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpGet("receitas")] public async Task<IActionResult> Recipes([FromQuery] CatalogQuery query, CancellationToken ct) => Ok(await service.RecipesAsync(query, ct));
    [HttpGet("receitas/{id:guid}")] public async Task<IActionResult> Recipe(Guid id, CancellationToken ct) => Ok(await service.GetRecipeAsync(id, ct));
    [HttpPost("receitas")]
    public async Task<IActionResult> CreateRecipe(RecipeHeader body, CancellationToken ct)
    { var r = await service.CreateRecipeAsync(body, Actor, ct); return CreatedAtAction(nameof(Recipe), new { id = r.Id }, r); }
    [HttpPut("receitas/{id:guid}")] public async Task<IActionResult> UpdateRecipe(Guid id, RecipeCommand body, CancellationToken ct) => Ok(await service.UpdateRecipeAsync(id, body, Actor, ct));
    [HttpPatch("receitas/{id:guid}/ativo")] public async Task<IActionResult> RecipeStatus(Guid id, StatusCommand body, CancellationToken ct) => Ok(await service.RecipeStatusAsync(id, body, Actor, ct));
    [HttpGet("receitas/{id:guid}/historico")] public async Task<IActionResult> RecipeHistory(Guid id, [FromQuery] CatalogQuery query, CancellationToken ct) => Ok(await service.HistoryAsync(id, query, ct));
    [HttpGet("receitas/{id:guid}/versoes")] public async Task<IActionResult> Versions(Guid id, [FromQuery] CatalogQuery query, CancellationToken ct) => Ok(await service.VersionsAsync(id, query, ct));
    [HttpGet("receitas/versoes")] public async Task<IActionResult> AllVersions([FromQuery] CatalogQuery query, CancellationToken ct) => Ok(await service.VersionsAsync(null, query, ct));
    [HttpPost("receitas/{id:guid}/versoes")]
    public async Task<IActionResult> CreateVersion(Guid id, RecipeData body, CancellationToken ct)
    { var v = await service.CreateVersionAsync(id, body, Actor, ct); return CreatedAtAction(nameof(Version), new { id = v.Id }, v); }
    [HttpGet("receitas/versoes/{id:guid}")] public async Task<IActionResult> Version(Guid id, CancellationToken ct) => Ok(await service.GetVersionAsync(id, ct));
    [HttpPut("receitas/versoes/{id:guid}")] public async Task<IActionResult> UpdateVersion(Guid id, RecipeVersionCommand body, CancellationToken ct) => Ok(await service.UpdateVersionAsync(id, body, Actor, ct));
    [HttpPost("receitas/versoes/{id:guid}/publicacao")] public async Task<IActionResult> Publish(Guid id, VersionCommand body, CancellationToken ct) => Ok(await service.VersionStateAsync(id, body.VersaoEsperada, true, Actor, ct));
    [HttpPost("receitas/versoes/{id:guid}/inativacao")] public async Task<IActionResult> Inactivate(Guid id, VersionCommand body, CancellationToken ct) => Ok(await service.VersionStateAsync(id, body.VersaoEsperada, false, Actor, ct));
    [HttpGet("receitas/versoes/{id:guid}/escalonamento")] public async Task<IActionResult> Scale(Guid id, [FromQuery] string tamanho, [FromQuery] string unidade, CancellationToken ct) => Ok(await service.ScaleAsync(id, tamanho, unidade, ct));
    [HttpPost("simulacoes-formulacao")] public async Task<IActionResult> Simulate(SimulationCommand body, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct) => StatusCode(201, await service.SimulateAsync(body, key, Actor, ct));
    [HttpGet("simulacoes-formulacao")] public async Task<IActionResult> Simulations([FromQuery] CatalogQuery query, CancellationToken ct) => Ok(await service.SnapshotsAsync("Simulacao", query, ct));
    [HttpGet("simulacoes-formulacao/{id:guid}")] public async Task<IActionResult> Simulation(Guid id, CancellationToken ct) => Ok(await service.GetSimulationAsync(id, ct));
    [HttpPost("comparacoes-formulacao")] public async Task<IActionResult> Compare(ComparisonCommand body, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct) => StatusCode(201, await service.CompareAsync(body, key, Actor, ct));
    [HttpGet("comparacoes-formulacao")] public async Task<IActionResult> Comparisons([FromQuery] CatalogQuery query, CancellationToken ct) => Ok(await service.SnapshotsAsync("Comparacao", query, ct));
    [HttpGet("comparacoes-formulacao/{id:guid}")] public async Task<IActionResult> Comparison(Guid id, CancellationToken ct) => Ok(await service.GetComparisonAsync(id, ct));
}
