using System.Security.Claims;
using GenSW.Application.MaterialCosts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GenSW.API.Controllers;

[ApiController,Authorize,MaterialCosts,Route("api/v1/custos-materiais")]
public sealed class MaterialCostsController(MaterialCostService service):ControllerBase
{
    private Guid Actor=>Guid.Parse(User.FindFirstValue("sub")??User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string Key=>Request.Headers["Idempotency-Key"].ToString();
    private async Task<IActionResult> Mutation(Task<MaterialMutationResult> action)
    {var result=await action;if(result.Location is not null)Response.Headers.Location=result.Location;if(result.Replayed)Response.Headers["Idempotency-Replayed"]="true";return new ContentResult{StatusCode=result.StatusHttp,ContentType="application/json; charset=utf-8",Content=result.RespostaJson};}
    [HttpGet("referencias")]public async Task<IActionResult> References([FromQuery]MaterialCostQuery q,CancellationToken ct)=>Ok(await service.ReferencesAsync(q,Actor,ct));
    [HttpGet("referencias/{id:guid}")]public async Task<IActionResult> Reference(Guid id,CancellationToken ct)=>Ok(await service.ReferenceAsync(id,Actor,ct));
    [HttpGet("referencias/{id:guid}/historico")]public async Task<IActionResult> ReferenceHistory(Guid id,[FromQuery]MaterialCostQuery q,CancellationToken ct)=>Ok(await service.ReferenceHistoryAsync(id,q,Actor,ct));
    [HttpGet("evidencias/{id:guid}")]public async Task<IActionResult> Evidence(Guid id,CancellationToken ct)=>Ok(await service.EvidenceAsync(id,Actor,ct));
    [HttpPost("referencias"),Authorize(Roles="Admin")]public Task<IActionResult> Publish(MaterialReferenceCommand c,CancellationToken ct)=>Mutation(service.PublishReferenceAsync(c,Key,Actor,ct));
    [HttpPost("referencias/{id:guid}/substituicoes"),Authorize(Roles="Admin")]public Task<IActionResult> Replace(Guid id,MaterialReferenceCommand c,CancellationToken ct)=>Mutation(service.PublishReferenceAsync(c,Key,Actor,ct,id));
    [HttpGet("lotes/{id:guid}")]public async Task<IActionResult> Lot(Guid id,[FromQuery]MaterialCostQuery q,CancellationToken ct)=>Ok(await service.LotAsync(id,q,Actor,ct));
    [HttpGet("lotes/{id:guid}/historico")]public async Task<IActionResult> History(Guid id,[FromQuery]MaterialCostQuery q,CancellationToken ct)=>Ok(await service.LotHistoryAsync(id,q,Actor,ct));
    [HttpGet("execucoes/{id:guid}")]public async Task<IActionResult> Execution(Guid id,[FromQuery]MaterialCostQuery q,CancellationToken ct)=>Ok(await service.ExecutionAsync(id,q,Actor,ct));
    [HttpPost("execucoes/{id:guid}/previas-resolucao"),Authorize(Roles="Admin")]public async Task<IActionResult> PreviewResolution(Guid id,MaterialResolutionCommand c,CancellationToken ct)=>Ok(await service.PreviewResolutionAsync(id,c,Actor,ct));
    [HttpPost("execucoes/{id:guid}/resolucoes"),Authorize(Roles="Admin")]public Task<IActionResult> Resolve(Guid id,MaterialResolutionCommand c,CancellationToken ct)=>Mutation(service.ResolveAsync(id,c,Key,Actor,ct));
    [HttpPost("avaliacoes"),Authorize(Roles="Admin")]public Task<IActionResult> Evaluate(MaterialEvaluationCommand c,CancellationToken ct)=>Mutation(service.EvaluateAsync(c,Key,Actor,ct));
    [HttpGet("avaliacoes")]public async Task<IActionResult> Evaluations([FromQuery]MaterialCostQuery q,CancellationToken ct)=>Ok(await service.EvaluationsAsync(q,Actor,ct));
    [HttpGet("avaliacoes/{id:guid}")]public async Task<IActionResult> Evaluation(Guid id,CancellationToken ct)=>Ok(await service.EvaluationAsync(id,Actor,ct));
    [HttpPost("avaliacoes/{id:guid}/compensacoes"),Authorize(Roles="Admin")]public Task<IActionResult> Compensate(Guid id,MaterialCompensationCommand c,CancellationToken ct)=>Mutation(service.CompensateAsync(id,c,Key,Actor,ct));
    [HttpPost("estimativas")]public Task<IActionResult> Estimate(MaterialEstimateCommand c,CancellationToken ct)=>Mutation(service.EstimateAsync(c,Key,Actor,ct));
    [HttpGet("estimativas/{id:guid}")]public async Task<IActionResult> Estimate(Guid id,CancellationToken ct)=>Content(await service.EstimateAsync(id,Actor,ct),"application/json; charset=utf-8");
    [HttpGet("reconciliacao"),Authorize(Roles="Admin")]public async Task<IActionResult> Reconcile([FromQuery]MaterialCostQuery q,CancellationToken ct)=>Ok(await service.ReconcileAsync(q,Actor,ct));
    [HttpGet("comandos/{resource:guid}/{operation}/{key}")]public Task<IActionResult> Command(Guid resource,string operation,string key,CancellationToken ct)=>Mutation(service.CommandAsync(resource,operation,key,Actor,ct));
}
