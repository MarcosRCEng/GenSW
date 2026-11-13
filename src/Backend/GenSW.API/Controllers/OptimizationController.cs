using System.Security.Claims;
using GenSW.Application.Formulation;
using GenSW.Application.Optimization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GenSW.API.Controllers;

[ApiController, Authorize, Formulation, Route("api/v1/otimizacoes-formulacao")]
public sealed class OptimizationController(OptimizationService service) : ControllerBase
{
    private Guid Actor => Guid.Parse(User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string Key => Request.Headers["Idempotency-Key"].ToString();

    [HttpPost]
    public async Task<IActionResult> Execute(OptimizationCommand command, CancellationToken ct)
    {
        try
        {
            var result = await service.ExecuteAsync(command, Key, Actor, ct);
            if (result.Estado == "EmCalculo")
            {
                Response.Headers.RetryAfter = "1";
                return AcceptedAtAction(nameof(Get), new { id = result.Id }, result);
            }
            return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
        }
        catch (FormulationException exception) when (exception.Status == 429)
        {
            Response.Headers.RetryAfter = "1";
            throw;
        }
    }

    [HttpPost("previas")]
    public async Task<IActionResult> Preview(OptimizationCommand command, CancellationToken ct) =>
        Ok(await service.PreviewAsync(command, Actor, ct));

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] OptimizationQuery query, CancellationToken ct) =>
        Ok(await service.ListAsync(query, Actor, ct));

    [HttpGet("por-chave")]
    public async Task<IActionResult> ByKey([FromQuery] string chave, CancellationToken ct) =>
        Ok(await service.GetByKeyAsync(chave, Actor, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(await service.GetAsync(id, Actor, ct));

    [HttpPost("{id:guid}/previas-rascunho")]
    public async Task<IActionResult> PreviewDraft(Guid id, OptimizationDraftDestination destination, CancellationToken ct) =>
        Ok(await service.PreviewDraftAsync(id, destination, Actor, ct));

    [HttpPost("{id:guid}/rascunhos")]
    public async Task<IActionResult> CreateDraft(Guid id, OptimizationDraftCommand command, CancellationToken ct)
    {
        var result = await service.CreateDraftAsync(id, command, Key, Actor, ct);
        return CreatedAtAction(nameof(DraftByVersion), new { versaoId = result.VersaoId }, result);
    }

    [HttpGet("{id:guid}/rascunhos/por-chave")]
    public async Task<IActionResult> DraftByKey(Guid id, [FromQuery] string chave, CancellationToken ct) =>
        Ok(await service.GetDraftByKeyAsync(id, chave, Actor, ct));

    [HttpGet("rascunhos/por-versao/{versaoId:guid}")]
    public async Task<IActionResult> DraftByVersion(Guid versaoId, CancellationToken ct) =>
        Ok(await service.GetDraftByVersionAsync(versaoId, Actor, ct));
}
