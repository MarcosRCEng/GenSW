using GenSW.API.Contracts.Properties;
using GenSW.Application.Properties;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GenSW.API.Controllers;

[ApiController, Authorize, AnimalEvolution, Route("api/v1/animais/{animalId:guid}")]
public sealed class AnimalPropriedadesController(PropriedadeService service) : ControllerBase
{
    [HttpGet("propriedades")]
    public async Task<IActionResult> History(Guid animalId, CancellationToken ct) => Ok(await service.HistoryAsync(animalId, ct));

    [HttpPost("propriedade/transferir")]
    public async Task<IActionResult> Transfer(Guid animalId, TransferirAnimalRequest request, CancellationToken ct) =>
        Ok(await service.TransferAsync(animalId,
            new(request.PropriedadeId, request.DataInicio!.Value, request.VinculoAtualIdEsperado, request.Observacao), ct));

    [HttpPost("propriedade/desvincular")]
    public async Task<IActionResult> Detach(Guid animalId, DesvincularAnimalRequest request, CancellationToken ct) =>
        Ok(await service.DetachAsync(animalId, new(request.DataFim!.Value, request.VinculoAtualIdEsperado!.Value), ct));
}
