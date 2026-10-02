using GenSW.API.Contracts.EggProduction;
using GenSW.Application.Animals.ProducaoOvos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace GenSW.API.Controllers;
[ApiController, Authorize, Route("api/v1/animais/{animalId:guid}/producoes-ovos")]
public sealed class ProducoesOvosController(IProducaoOvoService service) : ControllerBase
{
    [HttpPost] public async Task<ActionResult<ProducaoOvoResponse>> Create(Guid animalId, ProducaoOvoRequest request, CancellationToken ct) { try { var item = await service.CreateAsync(animalId, new(request.DataPostura, request.PesoGramas, request.Observacao), ct); return CreatedAtAction(nameof(List), new { animalId }, To(item)); } catch (ProducaoOvoNotEligibleException) { return NotFound(); } catch (ArgumentException) { return BadRequest(Problem("Dados de produção inválidos.", statusCode: 400)); } }
    [HttpGet] public async Task<ActionResult<ProducoesOvosListResponse>> List(Guid animalId, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] DateOnly? dataInicial = null, [FromQuery] DateOnly? dataFinal = null, CancellationToken ct = default) { try { var result = await service.ListAsync(animalId, new(page, pageSize, dataInicial, dataFinal), ct); return Ok(new ProducoesOvosListResponse(result.Items.Select(To).ToArray(), result.Page, result.PageSize, result.TotalItems, result.TotalPages, new(result.Metricas.TotalLancamentos, result.Metricas.PesoMedioGramas, result.Metricas.PesoMinimoGramas, result.Metricas.PesoMaximoGramas, result.Metricas.PesoPadraoGramas, result.Metricas.DiasAtePesoPadrao, result.Metricas.Evolucao.Select(To).ToArray()))); } catch (ProducaoOvoNotEligibleException) { return NotFound(); } catch (ArgumentException) { return BadRequest(Problem("Consulta inválida.", statusCode: 400)); } }
    [HttpPut("{id:guid}")] public async Task<ActionResult<ProducaoOvoResponse>> Update(Guid animalId, Guid id, ProducaoOvoRequest request, CancellationToken ct) { try { return Ok(To(await service.UpdateAsync(animalId, id, new(request.DataPostura, request.PesoGramas, request.Observacao), ct))); } catch (ProducaoOvoNotFoundException) { return NotFound(); } catch (ProducaoOvoNotEligibleException) { return NotFound(); } catch (ArgumentException) { return BadRequest(Problem("Dados de produção inválidos.", statusCode: 400)); } }
    private static ProducaoOvoResponse To(ProducaoOvoResult x) => new(x.Id, x.AnimalId, x.DataPostura, x.PesoGramas, x.Observacao, x.CreatedAtUtc, x.UpdatedAtUtc);
}
