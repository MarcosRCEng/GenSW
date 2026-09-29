using GenSW.API.Contracts.Breedings;
using GenSW.Application.Animals;
using GenSW.Application.Animals.Cruzamentos;
using GenSW.Domain.Animals;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace GenSW.API.Controllers;
[ApiController, Authorize, Route("api/v1/cruzamentos")]
public sealed class CruzamentosController(ICruzamentoService service) : ControllerBase
{
 [HttpPost] public Task<ActionResult<CruzamentoResponse>> Create(CreateCruzamentoRequest request,CancellationToken ct)=>Mutation(async()=>{var x=await service.CreateAsync(new(request.MachoId,request.FemeaId,request.Status,request.DataInicio,request.DataFim,request.Objetivo,request.Observacao),ct);return CreatedAtAction(nameof(Get),new{id=x.Id},To(x));});
 [HttpGet] public async Task<ActionResult<CruzamentosListResponse>> List([FromQuery]int page=1,[FromQuery]int pageSize=25,[FromQuery]StatusCruzamento? status=null,[FromQuery]Guid? machoId=null,[FromQuery]Guid? femeaId=null,CancellationToken ct=default){try{var x=await service.ListAsync(new(page,pageSize,status,machoId,femeaId),ct);return Ok(new CruzamentosListResponse(x.Items.Select(To).ToArray(),x.Page,x.PageSize,x.TotalItems,x.TotalPages));}catch(ArgumentException){return BadRequest(Problem("The supplied list query is invalid.",statusCode:400));}}
 [HttpGet("{id:guid}")] public async Task<ActionResult<CruzamentoResponse>> Get(Guid id,CancellationToken ct){try{return Ok(To(await service.GetAsync(id,ct)));}catch(CruzamentoNotFoundException){return NotFound();}}
 [HttpPut("{id:guid}")] public Task<ActionResult<CruzamentoResponse>> Update(Guid id,UpdateCruzamentoRequest request,CancellationToken ct)=>Mutation(async()=>Ok(To(await service.UpdateAsync(id,new(request.MachoId,request.FemeaId,request.Status,request.DataInicio,request.DataFim,request.Objetivo,request.Observacao),ct))));
 [HttpPatch("{id:guid}/status")] public Task<ActionResult<CruzamentoResponse>> Status(Guid id,UpdateCruzamentoStatusRequest request,CancellationToken ct)=>Mutation(async()=>Ok(To(await service.SetStatusAsync(id,new(request.Status),ct))));
 private async Task<ActionResult<CruzamentoResponse>> Mutation(Func<Task<ActionResult<CruzamentoResponse>>> action){try{return await action();}catch(AnimalNotFoundException){return NotFound();}catch(CruzamentoNotFoundException){return NotFound();}catch(CruzamentoConflictException e){return Conflict(Problem(e.Message,statusCode:409));}catch(ArgumentException){return BadRequest(Problem("The submitted breeding data is invalid.",statusCode:400));}}
 private static CruzamentoResponse To(CruzamentoResult x)=>new(x.Id,x.MachoId,x.FemeaId,x.Status,x.DataInicio,x.DataFim,x.Objetivo,x.Observacao,x.CreatedAtUtc,x.UpdatedAtUtc,new(x.Macho.Id,x.Macho.CodigoInterno,x.Macho.Nome),new(x.Femea.Id,x.Femea.CodigoInterno,x.Femea.Nome));
}
