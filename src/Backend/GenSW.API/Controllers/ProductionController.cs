using System.Security.Claims;
using GenSW.Application.Production;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GenSW.API.Controllers;

[ApiController,Authorize,Production,Route("api/v1/ordens-producao")]
public sealed class ProductionController(ProductionService service):ControllerBase
{
    private Guid Actor=>Guid.Parse(User.FindFirstValue("sub")??User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string Key=>Request.Headers["Idempotency-Key"].ToString();
    private async Task<IActionResult> Mutation(Task<ProductionMutationResult> action)
    {var result=await action;if(result.Location is not null)Response.Headers.Location=result.Location;if(result.Replayed)Response.Headers["Idempotency-Replayed"]="true";return new ContentResult{StatusCode=result.StatusHttp,ContentType="application/json; charset=utf-8",Content=result.RespostaJson};}
    [HttpGet]public async Task<IActionResult> List([FromQuery]ProductionQuery q,CancellationToken ct)=>Ok(await service.ListAsync(q,Actor,ct));
    [HttpGet("{id:guid}")]public async Task<IActionResult> Get(Guid id,CancellationToken ct)=>Ok(await service.GetAsync(id,Actor,ct));
    [HttpPost]public Task<IActionResult> Create(OrderCommand c,CancellationToken ct)=>Mutation(service.CreateAsync(c,Key,Actor,ct));
    [HttpPut("{id:guid}")]public Task<IActionResult> Update(Guid id,OrderCommand c,CancellationToken ct)=>Mutation(service.UpdateAsync(id,c,Key,Actor,ct));
    [HttpPost("{id:guid}/planejamento")]public Task<IActionResult> Plan(Guid id,ProductionStateCommand c,CancellationToken ct)=>Mutation(service.PlanAsync(id,c,Key,Actor,ct));
    [HttpPost("{id:guid}/revisao-planejamento")]public Task<IActionResult> Revise(Guid id,ProductionStateCommand c,CancellationToken ct)=>Mutation(service.StateAsync(id,"revisao-planejamento",c,Key,Actor,ct));
    [HttpPost("{id:guid}/cancelamento")]public Task<IActionResult> Cancel(Guid id,ProductionStateCommand c,CancellationToken ct)=>Mutation(service.StateAsync(id,"cancelamento",c,Key,Actor,ct));
    [HttpPost("{id:guid}/inicio")]public Task<IActionResult> Start(Guid id,StartCommand c,CancellationToken ct)=>Mutation(service.StartAsync(id,c,Key,Actor,ct));
    [HttpPost("{id:guid}/previas")]public async Task<IActionResult> Preview(Guid id,ProductionPreviewCommand c,CancellationToken ct)=>Ok(await service.PreviewAsync(id,c,Actor,ct));
    [HttpPost("{id:guid}/confirmacao")]public Task<IActionResult> Confirm(Guid id,ConfirmCommand c,CancellationToken ct)=>Mutation(service.ConfirmAsync(id,c,Key,Actor,false,ct));
    [HttpPost("{id:guid}/confirmacao-ocorrencia"),Authorize(Roles="Admin")]public Task<IActionResult> ConfirmOccurrence(Guid id,ConfirmCommand c,CancellationToken ct)=>Mutation(service.ConfirmAsync(id,c,Key,Actor,true,ct));
    [HttpPost("{id:guid}/aborto-sem-consumo"),Authorize(Roles="Admin")]public Task<IActionResult> Abort(Guid id,AbortCommand c,CancellationToken ct)=>Mutation(service.AbortAsync(id,c,Key,Actor,ct));
    [HttpPost("{id:guid}/reversao"),Authorize(Roles="Admin")]public Task<IActionResult> Reverse(Guid id,ReverseCommand c,CancellationToken ct)=>Mutation(service.ReverseAsync(id,c,Key,Actor,ct));
    [HttpPost("{id:guid}/ocorrencias")]public Task<IActionResult> Occurrence(Guid id,OccurrenceCommand c,CancellationToken ct)=>Mutation(service.OccurrenceAsync(id,c,Key,Actor,ct));
    [HttpGet("{id:guid}/historico")]public async Task<IActionResult> History(Guid id,int page=1,int pageSize=25,CancellationToken ct=default)=>Ok(await service.HistoryAsync(id,page,pageSize,Actor,ct));
    [HttpGet("{id:guid}/ocorrencias")]public async Task<IActionResult> Occurrences(Guid id,int page=1,int pageSize=25,CancellationToken ct=default)=>Ok(await service.OccurrencesAsync(id,page,pageSize,Actor,ct));
    [HttpGet("{id:guid}/comandos/{operacao}/{chave}")]public Task<IActionResult> Command(Guid id,string operacao,string chave,CancellationToken ct)=>Mutation(service.CommandAsync(id,operacao,chave,Actor,ct));
    [HttpGet("reconciliacao"),Authorize(Roles="Admin")]public async Task<IActionResult> Reconcile([FromQuery]ProductionQuery q,CancellationToken ct)=>Ok(await service.ReconcileAsync(q,Actor,ct));
    [HttpGet("/api/v1/estoque/lotes/{id:guid}/genealogia")]public async Task<IActionResult> Genealogy(Guid id,[FromQuery]GenealogyQuery q,CancellationToken ct)=>Ok(await service.GenealogyAsync(id,q,Actor,ct));
}
