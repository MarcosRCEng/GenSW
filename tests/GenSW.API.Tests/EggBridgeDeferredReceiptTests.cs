using System.Data;
using GenSW.Application.EggBridge;
using GenSW.Domain.EggBridge;
using GenSW.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GenSW.API.Tests;

public sealed partial class EggBridgeApiTests
{
    [Theory]
    [InlineData(IsolationLevel.ReadCommitted)]
    [InlineData(IsolationLevel.RepeatableRead)]
    public async Task F03_11_18_sql_cannot_add_manual_receipt_before_dedicated_origin_marker(IsolationLevel isolation)
    {
        var (client, user) = await Session(true); using (client)
        {
            var material = await Stock(client, user.UserId); var eggs = await Eggs(1);
            var scope = await Scope(client, material, user.UserId, eggs);
            var before = await Snapshot();
            var failure = await Record.ExceptionAsync(() => Factory.ExecuteDbContextAsync(async db =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(isolation);
                var now = DateTimeOffset.UtcNow; var today = InventoryRules.Today(TimeProvider.System);
                var source = await db.ProducoesOvos.AsNoTracking().SingleAsync(x => x.Id == eggs[0].Id);
                var lot = await db.LotesMateriais.AsNoTracking().SingleAsync(x => x.Id == material.Lot.Id);
                var groupId = Guid.NewGuid(); var rootEvent = Guid.NewGuid(); var rootMovement = Guid.NewGuid();
                var quantity = new PhysicalQuantity("1", "un", "1", "1", "0", "un", null, null, null, false);
                var rootSnapshot = InventoryJson.Write(new { Previa = new { Quantidade = quantity }, F03 = new { GrupoId = groupId } });
                async Task<long> Sequence() => await db.Database.SqlQueryRaw<long>("SELECT nextval('\"SequenciaEstoque\"') AS \"Value\"").SingleAsync();
                var group = new GrupoPonteOvos(groupId, "Entrada", material.Item.Id, lot.Id, material.Local.Id,
                    scope.Id, null, rootEvent, rootMovement, 1, lot.Revisao, lot.Situacao, lot.Validade,
                    InventoryJson.Write(lot), user.UserId, user.UserId, Evidence, Evidence, now);
                var position = PosicaoEstoque.Create(lot.Id, material.Local.Id, "un"); position.SetBalance(1, 0);
                db.AddRange(position,
                    new EventoEstoque(rootEvent, await Sequence(), "Entrada", user.UserId, user.UserId, now, today,
                        today, Evidence, Evidence, null, EggBridgeService.Algorithm, rootSnapshot),
                    new MovimentoEstoque(rootMovement, rootEvent, 1, lot.Id, material.Local.Id, material.Item.Id,
                        "un", "Entrada", 1, "1", "un", "0", 0, 1, null, null, rootSnapshot, "Entrada"),
                    group,
                    new PonteProducaoOvo(Guid.NewGuid(), source.Id, groupId, EggBridgeService.SourceSnapshot(source),
                        EggBridgeService.SourceHash(source), source.UpdatedAtUtc, EggBridgeService.Algorithm),
                    new ComandoPonteOvos(Guid.NewGuid(), "Entrada", Unique(), new string('a', 64), user.UserId,
                        groupId, false, 201, Root + "grupos/" + groupId, InventoryJson.Write(new { Grupo = group }), now));
                await db.SaveChangesAsync();
                var laterScope = scope with { Id = Guid.NewGuid(), RevisaoAncora = 1, OrdemAprovacao = 0, CreatedAtUtc = now };
                db.Add(laterScope); await db.SaveChangesAsync();
                var manualEvent = Guid.NewGuid();
                var manualSnapshot = InventoryJson.Write(new
                {
                    Previa = new { Quantidade = quantity },
                    Pedido = new { ConciliacaoOvosId = laterScope.Id, OrigemOvosComprovada = "ColetivaDistinta", Evidencia = Evidence }
                });
                db.AddRange(new EventoEstoque(manualEvent, await Sequence(), "Entrada", user.UserId, user.UserId, now,
                        today, today, Evidence, Evidence, null, InventoryRules.Algorithm, manualSnapshot),
                    new MovimentoEstoque(Guid.NewGuid(), manualEvent, 1, lot.Id, material.Local.Id, material.Item.Id,
                        "un", "Entrada", 1, "1", "un", "0", 1, 2, null, null, manualSnapshot, "Entrada"),
                    new OrigemRecebimentoOvos(Guid.NewGuid(), manualEvent, material.Item.Id, laterScope.Id, Evidence, user.UserId, now));
                await db.SaveChangesAsync();
                var debitEvent = Guid.NewGuid();
                var debitSnapshot = InventoryJson.Write(new { Previa = new { Quantidade = quantity } });
                db.AddRange(new EventoEstoque(debitEvent, await Sequence(), "SaidaManual", user.UserId, user.UserId,
                        now, today, today, Evidence, Evidence, null, InventoryRules.Algorithm, debitSnapshot),
                    new MovimentoEstoque(Guid.NewGuid(), debitEvent, 1, lot.Id, material.Local.Id, material.Item.Id,
                        "un", "Saida", 1, "1", "un", "0", 2, 1, null, null, debitSnapshot, "SaidaManual"),
                    new OrigemLoteOvos(Guid.NewGuid(), lot.Id, groupId));
                await db.SaveChangesAsync();
                // All movement, snapshot, quantity and projection guards are active. The extra
                // receipt and debit leave a coherent net balance, so only the deferred F03
                // dedicated-origin check can reject this insertion order at commit.
                await tx.CommitAsync();
            }));
            Assert.NotNull(failure);
            Assert.Equal("23514", PostgresFailure(failure!)?.SqlState);
            Assert.Equal(before, await Snapshot());
        }
    }
}
