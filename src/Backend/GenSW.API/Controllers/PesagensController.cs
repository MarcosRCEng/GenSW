using GenSW.API.Contracts.AnimalEvolution;
using GenSW.Application.Animals.Pesagens;
using GenSW.Domain.Animals;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GenSW.API.Controllers;

public sealed record PesagemRequest(DateOnly DataMedicao, decimal PesoGramas, TipoMarcoPesagem TipoMarco,
    string? DescricaoMarco, int? IdadeReferenciaDias, string? Observacao)
{
    public PesagemCommand ToCommand() => new(DataMedicao, PesoGramas, TipoMarco, DescricaoMarco, IdadeReferenciaDias, Observacao);
}

[ApiController, Authorize, AnimalEvolution, Route("api/v1/animais/{animalId:guid}/pesagens")]
public sealed class PesagensController(PesagemService service) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(Guid animalId, PesagemRequest request, CancellationToken ct)
    {
        var item = await service.SaveAsync(animalId, null, request.ToCommand(), ct);
        return CreatedAtAction(nameof(Get), new { animalId, id = item.Id }, EvolutionResponse.Map(item));
    }
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid animalId, Guid id, PesagemRequest request, CancellationToken ct) =>
        Ok(EvolutionResponse.Map(await service.SaveAsync(animalId, id, request.ToCommand(), ct)));
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid animalId, Guid id, CancellationToken ct) => Ok(EvolutionResponse.Map(await service.GetAsync(animalId, id, ct)));
    [HttpGet]
    public async Task<IActionResult> List(Guid animalId, [FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        [FromQuery] DateOnly? dataInicial = null, [FromQuery] DateOnly? dataFinal = null,
        [FromQuery] TipoMarcoPesagem? tipoMarco = null, CancellationToken ct = default) =>
        Ok(EvolutionResponse.Map(await service.ListAsync(animalId, new(page, pageSize, dataInicial, dataFinal, tipoMarco), ct)));
}
