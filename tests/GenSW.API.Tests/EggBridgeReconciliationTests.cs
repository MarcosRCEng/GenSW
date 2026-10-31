using System.Net;
using System.Net.Http.Json;
using GenSW.Application.EggBridge;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GenSW.API.Tests;

public sealed partial class EggBridgeApiTests
{
    [Fact]
    public async Task F03_19_isolated_corruption_is_reported_without_repair_and_blocks_affected_treatment()
    {
        var (client, user) = await Session(true); using (client)
        {
            var m = await Stock(client, user.UserId); var eggs = await Eggs(1); var scope = await Scope(client, m, user.UserId, eggs);
            var group = await Confirm(client, await Preview(client, Entry(m, scope.Id, user.UserId, eggs)));
            // Deliberately corrupt only this newly initialized fixture; restore
            // the original projection in finally, retaining every real guard.
            await Factory.ExecuteDbContextAsync(db => db.Database.ExecuteSqlInterpolatedAsync($"ALTER TABLE \"PosicoesEstoque\" DISABLE TRIGGER ALL; UPDATE \"PosicoesEstoque\" SET \"Quantidade\"=2 WHERE \"LoteId\"={m.Lot.Id}; ALTER TABLE \"PosicoesEstoque\" ENABLE TRIGGER ALL;"));
            try
            {
                var before = await Snapshot();
                var detail = (await client.GetFromJsonAsync<EggBridgeDetail>(Root + "grupos/" + group.Grupo.Id))!;
                Assert.False(detail.Conciliacao.Coerente); Assert.NotEmpty(detail.Conciliacao.Divergencias);
                Assert.Equal(before, await Snapshot());
                var preview = await Success<EggTreatmentPreview>(await Post(client, $"grupos/{group.Grupo.Id}/previas-tratamento", new EggTreatmentCommand("Compensacao", user.UserId, Evidence, Evidence, true)));
                Assert.False(preview.PodeConfirmar);
                await Error(await Post(client, $"grupos/{group.Grupo.Id}/tratamentos", new EggConfirmCommand(preview.Token)), HttpStatusCode.Conflict);
                Assert.Equal(before, await Snapshot());
            }
            finally
            {
                await Factory.ExecuteDbContextAsync(db => db.Database.ExecuteSqlInterpolatedAsync($"ALTER TABLE \"PosicoesEstoque\" DISABLE TRIGGER ALL; UPDATE \"PosicoesEstoque\" SET \"Quantidade\"=1 WHERE \"LoteId\"={m.Lot.Id}; ALTER TABLE \"PosicoesEstoque\" ENABLE TRIGGER ALL;"));
            }
        }
    }
}
