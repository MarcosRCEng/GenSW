using GenSW.API.Contracts.AnimalEvolution;
using GenSW.Application.Animals;
using GenSW.Application.Images;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace GenSW.API.Controllers;

public sealed class ImageUploadRequest
{
    public IFormFile Arquivo { get; set; } = null!;
    public string? Legenda { get; set; }
    public DateOnly? DataCaptura { get; set; }
}
public sealed record ImageMetadataRequest(string? Legenda, DateOnly? DataCaptura);
public sealed record ImageActiveRequest(bool Ativo);
public sealed record ImagePreferredRequest(bool Representativa);
public sealed record ImageOrderRequest(IReadOnlyList<Guid> Ids);

[ApiController, Authorize, AnimalEvolution]
[Route("api/v1/animais/{owner:guid}/imagens")]
[Route("api/v1/variedades/{owner:guid}/imagens")]
public sealed class ImagesController(ImageService service) : ControllerBase
{
    private ImageOwner Kind => Request.Path.StartsWithSegments("/api/v1/animais") ? ImageOwner.Animal : ImageOwner.Variedade;
    [HttpPost, RequestSizeLimit(6 * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024)]
    public async Task<IActionResult> Upload(Guid owner, [FromForm] ImageUploadRequest request, CancellationToken ct)
    {
        if (request.Arquivo is null || Request.Form.Files.Count != 1) throw new ArgumentException("Envie exatamente um arquivo.");
        if (request.Arquivo.Length > 5 * 1024 * 1024) throw new AnimalEvolutionException(413, "arquivo_grande", "Arquivo excede 5 MiB.");
        await using var input = request.Arquivo.OpenReadStream();
        var item = await service.UploadAsync(Kind, owner, input, request.Legenda, request.DataCaptura, ct);
        return Created(item.ConteudoPath, EvolutionResponse.Map(item));
    }
    [HttpGet]
    public async Task<IActionResult> List(Guid owner, [FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        [FromQuery] bool? ativo = true, CancellationToken ct = default) => Ok(EvolutionResponse.Map(await service.ListAsync(Kind, owner, ativo, page, pageSize, ct)));
    [HttpPut("{id:guid}/metadados")]
    public async Task<IActionResult> Edit(Guid owner, Guid id, ImageMetadataRequest request, CancellationToken ct) => Ok(EvolutionResponse.Map(await service.EditAsync(Kind, owner, id, request.Legenda, request.DataCaptura, ct)));
    [HttpPut("{id:guid}/ativo")]
    public async Task<IActionResult> Active(Guid owner, Guid id, ImageActiveRequest request, CancellationToken ct) => Ok(EvolutionResponse.Map(await service.SetActiveAsync(Kind, owner, id, request.Ativo, ct)));
    [HttpPut("{id:guid}/representativa")]
    public async Task<IActionResult> Preferred(Guid owner, Guid id, ImagePreferredRequest request, CancellationToken ct) => Ok(EvolutionResponse.Map(await service.PreferAsync(Kind, owner, id, request.Representativa, ct)));
    [HttpPut("ordem")]
    public async Task<IActionResult> Order(Guid owner, ImageOrderRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request.Ids);
        await service.ReorderAsync(Kind, owner, request.Ids, ct); return NoContent();
    }
    [HttpGet("preferencial")]
    public async Task<IActionResult> Preferred(Guid owner, CancellationToken ct) => Ok(EvolutionResponse.Map(await service.PreferredAsync(Kind, owner, ct)));
    [HttpGet("{id:guid}/conteudo")]
    public async Task<IActionResult> Content(Guid owner, Guid id, [FromQuery] string versao = "visualizacao", CancellationToken ct = default)
    {
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(await service.ContentAsync(Kind, owner, id, versao, ct), "image/png");
    }
}
