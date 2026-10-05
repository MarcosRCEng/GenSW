using GenSW.API.Contracts.Properties;
using GenSW.Application.Properties;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GenSW.API.Controllers;

[ApiController, Authorize, AnimalEvolution, Route("api/v1/propriedades")]
public sealed class PropriedadesController(PropriedadeService service) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(PropriedadeRequest request, CancellationToken ct)
    {
        var item = await service.CreateAsync(new(request.Nome, request.Localizacao, request.Observacao), ct);
        return CreatedAtAction(nameof(Get), new { id = item.Id }, item);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(await service.GetAsync(id, ct));

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null, [FromQuery] bool? ativo = null, [FromQuery] string sortBy = "nome",
        [FromQuery] string sortDirection = "asc", CancellationToken ct = default) =>
        Ok(await service.ListAsync(new(page, pageSize, search, ativo, sortBy, sortDirection), ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, PropriedadeRequest request, CancellationToken ct) =>
        Ok(await service.UpdateAsync(id, new(request.Nome, request.Localizacao, request.Observacao), ct));

    [HttpPatch("{id:guid}/ativo")]
    public async Task<IActionResult> SetActive(Guid id, PropriedadeStatusRequest request, CancellationToken ct) =>
        Ok(await service.SetActiveAsync(id, request.Ativo!.Value, ct));
}
