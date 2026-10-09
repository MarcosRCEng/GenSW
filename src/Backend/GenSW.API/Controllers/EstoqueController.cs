using System.Security.Claims;
using GenSW.Application.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GenSW.API.Controllers;

[ApiController, Authorize, Inventory, Route("api/v1/estoque")]
public sealed class EstoqueController(InventoryService service) : ControllerBase
{
    private Guid Actor => Guid.Parse(User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string Key => Request.Headers["Idempotency-Key"].ToString();

    // The stored bytes, status and Location are also the response on replay.
    private async Task<IActionResult> Mutation(Task<InventoryMutationResult> action)
    {
        var result = await action;
        if (result.Location is not null) Response.Headers.Location = result.Location;
        if (result.Replayed) Response.Headers["Idempotency-Replayed"] = "true";
        return new ContentResult { StatusCode = result.StatusHttp, ContentType = "application/json; charset=utf-8", Content = result.RespostaJson };
    }

    [HttpGet("locais")]
    public async Task<IActionResult> Locais([FromQuery] InventoryQuery query, CancellationToken ct) => Ok(await service.LocaisAsync(query, Actor, ct));
    [HttpGet("locais/{id:guid}")]
    public async Task<IActionResult> Local(Guid id, CancellationToken ct) => Ok(await service.GetLocalAsync(id, Actor, ct));
    [HttpPost("locais")]
    public Task<IActionResult> CreateLocal(LocalCommand command, CancellationToken ct) => Mutation(service.CreateLocalAsync(command, Key, Actor, ct));
    [HttpPut("locais/{id:guid}")]
    public Task<IActionResult> UpdateLocal(Guid id, LocalCommand command, CancellationToken ct) => Mutation(service.UpdateLocalAsync(id, command, Key, Actor, ct));
    [HttpPatch("locais/{id:guid}/ativo"), Authorize(Roles = "Admin")]
    public Task<IActionResult> LocalAtivo(Guid id, InventoryStateCommand command, CancellationToken ct) => Mutation(service.LocalStateAsync(id, "Ativo", command, Key, Actor, ct));
    [HttpPost("locais/{id:guid}/finalidade"), Authorize(Roles = "Admin")]
    public Task<IActionResult> LocalFinalidade(Guid id, InventoryStateCommand command, CancellationToken ct) => Mutation(service.LocalStateAsync(id, "Finalidade", command, Key, Actor, ct));
    [HttpGet("locais/{id:guid}/historico")]
    public async Task<IActionResult> LocalHistory(Guid id, [FromQuery] InventoryQuery query, CancellationToken ct) => Ok(await service.HistoryAsync(id, query, Actor, ct));

    [HttpGet("lotes")]
    public async Task<IActionResult> Lotes([FromQuery] InventoryQuery query, CancellationToken ct) => Ok(await service.LotesAsync(query, Actor, ct));
    [HttpGet("lotes/{id:guid}")]
    public async Task<IActionResult> Lote(Guid id, CancellationToken ct) => Ok(await service.GetLoteAsync(id, Actor, ct));
    [HttpPost("lotes")]
    public Task<IActionResult> CreateLote(LoteCommand command, CancellationToken ct) => Mutation(service.CreateLoteAsync(command, Key, Actor, ct));
    [HttpPut("lotes/{id:guid}")]
    public Task<IActionResult> UpdateLote(Guid id, LoteCommand command, CancellationToken ct) => Mutation(service.UpdateLoteAsync(id, command, Key, Actor, ct));
    [HttpPatch("lotes/{id:guid}/ativo"), Authorize(Roles = "Admin")]
    public Task<IActionResult> LoteAtivo(Guid id, InventoryStateCommand command, CancellationToken ct) => LoteState(id, "Ativo", command, ct);
    [HttpPost("lotes/{id:guid}/bloqueio"), Authorize(Roles = "Admin")]
    public Task<IActionResult> Bloqueio(Guid id, InventoryStateCommand command, CancellationToken ct) => LoteState(id, "Bloqueio", command, ct);
    [HttpPost("lotes/{id:guid}/liberacao"), Authorize(Roles = "Admin")]
    public Task<IActionResult> Liberacao(Guid id, InventoryStateCommand command, CancellationToken ct) => LoteState(id, "Liberacao", command, ct);
    [HttpPost("lotes/{id:guid}/encerramento"), Authorize(Roles = "Admin")]
    public Task<IActionResult> Encerramento(Guid id, InventoryStateCommand command, CancellationToken ct) => LoteState(id, "Encerramento", command, ct);
    [HttpPost("lotes/{id:guid}/validade"), Authorize(Roles = "Admin")]
    public Task<IActionResult> Validade(Guid id, InventoryStateCommand command, CancellationToken ct) => LoteState(id, "Validade", command, ct);
    [HttpPost("lotes/{id:guid}/referencias"), Authorize(Roles = "Admin")]
    public Task<IActionResult> Referencias(Guid id, InventoryStateCommand command, CancellationToken ct) => LoteState(id, "Referencias", command, ct);
    private Task<IActionResult> LoteState(Guid id, string operation, InventoryStateCommand command, CancellationToken ct) => Mutation(service.LoteStateAsync(id, operation, command, Key, Actor, ct));
    [HttpGet("lotes/{id:guid}/historico")]
    public async Task<IActionResult> LoteHistory(Guid id, [FromQuery] InventoryQuery query, CancellationToken ct) => Ok(await service.HistoryAsync(id, query, Actor, ct));

    [HttpGet("saldos")]
    public async Task<IActionResult> Saldos([FromQuery] InventoryQuery query, CancellationToken ct) => Ok(await service.SaldosAsync(query, Actor, ct));
    [HttpGet("saldos/{loteId:guid}/{localId:guid}")]
    public async Task<IActionResult> Saldo(Guid loteId, Guid localId, CancellationToken ct) => Ok(await service.GetSaldoAsync(loteId, localId, Actor, ct));
    [HttpGet("movimentos")]
    public async Task<IActionResult> Movimentos([FromQuery] InventoryQuery query, CancellationToken ct) => Ok(await service.EventosAsync(query, Actor, ct));
    [HttpGet("movimentos/{id:guid}")]
    public async Task<IActionResult> Movimento(Guid id, CancellationToken ct) => Ok(await service.GetEventoAsync(id, Actor, ct));
    [HttpGet("responsaveis")]
    public async Task<IActionResult> Responsaveis([FromQuery] InventoryQuery query, CancellationToken ct) => Ok(await service.ResponsaveisAsync(query, Actor, ct));
    [HttpGet("responsaveis/{id:guid}")]
    public async Task<IActionResult> Responsavel(Guid id, CancellationToken ct) => Ok(await service.GetResponsavelAsync(id, Actor, ct));
    [HttpGet("conversoes/{id:guid}")]
    public async Task<IActionResult> Conversao(Guid id, CancellationToken ct) => Ok(await service.GetConversionAsync(id, Actor, ct));
    [HttpGet("reconciliacao")]
    public async Task<IActionResult> Reconciliacao([FromQuery] InventoryQuery query, CancellationToken ct) => Ok(await service.ReconcileAsync(query, Actor, ct));
    [HttpPost("previas")]
    public async Task<IActionResult> Preview(InventoryPreviewCommand command, CancellationToken ct) => Ok(await service.PreviewAsync(command, Actor, ct));

    [HttpPost("aberturas"), Authorize(Roles = "Admin")]
    public Task<IActionResult> Abertura(InventoryMovementCommand command, CancellationToken ct) => Move("Abertura", command, ct);
    [HttpPost("entradas")]
    public Task<IActionResult> Entrada(InventoryMovementCommand command, CancellationToken ct) => Move("Entrada", command, ct);
    [HttpPost("transferencias")]
    public Task<IActionResult> Transferencia(InventoryMovementCommand command, CancellationToken ct) => Move("Transferencia", command, ct);
    [HttpPost("saidas-manuais")]
    public Task<IActionResult> SaidaManual(InventoryMovementCommand command, CancellationToken ct) => Move("SaidaManual", command, ct);
    [HttpPost("consumos-internos")]
    public Task<IActionResult> ConsumoInterno(InventoryMovementCommand command, CancellationToken ct) => Move("ConsumoInterno", command, ct);
    [HttpPost("ajustes"), Authorize(Roles = "Admin")]
    public Task<IActionResult> Ajuste(InventoryMovementCommand command, CancellationToken ct) => Move("Ajuste", command, ct);
    [HttpPost("segregacoes"), Authorize(Roles = "Admin")]
    public Task<IActionResult> Segregacao(InventoryMovementCommand command, CancellationToken ct) => Move("Segregacao", command, ct);
    [HttpPost("retornos-segregacao"), Authorize(Roles = "Admin")]
    public Task<IActionResult> Retorno(InventoryMovementCommand command, CancellationToken ct) => Move("RetornoSegregacao", command, ct);
    [HttpPost("descartes"), Authorize(Roles = "Admin")]
    public Task<IActionResult> Descarte(InventoryMovementCommand command, CancellationToken ct) => Move("Descarte", command, ct);
    private Task<IActionResult> Move(string operation, InventoryMovementCommand command, CancellationToken ct) => Mutation(service.MoveAsync(operation, command, Key, Actor, ct));
}
