using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GenSW.Application.Inventory;
using GenSW.Application.Production;
using GenSW.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GenSW.API.Tests;

public sealed partial class ProductionApiTests
{
    [Fact]
    public async Task T11_abort_requires_admin_and_explicit_no_consumption_releases_once_and_is_terminal()
    {
        var (admin, user) = await Session(true); var (ordinary, _) = await Session(); using (admin) using (ordinary)
        {
            var f = await ScenarioAsync(admin, user.UserId); var order = await Order(admin, user.UserId, f);
            order = await Success<OrderView>(await Post(admin, $"{order.Id}/inicio", Start(order, f, user.UserId)));
            var before = await Snapshot(); var command = new AbortCommand(order.Revisao, true, "Nenhum material consumido", Evidence);
            await Error(await Post(ordinary, $"{order.Id}/aborto-sem-consumo", command), HttpStatusCode.Forbidden);
            await Error(await Post(admin, $"{order.Id}/aborto-sem-consumo", command with { NenhumConsumoFisico = false }), HttpStatusCode.Conflict);
            Assert.Equal(before, await Snapshot());
            var key = Unique(); order = await Success<OrderView>(await Post(admin, $"{order.Id}/aborto-sem-consumo", command, key));
            Assert.Equal("AbortadaSemConsumo", order.Estado); ReservationBalances(order, 100, 0, 0, 100);
            var replay = await Success<OrderView>(await Post(admin, $"{order.Id}/aborto-sem-consumo", command, key)); Assert.Equal(order.Revisao, replay.Revisao);
            await Error(await Post(admin, $"{order.Id}/inicio", Start(order, f, user.UserId)), HttpStatusCode.Conflict);
            Assert.Equal("100", (await Balance(admin, f.Input))!.Quantidade);
        }
    }

    [Fact]
    public async Task T11_consumption_occurrence_prevents_abort_and_partial_loss_is_not_debited_twice()
    {
        var (client, user) = await Session(true); using (client)
        {
            var f = await ScenarioAsync(client, user.UserId); var order = await Order(client, user.UserId, f);
            order = await Success<OrderView>(await Post(client, $"{order.Id}/inicio", Start(order, f, user.UserId)));
            order = await Success<OrderView>(await Post(client, $"{order.Id}/ocorrencias", new OccurrenceCommand(order.Revisao, "Interrupcao", "Processo interrompido após consumo", Evidence, HouveConsumoFisico: true)));
            ReservationBalances(order, 100, 100, 0, 0); var before = await Snapshot();
            await Error(await Post(client, $"{order.Id}/aborto-sem-consumo", new AbortCommand(order.Revisao, true, "Tentativa incompatível com fato registrado", Evidence)), HttpStatusCode.Conflict);
            Assert.Equal(before, await Snapshot());
            order = await Success<OrderView>(await Post(client, $"{order.Id}/confirmacao", Confirm(order, f, user.UserId)));
            Assert.Equal("0", (await Balance(client, f.Input))!.Quantidade);
            var stock = await client.GetFromJsonAsync<InventoryPage<EventoView>>($"/api/v1/estoque/movimentos?loteId={f.Input.Lot.Id}");
            Assert.Single(stock!.Items, e => e.Tipo == "ProducaoConsumo");
            Assert.Equal("100", Assert.Single(stock.Items.Single(e => e.Tipo == "ProducaoConsumo").Movimentos).Quantidade);
        }
    }

    [Fact]
    public async Task T11_total_loss_requires_admin_evidence_and_creates_no_empty_lot()
    {
        var (admin, user) = await Session(true); var (ordinary, _) = await Session(); using (admin) using (ordinary)
        {
            var f = await ScenarioAsync(admin, user.UserId); var order = await Order(admin, user.UserId, f);
            order = await Success<OrderView>(await Post(admin, $"{order.Id}/inicio", Start(order, f, user.UserId)));
            var command = Confirm(order, f, user.UserId, output: "0", loss: "100"); var before = await Snapshot();
            await Error(await Post(ordinary, $"{order.Id}/confirmacao", command), HttpStatusCode.Forbidden);
            await Error(await Post(admin, $"{order.Id}/confirmacao", command with { Evidencia = null }), HttpStatusCode.BadRequest);
            Assert.Equal(before, await Snapshot());
            order = await Success<OrderView>(await Post(admin, $"{order.Id}/confirmacao", command));
            Assert.Equal("PerdaTotal", order.Execucao!.Resultado); Assert.Empty(order.Execucao.LotesSaida);
            Assert.Equal("0", (await Balance(admin, f.Input))!.Quantidade); ReservationBalances(order, 100, 0, 100, 0);
            Assert.False(await Factory.ExecuteDbContextAsync(db => db.LotesMateriais.AnyAsync(l => l.ItemId == f.Output.Id)));
        }
    }

    [Fact]
    public async Task T12_intact_reversal_is_additive_atomic_and_original_confirmation_can_replay_afterwards()
    {
        var (client, user) = await Session(true); using (client)
        {
            var f = await ScenarioAsync(client, user.UserId); var order = await Order(client, user.UserId, f);
            order = await Success<OrderView>(await Post(client, $"{order.Id}/inicio", Start(order, f, user.UserId)));
            var confirm = Confirm(order, f, user.UserId); var confirmKey = Unique();
            var original = await Success<OrderView>(await Post(client, $"{order.Id}/confirmacao", confirm, confirmKey));
            var execution = original.Execucao!; var output = Assert.Single(execution.LotesSaida);
            var command = new ReverseCommand(original.Revisao, true, "Lançamento incorreto; contagem física comprovada", Evidence); var key = Unique();
            order = await Success<OrderView>(await Post(client, $"{order.Id}/reversao", command, key));
            Assert.Equal("Revertida", order.Estado); Assert.Equal(execution.Id, order.Execucao!.Id); Assert.Equal("100", (await Balance(client, f.Input))!.Quantidade);
            var result = await client.GetFromJsonAsync<SaldoView>($"/api/v1/estoque/saldos/{output.LoteId}/{output.LocalId}"); Assert.Equal("0", result!.Quantidade);
            var before = await Snapshot();
            Assert.Equal("Revertida", (await Success<OrderView>(await Post(client, $"{order.Id}/reversao", command, key))).Estado);
            var replay = await Success<OrderView>(await Post(client, $"{order.Id}/confirmacao", confirm, confirmKey)); Assert.Equal("Confirmada", replay.Estado); Assert.Equal(execution.Id, replay.Execucao!.Id);
            Assert.Equal(before, await Snapshot());
            var events = await client.GetFromJsonAsync<InventoryPage<EventoView>>($"/api/v1/estoque/movimentos?loteId={f.Input.Lot.Id}");
            Assert.Contains(events!.Items, e => e.Tipo == "ReversaoProducaoConsumo"); Assert.Contains(events.Items, e => e.Tipo == "ProducaoConsumo");
        }
    }

    [Fact]
    public async Task T12_closed_input_blocks_reversal_before_any_output_is_debited()
    {
        var (client, user) = await Session(true); using (client)
        {
            var f = await ScenarioAsync(client, user.UserId); var order = await Order(client, user.UserId, f);
            order = await Success<OrderView>(await Post(client, $"{order.Id}/inicio", Start(order, f, user.UserId)));
            order = await Success<OrderView>(await Post(client, $"{order.Id}/confirmacao", Confirm(order, f, user.UserId)));
            await Success<LoteView>(await Post(client, $"lotes/{f.Input.Lot.Id}/encerramento", new InventoryStateCommand(f.Input.Lot.Revisao, "Fonte encerrada após uso", Evidence), inventory: true));
            var before = await Snapshot();
            await Error(await Post(client, $"{order.Id}/reversao", new ReverseCommand(order.Revisao, true, "Correção solicitada", Evidence)), HttpStatusCode.Conflict);
            Assert.Equal(before, await Snapshot());
        }
    }

    [Fact]
    public async Task T13_transfer_out_and_back_still_blocks_reversal_even_with_original_balance()
    {
        var (client, user) = await Session(true); using (client)
        {
            var f = await ScenarioAsync(client, user.UserId); var order = await Order(client, user.UserId, f);
            order = await Success<OrderView>(await Post(client, $"{order.Id}/inicio", Start(order, f, user.UserId)));
            order = await Success<OrderView>(await Post(client, $"{order.Id}/confirmacao", Confirm(order, f, user.UserId)));
            var output = Assert.Single(order.Execucao!.LotesSaida); var lot = (await client.GetFromJsonAsync<LoteView>($"/api/v1/estoque/lotes/{output.LoteId}"))!;
            var target = await Success<LocalView>(await Post(client, "locais", new LocalCommand(Unique(), "Destino de ida e volta"), inventory: true), HttpStatusCode.Created);
            var transfer = new InventoryMovementCommand(lot.Id, "98", "kg", user.UserId, "Transferência sintética", new(f.Output.Revisao, lot.Revisao, OrigemLocal: f.Destination.Revisao, DestinoLocal: target.Revisao, OrigemPosicao: 1), OrigemLocalId: output.LocalId, DestinoLocalId: target.Id, Evidencia: Evidence, Destino: "Local conferido");
            await Success<JsonElement>(await Post(client, "transferencias", transfer, inventory: true), HttpStatusCode.Created);
            await Success<JsonElement>(await Post(client, "transferencias", transfer with { OrigemLocalId = target.Id, DestinoLocalId = output.LocalId, VersoesEsperadas = new(f.Output.Revisao, lot.Revisao, OrigemLocal: target.Revisao, DestinoLocal: f.Destination.Revisao, OrigemPosicao: 1, DestinoPosicao: 2) }, inventory: true), HttpStatusCode.Created);
            var before = await Snapshot();
            await Error(await Post(client, $"{order.Id}/reversao", new ReverseCommand(order.Revisao, true, "Quantidade voltou ao local original", Evidence)), HttpStatusCode.Conflict);
            Assert.Equal(before, await Snapshot());
        }
    }

    [Fact]
    public async Task T20_authorization_is_checked_before_replay_and_inexistent_differs_from_ineligible()
    {
        using var anonymous = Factory.CreateHttpsClient(); Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/ordens-producao")).StatusCode);
        var (client, user) = await Session(true); var (ordinary, _) = await Session(); using (client) using (ordinary)
        {
            var f = await ScenarioAsync(client, user.UserId); var order = await Order(client, user.UserId, f);
            order = await Success<OrderView>(await Post(client, $"{order.Id}/inicio", Start(order, f, user.UserId)));
            var command = new AbortCommand(order.Revisao, true, "Sem consumo confirmado", Evidence); var key = Unique();
            await Success<OrderView>(await Post(client, $"{order.Id}/aborto-sem-consumo", command, key));
            var before = await Snapshot();
            await Factory.ExecuteScopeAsync(async s =>
            {
                var users = s.GetRequiredService<UserManager<ApplicationUser>>(); var current = await users.FindByIdAsync(user.UserId.ToString());
                Assert.True((await users.RemoveFromRoleAsync(current!, "Admin")).Succeeded); return 0;
            });
            await Error(await Post(client, $"{order.Id}/aborto-sem-consumo", command, key), HttpStatusCode.Forbidden);
            await Error(await Post(ordinary, $"{order.Id}/aborto-sem-consumo", command, key), HttpStatusCode.Forbidden);
            Assert.Equal(HttpStatusCode.NotFound, (await ordinary.GetAsync($"/api/v1/ordens-producao/{Guid.NewGuid()}")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await ordinary.GetAsync("/api/v1/ordens-producao?pageSize=101")).StatusCode);
            Assert.Equal(before, await Snapshot());
        }
    }
}
