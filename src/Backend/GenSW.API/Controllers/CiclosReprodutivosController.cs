using GenSW.API.Contracts.ReproductiveCycles;
using GenSW.Application.Animals.CiclosReprodutivos;
using GenSW.Application.Animals.Cruzamentos;
using GenSW.Domain.Animals;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace GenSW.API.Controllers;
[ApiController,Authorize,Route("api/v1/ciclos-reprodutivos")]
public sealed class CiclosReprodutivosController(ICicloReprodutivoService service):ControllerBase
{
 [HttpPost] public Task<ActionResult<CicloReprodutivoResponse>> Create([FromQuery]Guid cruzamentoId,CicloReprodutivoRequest r,CancellationToken ct)=>Mutation(async()=>{var x=await service.CreateAsync(cruzamentoId,Command(r),ct);return CreatedAtAction(nameof(Get),new{id=x.Id},To(x));});
 [HttpGet] public async Task<ActionResult<CiclosReprodutivosListResponse>> List([FromQuery]int page=1,[FromQuery]int pageSize=25,[FromQuery]Guid? cruzamentoId=null,[FromQuery]TipoCicloReprodutivo? tipo=null,[FromQuery]StatusCicloReprodutivo? status=null,CancellationToken ct=default){try{var x=await service.ListAsync(new(page,pageSize,cruzamentoId,tipo,status),ct);return Ok(new CiclosReprodutivosListResponse(x.Items.Select(To).ToArray(),x.Page,x.PageSize,x.TotalItems,x.TotalPages));}catch(ArgumentException){return BadRequest(Problem("The supplied list query is invalid.",statusCode:400));}}
 [HttpGet("{id:guid}")] public async Task<ActionResult<CicloReprodutivoResponse>> Get(Guid id,CancellationToken ct){try{return Ok(To(await service.GetAsync(id,ct)));}catch(CicloReprodutivoNotFoundException){return NotFound();}}
 [HttpPut("{id:guid}")] public Task<ActionResult<CicloReprodutivoResponse>> Update(Guid id,CicloReprodutivoRequest r,CancellationToken ct)=>Mutation(async()=>Ok(To(await service.UpdateAsync(id,Command(r),ct))));
 [HttpPatch("{id:guid}/status")] public Task<ActionResult<CicloReprodutivoResponse>> Status(Guid id,CicloReprodutivoStatusRequest r,CancellationToken ct)=>Mutation(async()=>Ok(To(await service.SetStatusAsync(id,new(r.Status),ct))));
 private async Task<ActionResult<CicloReprodutivoResponse>> Mutation(Func<Task<ActionResult<CicloReprodutivoResponse>>> action){try{return await action();}catch(CicloReprodutivoNotFoundException){return NotFound();}catch(CruzamentoNotFoundException){return NotFound();}catch(ArgumentException){return BadRequest(Problem("The submitted reproductive cycle data is invalid.",statusCode:400));}}
 private static CicloReprodutivoCommand Command(CicloReprodutivoRequest x)=>new(x.Tipo,x.Status,x.DataPostura,x.DataInicioIncubacao,x.DataEclosao,x.OvosPostos,x.OvosFerteis,x.OvosIncubados,x.OvosEclodidos,x.OvosInviaveis,x.PesoMedioOvoGramas,x.DataInicioGestacao,x.DataPrevistaParto,x.DataParto,x.Nascidos,x.NascidosVivos,x.NascidosMortos,x.PesoAoNascerGramas,x.Observacao);
 private static CicloReprodutivoResponse To(CicloReprodutivoResult x)=>new(x.Id,x.CruzamentoId,x.Tipo,x.Status,x.DataPostura,x.DataInicioIncubacao,x.DataEclosao,x.OvosPostos,x.OvosFerteis,x.OvosIncubados,x.OvosEclodidos,x.OvosInviaveis,x.PesoMedioOvoGramas,x.DataInicioGestacao,x.DataPrevistaParto,x.DataParto,x.Nascidos,x.NascidosVivos,x.NascidosMortos,x.PesoAoNascerGramas,x.Observacao,x.DuracaoIncubacaoDias,x.DuracaoGestacaoDias,x.TaxaFertilidade,x.TaxaEclosao,x.CreatedAtUtc,x.UpdatedAtUtc);
}
