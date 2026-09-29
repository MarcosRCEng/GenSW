using GenSW.API.Contracts.Animals;
using GenSW.API.Contracts.Offspring;
using GenSW.Application.Animals;
using GenSW.Application.Animals.Proles;
using GenSW.Domain.Animals;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace GenSW.API.Controllers;
[ApiController,Authorize,Route("api/v1/proles")]
public sealed class ProlesController(IProleService service):ControllerBase
{
 [HttpPost] public Task<ActionResult<ProleResponse>> Create([FromQuery]Guid cicloReprodutivoId,ProleRequest r,CancellationToken ct)=>Run<ProleResponse>(async()=>{var x=await service.CreateAsync(cicloReprodutivoId,new(r.TipoRegistro,r.Quantidade,r.Origem,r.Data,r.PesoGramas,r.Sexo,r.Condicao,r.Observacao),ct);return CreatedAtAction(nameof(Get),new{id=x.Id},To(x));});
 [HttpGet] public async Task<ActionResult<ProlesListResponse>> List([FromQuery]int page=1,[FromQuery]int pageSize=25,[FromQuery]Guid? cicloReprodutivoId=null,[FromQuery]TipoRegistroProle? tipoRegistro=null,[FromQuery]TipoOrigemProle? origem=null,[FromQuery]bool? convertida=null,CancellationToken ct=default){try{var x=await service.ListAsync(new(page,pageSize,cicloReprodutivoId,tipoRegistro,origem,convertida),ct);return Ok(new ProlesListResponse(x.Items.Select(To).ToArray(),x.Page,x.PageSize,x.TotalItems,x.TotalPages));}catch(ArgumentException){return BadRequest(Problem("The supplied list query is invalid.",statusCode:400));}}
 [HttpGet("{id:guid}")] public Task<ActionResult<ProleResponse>> Get(Guid id,CancellationToken ct)=>Run<ProleResponse>(async()=>Ok(To(await service.GetAsync(id,ct))));
 [HttpPut("{id:guid}")] public Task<ActionResult<ProleResponse>> Update(Guid id,ProleUpdateRequest r,CancellationToken ct)=>Run<ProleResponse>(async()=>Ok(To(await service.UpdateAsync(id,new(r.Origem,r.Data,r.PesoGramas,r.Sexo,r.Condicao,r.Observacao),ct))));
 [HttpPost("{id:guid}/desdobramentos")] public Task<ActionResult<ProleResponse>> Split(Guid id,ProleUpdateRequest r,CancellationToken ct)=>Run<ProleResponse>(async()=>{var x=await service.SplitAsync(id,new(r.Origem,r.Data,r.PesoGramas,r.Sexo,r.Condicao,r.Observacao),ct);return CreatedAtAction(nameof(Get),new{id=x.Id},To(x));});
 [HttpPost("{id:guid}/conversoes")] public Task<ActionResult<ProleConversaoResponse>> Convert(Guid id,ProleConversaoRequest r,CancellationToken ct)=>Run<ProleConversaoResponse>(async()=>{var x=await service.ConvertAsync(id,new(r.CodigoInterno,r.Nome,r.EspecieId,r.RacaId,r.VariedadeId,r.Escopo),ct);return Ok(new ProleConversaoResponse(To(x.Prole),Animal(x.Animal),new(x.PaiSugerido.Id,x.PaiSugerido.CodigoInterno,x.PaiSugerido.Nome),new(x.MaeSugerida.Id,x.MaeSugerida.CodigoInterno,x.MaeSugerida.Nome)));});
 private async Task<ActionResult<T>> Run<T>(Func<Task<ActionResult<T>>> action){try{return await action();}catch(ProleNotFoundException){return NotFound();}catch(ProleConflictException e){return Conflict(Problem(e.Message,statusCode:409));}catch(ArgumentException){return BadRequest(Problem("The submitted offspring data is invalid.",statusCode:400));}catch(AnimalNotFoundException){return NotFound();}}
 private static ProleResponse To(ProleResult x)=>new(x.Id,x.CicloReprodutivoId,x.LoteOrigemId,x.TipoRegistro,x.Quantidade,x.QuantidadeDesdobrada,x.Origem,x.Data,x.PesoGramas,x.Sexo,x.Condicao,x.Observacao,x.AnimalId,x.CreatedAtUtc,x.UpdatedAtUtc);
 private static AnimalResponse Animal(AnimalResult r)=>new(r.Id,r.CodigoInterno,r.Nome,r.EspecieId,r.RacaId,r.VariedadeId,r.Sexo,r.DataNascimento,r.Escopo,r.Ativo,r.CreatedAtUtc,r.UpdatedAtUtc,new(r.Especie.Id,r.Especie.NomeComum,r.Especie.Ativo),r.Raca is null?null:new(r.Raca.Id,r.Raca.Nome,r.Raca.Ativo),r.Variedade is null?null:new(r.Variedade.Id,r.Variedade.Nome,r.Variedade.Ativo));
}
