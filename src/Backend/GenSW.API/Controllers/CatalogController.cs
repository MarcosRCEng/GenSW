using System.Security.Claims;
using GenSW.Application.Formulation;
using GenSW.Domain.Catalog;
using GenSW.Domain.Formulation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GenSW.API.Controllers;

[ApiController, Authorize, Formulation, Route("api/v1")]
public sealed class CatalogController(FormulationService service) : ControllerBase
{
    private Guid Actor => Guid.Parse(User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpGet("itens")] public async Task<IActionResult> Items([FromQuery] CatalogQuery query, CancellationToken ct) => Ok(await service.ItemsAsync(query, ct));
    [HttpGet("itens/{id:guid}")] public async Task<IActionResult> Item(Guid id, CancellationToken ct) => Ok(await service.GetItemAsync(id, ct));
    [HttpPost("itens")]
    public async Task<IActionResult> CreateItem(ItemData body, CancellationToken ct)
    { var item = await service.CreateItemAsync(body, Actor, ct); return CreatedAtAction(nameof(Item), new { id = item.Id }, item); }
    [HttpPut("itens/{id:guid}")] public async Task<IActionResult> UpdateItem(Guid id, ItemCommand body, CancellationToken ct) => Ok(await service.UpdateItemAsync(id, body, Actor, ct));
    [HttpPatch("itens/{id:guid}/ativo")] public async Task<IActionResult> ItemStatus(Guid id, StatusCommand body, CancellationToken ct) => Ok(await service.ItemStatusAsync(id, body, Actor, ct));
    [HttpGet("itens/{id:guid}/historico")] public async Task<IActionResult> ItemHistory(Guid id, [FromQuery] CatalogQuery query, CancellationToken ct) => Ok(await service.HistoryAsync(id, query, ct));
    [HttpGet("categorias-itens")] public async Task<IActionResult> Categories([FromQuery] CatalogQuery query, CancellationToken ct) => Ok(await service.CategoriesAsync(query, ct));
    [HttpPost("categorias-itens")] public async Task<IActionResult> CreateCategory(CategoryCommand body, CancellationToken ct) => StatusCode(201, await service.CreateCategoryAsync(body, Actor, ct));
    [HttpPut("categorias-itens/{id:guid}")] public async Task<IActionResult> UpdateCategory(Guid id, CategoryCommand body, CancellationToken ct) => Ok(await service.UpdateCategoryAsync(id, body, Actor, ct));
    [HttpGet("itens/{id:guid}/conversoes")] public async Task<IActionResult> Conversions(Guid id, [FromQuery] CatalogQuery query, CancellationToken ct) => Ok(await service.ConversionsAsync(id, query, ct));
    [HttpPost("itens/{id:guid}/conversoes")] public async Task<IActionResult> CreateConversion(Guid id, ConversionData body, CancellationToken ct) => StatusCode(201, await service.CreateConversionAsync(id, body, Actor, ct));
    [HttpGet("componentes-nutricionais")] public IActionResult Components() => Ok(NutritionComponents.All);
    [HttpGet("itens/{id:guid}/perfis-nutricionais")] public async Task<IActionResult> Profiles(Guid id, [FromQuery] CatalogQuery query, CancellationToken ct) => Ok(await service.ProfilesAsync(id, query, ct));
    [HttpPost("itens/{id:guid}/perfis-nutricionais")]
    public async Task<IActionResult> CreateProfile(Guid id, ProfileData body, CancellationToken ct)
    { var p = await service.CreateProfileAsync(id, body, Actor, ct); return CreatedAtAction(nameof(Profile), new { id = p.Id }, p); }
    [HttpGet("perfis-nutricionais/{id:guid}")] public async Task<IActionResult> Profile(Guid id, CancellationToken ct) => Ok(await service.GetProfileAsync(id, ct));
    [HttpPut("perfis-nutricionais/{id:guid}")] public async Task<IActionResult> UpdateProfile(Guid id, ProfileCommand body, CancellationToken ct) => Ok(await service.UpdateProfileAsync(id, body, Actor, ct));
    [HttpPost("perfis-nutricionais/{id:guid}/publicacao")] public async Task<IActionResult> PublishProfile(Guid id, VersionCommand body, CancellationToken ct) => Ok(await service.ProfileStateAsync(id, body.VersaoEsperada, true, Actor, ct));
    [HttpPost("perfis-nutricionais/{id:guid}/inativacao")] public async Task<IActionResult> InactivateProfile(Guid id, VersionCommand body, CancellationToken ct) => Ok(await service.ProfileStateAsync(id, body.VersaoEsperada, false, Actor, ct));
}
