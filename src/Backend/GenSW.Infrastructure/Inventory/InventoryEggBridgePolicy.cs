using GenSW.Application.EggBridge;
using GenSW.Application.Inventory;
using GenSW.Domain.EggBridge;
using GenSW.Domain.Inventory;
using Microsoft.EntityFrameworkCore;

namespace GenSW.Infrastructure.Inventory;

public sealed partial class InventoryRepository : IInventoryEggBridgePolicy
{
    public async Task LockItemAsync(Guid item, bool forUpdate, CancellationToken ct)
    {
        if (forUpdate && Postgres)
            await db.Set<AncoraConciliacaoOvos>().FromSqlInterpolated($"SELECT * FROM \"AncorasConciliacaoOvos\" WHERE \"ItemId\"={item} FOR UPDATE").AsNoTracking().ToArrayAsync(ct);
    }
    public async Task ValidateAsync(Guid item, Guid lot, string operation, bool credit, InventoryMovementCommand command, CancellationToken ct)
    {
        var origin = await db.Set<OrigemLoteOvos>().AsNoTracking().SingleOrDefaultAsync(x => x.LoteId == lot, ct);
        if (origin is not null)
        {
            if (operation is "Entrada" or "Abertura") throw new InventoryException(409, "lote_f03_sem_recebimento", "Lote dedicado F03 não admite recebimento adicional.");
            if (operation == "Ajuste" && command.GrupoPonteOvosReferenciaId != origin.GrupoId)
                throw new InventoryException(409, "ajuste_f03_referencia", "Ajuste presente do lote F03 exige referência estruturada ao grupo original.");
        }
        var hasGroup = origin is not null || await db.Set<GrupoPonteOvos>().AnyAsync(x => x.LoteId == lot, ct);
        if (command.GrupoPonteOvosReferenciaId is { } groupId)
        {
            if (operation != "Ajuste" || !await db.Set<GrupoPonteOvos>().AnyAsync(x => x.Id == groupId && x.LoteId == lot && x.ItemId == item, ct))
                throw new ArgumentException("Referência de grupo F03 não corresponde ao ajuste deste lote.");
        }
        else if (hasGroup && operation == "Ajuste")
            throw new InventoryException(409, "ajuste_f03_referencia", "Ajuste presente de lote com histórico F03 exige referência estruturada ao grupo.");
        if (!credit || operation is not ("Entrada" or "Abertura" or "Ajuste")) return;
        var anchor = await db.Set<AncoraConciliacaoOvos>().AsNoTracking().SingleOrDefaultAsync(x => x.ItemId == item, ct);
        if (anchor is null)
        {
            if (command.ConciliacaoOvosId.HasValue || command.OrigemOvosComprovada is not null) throw new ArgumentException("Protocolo F03 exige inscrição explícita do item.");
            return;
        }
        var expectedOrigin = command.GrupoPonteOvosReferenciaId.HasValue ? "AjustePresenteVinculado" : "ColetivaDistinta";
        if (command.OrigemOvosComprovada != expectedOrigin || string.IsNullOrWhiteSpace(command.Evidencia) || command.Evidencia.Length > 2000 || command.ConciliacaoOvosId is not { } scopeId)
            throw new InventoryException(409, "origem_ovos_pendente", "Item gerido pela F03 exige origem comprovada, evidência e conciliação atual. Para indivíduos use a ponte atômica.");
        var scope = await db.Set<RecorteConciliacaoOvos>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == scopeId, ct);
        if (scope is null || scope.ItemId != item || scope.RevisaoAncora != anchor.Revisao)
            throw new InventoryException(409, "conciliacao_ovos_obsoleta", "Conferência F03 obsoleta; refaça a conciliação antes do recebimento.");
    }
    public async Task RecordReceiptAsync(Guid item, EventoEstoque stockEvent, InventoryMovementCommand command, CancellationToken ct)
    {
        if (command.ConciliacaoOvosId is { } scope && await db.Set<AncoraConciliacaoOvos>().AnyAsync(x => x.ItemId == item, ct))
            db.Add(new OrigemRecebimentoOvos(Guid.NewGuid(), stockEvent.Id, item, scope, command.Evidencia!, stockEvent.AutorId, stockEvent.CreatedAtUtc));
    }
}
