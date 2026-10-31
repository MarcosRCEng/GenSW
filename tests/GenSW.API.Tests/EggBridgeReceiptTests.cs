using System.Net;
using GenSW.Application.EggBridge;
using GenSW.Application.Inventory;
using GenSW.Domain.EggBridge;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GenSW.API.Tests;

public sealed partial class EggBridgeApiTests
{
    private async Task<Material> AnotherDestination(HttpClient client, Guid actor, Material original)
    {
        var local = await Success<LocalView>(await Post(client, "locais", new LocalCommand(Unique(), "Outro local sintético F03"), inventory: true));
        var lot = await Success<LoteView>(await Post(client, "lotes", new LoteCommand(original.Item.Id, Unique(), "Origem coletiva distinta sintética", Evidence, actor, ItemVersaoEsperada: original.Item.Revisao), inventory: true));
        return new(original.Item, lot, local);
    }
    private static InventoryMovementCommand DistinctReceipt(Material m, Guid actor, Guid scope) => Movement(m, actor, "5", 0) with
    { ConciliacaoOvosId = scope, OrigemOvosComprovada = "ColetivaDistinta" };

    [Fact]
    public async Task F03_10_manual_receipt_in_other_lot_and_local_invalidates_prior_preview_and_requires_new_conference()
    {
        var (client, user) = await Session(true); using (client)
        {
            var m = await Stock(client, user.UserId); var other = await AnotherDestination(client, user.UserId, m); var eggs = await Eggs(1);
            var scope = await Scope(client, m, user.UserId, eggs); var preview = await Preview(client, Entry(m, scope.Id, user.UserId, eggs));
            var before = await Snapshot();
            await Error(await Post(client, "entradas", Movement(other, user.UserId, "5", 0), inventory: true), HttpStatusCode.Conflict);
            Assert.Equal(before, await Snapshot());
            await Success<System.Text.Json.JsonElement>(await Post(client, "entradas", DistinctReceipt(other, user.UserId, scope.Id), inventory: true));
            Assert.Equal("5", (await Balance(client, other))!.Quantidade);
            await Error(await Post(client, "confirmacoes", new EggConfirmCommand(preview.Token)), HttpStatusCode.Conflict);
            var renewed = await Scope(client, m, user.UserId, eggs, enroll: false);
            await Confirm(client, await Preview(client, Entry(m, renewed.Id, user.UserId, eggs)));
            Assert.Equal("1", (await Balance(client, m))!.Quantidade);
        }
    }

    [Fact]
    public async Task F03_10_bridge_credit_first_rejects_sequential_manual_receipt_using_old_conference()
    {
        var (client, user) = await Session(true); using (client)
        {
            var m = await Stock(client, user.UserId); var other = await AnotherDestination(client, user.UserId, m); var eggs = await Eggs(1);
            var scope = await Scope(client, m, user.UserId, eggs); var manual = DistinctReceipt(other, user.UserId, scope.Id);
            await Confirm(client, await Preview(client, Entry(m, scope.Id, user.UserId, eggs)));
            var before = await Snapshot();
            await Error(await Post(client, "entradas", manual, inventory: true), HttpStatusCode.Conflict);
            Assert.Equal(before, await Snapshot()); Assert.Equal("1", (await Balance(client, m))!.Quantidade);
        }
    }

    [Fact]
    public async Task F03_10_bridge_and_manual_receipt_race_revalidate_same_item_anchor_across_destinations()
    {
        var (client, user) = await Session(true); using (client)
        {
            var m = await Stock(client, user.UserId); var other = await AnotherDestination(client, user.UserId, m); var eggs = await Eggs(1);
            var scope = await Scope(client, m, user.UserId, eggs); var preview = await Preview(client, Entry(m, scope.Id, user.UserId, eggs));
            var replies = await Race(() => Post(client, "confirmacoes", new EggConfirmCommand(preview.Token)),
                () => Post(client, "entradas", DistinctReceipt(other, user.UserId, scope.Id), inventory: true));
            try
            {
                Assert.Single(replies, x => x.StatusCode == HttpStatusCode.Created);
                Assert.Single(replies, x => x.StatusCode == HttpStatusCode.Conflict);
                var anchor = await Factory.ExecuteDbContextAsync(db => db.Set<AncoraConciliacaoOvos>().SingleAsync(x => x.ItemId == m.Item.Id));
                Assert.Equal(1, anchor.Revisao);
                Assert.Equal(1, await Factory.ExecuteDbContextAsync(db => db.MovimentosEstoque.CountAsync(x => x.ItemId == m.Item.Id)));
            }
            finally { foreach (var reply in replies) reply.Dispose(); }
        }
    }
}
