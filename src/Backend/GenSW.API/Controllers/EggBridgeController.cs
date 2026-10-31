using System.Security.Claims;
using GenSW.Application.EggBridge;
using GenSW.Domain.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GenSW.API.Controllers;

[ApiController, Authorize, EggBridge, Route("api/v1/producao/ponte-ovos")]
public sealed class EggBridgeController(EggBridgeService service) : ControllerBase
{
    private Guid Actor => Guid.Parse(User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string Key => Request.Headers["Idempotency-Key"].ToString();
    private static JsonResult Data(object value) => new(value, InventoryJson.Options);
    private async Task<IActionResult> Mutation(Task<EggMutationResult> action)
    {
        var result = await action;
        if (result.Location is not null) Response.Headers.Location = result.Location;
        if (result.Replayed) Response.Headers["Idempotency-Replayed"] = "true";
        return new ContentResult { StatusCode = result.StatusHttp, ContentType = "application/json; charset=utf-8", Content = result.RespostaJson };
    }
    [HttpGet("candidatos")]
    public async Task<IActionResult> Candidates([FromQuery] EggBridgeQuery q, CancellationToken ct) => Data(await service.CandidatesAsync(q, Actor, ct));
    [HttpGet("grupos")]
    public async Task<IActionResult> Groups([FromQuery] EggBridgeQuery q, CancellationToken ct) => Data(await service.GroupsAsync(q, Actor, ct));
    [HttpGet("grupos/{id:guid}")]
    public async Task<IActionResult> Group(Guid id, CancellationToken ct) => Data(await service.GetAsync(id, Actor, ct));
    [HttpGet("itens/{id:guid}")]
    public async Task<IActionResult> Enrollment(Guid id, CancellationToken ct) => Data(await service.EnrollmentAsync(id, Actor, ct));
    [HttpPost("itens"), Authorize(Roles = "Admin")]
    public Task<IActionResult> Enroll(EggEnrollmentCommand c, CancellationToken ct) => Mutation(service.EnrollAsync(c, Key, Actor, ct));
    [HttpGet("recortes")]
    public async Task<IActionResult> Scopes([FromQuery] EggBridgeQuery q, CancellationToken ct) => Data(await service.ScopesAsync(q, Actor, ct));
    [HttpGet("recortes/{id:guid}")]
    public async Task<IActionResult> Scope(Guid id, CancellationToken ct) => Data(await service.ScopeAsync(id, Actor, ct));
    [HttpPost("recortes"), Authorize(Roles = "Admin")]
    public Task<IActionResult> Scope(EggScopeCommand c, CancellationToken ct) => Mutation(service.ApproveScopeAsync(c, Key, Actor, ct));
    [HttpGet("provas")]
    public async Task<IActionResult> Proofs([FromQuery] EggBridgeQuery q, CancellationToken ct) => Data(await service.ProofsAsync(q, Actor, ct));
    [HttpGet("provas/{id:guid}")]
    public async Task<IActionResult> Proof(Guid id, CancellationToken ct) => Data(await service.ProofAsync(id, Actor, ct));
    [HttpPost("provas"), Authorize(Roles = "Admin")]
    public Task<IActionResult> Proof(EggProofCommand c, CancellationToken ct) => Mutation(service.ApproveProofAsync(c, Key, Actor, ct));
    [HttpPost("previas")]
    public async Task<IActionResult> Preview(EggPreviewCommand c, CancellationToken ct) => Data(await service.PreviewAsync(c, Actor, ct));
    [HttpPost("confirmacoes")]
    public Task<IActionResult> Confirm(EggConfirmCommand c, CancellationToken ct) => Mutation(service.ConfirmAsync(c, Key, Actor, ct));
    [HttpPost("grupos/{id:guid}/previas-tratamento"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> PreviewTreatment(Guid id, EggTreatmentCommand c, CancellationToken ct) => Data(await service.PreviewTreatmentAsync(id, c, Actor, ct));
    [HttpPost("grupos/{id:guid}/tratamentos"), Authorize(Roles = "Admin")]
    public Task<IActionResult> Treat(Guid id, EggConfirmCommand c, CancellationToken ct) => Mutation(service.TreatAsync(id, c, Key, Actor, ct));
    [HttpGet("comandos/{operacao}/{chave}")]
    public Task<IActionResult> Command(string operacao, string chave, CancellationToken ct) => Mutation(service.CommandAsync(operacao, chave, Actor, ct));
    [HttpGet("conciliacao"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> Reconcile([FromQuery] EggBridgeQuery q, CancellationToken ct) => Data(await service.ReconcileAsync(q, Actor, ct));
}
