using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GenSW.Application.Formulation;
using GenSW.Application.Inventory;
using GenSW.Application.Production;
using GenSW.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GenSW.API.Tests;

public sealed partial class ProductionApiTests
{
    [Fact]
    public async Task T01_plan_freezes_exact_version_revision_and_labels_without_stock_effects()
    {
        var (client, user) = await Session(); using (client)
        {
            var f = await ScenarioAsync(client, user.UserId); var before = (await Balance(client, f.Input))!;
            var order = await Order(client, user.UserId, f); var frozen = JsonSerializer.Serialize(order.Planejamento);
            Assert.Equal("Planejada", order.Estado); Assert.Equal(f.Version.Id, order.Planejamento!.Receita.Id);
            Assert.Empty(order.Reservas); Assert.Null(order.Execucao);
            await Formulation(s => s.UpdateItemAsync(f.Input.Item.Id, new(f.Input.Item.Data() with { Nome = "Nome atual alterado" }, f.Input.Item.Revisao), user.UserId, default));
            Assert.Equal(frozen, JsonSerializer.Serialize((await Get(client, order.Id))!.Planejamento));
            await Error(await Post(client, $"{order.Id}/revisao-planejamento", new ProductionStateCommand(order.Revisao - 1, "Revisão obsoleta")), HttpStatusCode.Conflict);
            order = await Success<OrderView>(await Post(client, $"{order.Id}/revisao-planejamento", new ProductionStateCommand(order.Revisao, "Replanejamento explícito")));
            Assert.Equal("Rascunho", order.Estado);
            order = await Success<OrderView>(await Post(client, $"{order.Id}/cancelamento", new ProductionStateCommand(order.Revisao, "Cancelamento antes de produzir")));
            Assert.Equal("Cancelada", order.Estado); Assert.Empty(order.Reservas); Assert.Null(order.Execucao);
            Assert.Equal(before.Quantidade, (await Balance(client, f.Input))!.Quantidade);
            Assert.Equal(before.Revisao, (await Balance(client, f.Input))!.Revisao);
            var history = await client.GetFromJsonAsync<ProductionPage<JsonElement>>($"/api/v1/ordens-producao/{order.Id}/historico");
            Assert.True(history!.TotalItems >= 4);
        }
    }

    [Fact]
    public async Task T02_two_orders_racing_for_sixty_of_one_hundred_reserve_exactly_once()
    {
        var (client, user) = await Session(); using (client)
        {
            var f = await ScenarioAsync(client, user.UserId, planned: "60", output: "58");
            var a = await Order(client, user.UserId, f); var b = await Order(client, user.UserId, f);
            var replies = await Race(() => Post(client, $"{a.Id}/inicio", Start(a, f, user.UserId)), () => Post(client, $"{b.Id}/inicio", Start(b, f, user.UserId)));
            Assert.Single(replies, r => r.StatusCode == HttpStatusCode.OK); Assert.Single(replies, r => r.StatusCode == HttpStatusCode.Conflict);
            var orders = new[] { (await Get(client, a.Id))!, (await Get(client, b.Id))! };
            var started = Assert.Single(orders, o => o.Estado == "Iniciada"); ReservationBalances(started, 60, 60, 0, 0);
            Assert.Empty(Assert.Single(orders, o => o.Estado == "Planejada").Reservas);
            Assert.Equal("100", (await Balance(client, f.Input))!.Quantidade);
            var balance = await client.GetFromJsonAsync<JsonElement>($"/api/v1/estoque/saldos/{f.Input.Lot.Id}/{f.Input.Local.Id}");
            Assert.Equal("60", balance.GetProperty("reservado").GetString()); Assert.Equal("40", balance.GetProperty("disponivel").GetString());
            foreach (var reply in replies) reply.Dispose();
        }
    }

    [Theory]
    [InlineData("saidas-manuais")]
    [InlineData("consumos-internos")]
    [InlineData("ajustes")]
    [InlineData("descartes")]
    public async Task T03_inventory_withdrawals_and_admin_adjustment_cannot_cross_active_reservations(string operation)
    {
        var (client, user) = await Session(true); using (client)
        {
            var f = await ScenarioAsync(client, user.UserId, planned: "60", output: "58"); var order = await Order(client, user.UserId, f);
            var command = Movement(f.Input, user.UserId, "50", 1);
            if (operation == "ajustes") command = command with { QuantidadeContada = "50" };
            var replies = await Race(() => Post(client, $"{order.Id}/inicio", Start(order, f, user.UserId)), () => Post(client, operation, command, inventory: true));
            Assert.Single(replies, r => r.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created);
            Assert.Single(replies, r => r.StatusCode == HttpStatusCode.Conflict);
            var after = (await Get(client, order.Id))!; var quantity = decimal.Parse((await Balance(client, f.Input))!.Quantidade, CultureInfo.InvariantCulture);
            if (after.Estado == "Iniciada") { ReservationBalances(after, 60, 60, 0, 0); Assert.Equal(100m, quantity); }
            else { Assert.Equal("Planejada", after.Estado); Assert.Empty(after.Reservas); Assert.Equal(50m, quantity); }
            var reconciliation = await client.GetFromJsonAsync<InventoryPage<ReconciliacaoView>>($"/api/v1/estoque/reconciliacao?loteId={f.Input.Lot.Id}"); Assert.Empty(reconciliation!.Items);
            foreach (var reply in replies) reply.Dispose();
        }
    }

    [Theory]
    [InlineData("transferencias", "Ordinario")]
    [InlineData("segregacoes", "Segregacao")]
    public async Task T03_transfer_or_segregation_racing_with_start_cannot_move_reserved_material(string operation, string purpose)
    {
        var (client, user) = await Session(true); using (client)
        {
            var f = await ScenarioAsync(client, user.UserId, planned: "60", output: "58"); var order = await Order(client, user.UserId, f);
            var destination = await Success<LocalView>(await Post(client, "locais", new LocalCommand(Unique(), "Destino concorrente sintético", Finalidade: purpose), inventory: true), HttpStatusCode.Created);
            var movement = new InventoryMovementCommand(f.Input.Lot.Id, "50", "kg", user.UserId, "Retirada em corrida com reserva", new(f.Input.Item.Revisao, f.Input.Lot.Revisao, OrigemLocal: f.Input.Local.Revisao, DestinoLocal: destination.Revisao, OrigemPosicao: 1),
                OrigemLocalId: f.Input.Local.Id, DestinoLocalId: destination.Id, Destino: "Destino conferido", Evidencia: Evidence);
            var replies = await Race(() => Post(client, $"{order.Id}/inicio", Start(order, f, user.UserId)), () => Post(client, operation, movement, inventory: true));
            Assert.Single(replies, r => r.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created); Assert.Single(replies, r => r.StatusCode == HttpStatusCode.Conflict);
            var current = (await Get(client, order.Id))!;
            if (current.Estado == "Iniciada") { ReservationBalances(current, 60, 60, 0, 0); Assert.Equal("100", (await Balance(client, f.Input))!.Quantidade); }
            else { Assert.Equal("Planejada", current.Estado); Assert.Empty(current.Reservas); Assert.Equal("50", (await Balance(client, f.Input))!.Quantidade); }
            foreach (var reply in replies) reply.Dispose();
        }
    }

    [Fact]
    public async Task T04_insufficient_last_input_rolls_back_all_reservations_state_and_history()
    {
        var (client, user) = await Session(); using (client)
        {
            var f = await ScenarioAsync(client, user.UserId); var second = await Stock(client, user.UserId, "9");
            var content = f.Version.Conteudo with { Entradas = [f.Version.Conteudo.Entradas[0], new(Guid.NewGuid(), second.Item.Id, "Alimentar", "10", "kg", "Variavel")] };
            var version = await RecipeAsync(user.UserId, f.Input.Item.Id, f.Output.Id, data: content); f = f with { Version = version };
            var order = await Order(client, user.UserId, f); var before = await Snapshot();
            var command = Start(order, f, user.UserId) with { Entradas = [Start(order, f, user.UserId).Entradas[0], new(version.Conteudo.Entradas[1].Id, [new(Guid.NewGuid(), second.Lot.Id, second.Local.Id, "10", "kg")])] };
            await Error(await Post(client, $"{order.Id}/inicio", command), HttpStatusCode.Conflict);
            Assert.Equal(before, await Snapshot()); Assert.Equal("Planejada", (await Get(client, order.Id))!.Estado);
        }
    }

    [Fact]
    public async Task T05_T06_concurrent_confirmation_replay_is_shared_between_authors_and_preserves_business_hash()
    {
        var (client, user) = await Session(); var (other, second) = await Session(); using (client) using (other)
        {
            var f = await ScenarioAsync(client, user.UserId); var order = await Order(client, user.UserId, f);
            order = await Success<OrderView>(await Post(client, $"{order.Id}/inicio", Start(order, f, user.UserId)));
            var command = Confirm(order, f, user.UserId); var equivalent = command with { Entradas = [command.Entradas[0] with { Alocacoes = [command.Entradas[0].Alocacoes[0] with { Quantidade = "100.000000" }] }] };
            var key = Unique(); var replies = await Race(() => Post(client, $"{order.Id}/confirmacao", command, key), () => Post(other, $"{order.Id}/confirmacao", equivalent, key));
            Assert.All(replies, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
            var body = await replies[0].Content.ReadAsStringAsync(); Assert.Equal(body, await replies[1].Content.ReadAsStringAsync());
            var confirmed = InventoryJson.Read<OrderView>(body); Assert.Equal("Confirmada", confirmed.Estado); Assert.NotNull(confirmed.Execucao);
            Assert.Contains(confirmed.Execucao.AutorId, new[] { user.UserId, second.UserId });
            var before = await Snapshot();
            Assert.Contains("idempotencia_divergente", await Error(await Post(other, $"{order.Id}/confirmacao", command with { Motivo = "Motivo alterado" }, key), HttpStatusCode.Conflict));
            Assert.Contains("estado_invalido", await Error(await Post(client, $"{order.Id}/confirmacao", command), HttpStatusCode.Conflict));
            Assert.Equal(before, await Snapshot()); ReservationBalances(confirmed, 100, 0, 100, 0); Assert.Equal("0", (await Balance(client, f.Input))!.Quantidade);
            foreach (var reply in replies) reply.Dispose();
        }
    }

    [Theory]
    [InlineData("90", "88", "100", "90", "10")]
    [InlineData("110", "108", "110", "110", "0")]
    public async Task T09_real_less_or_more_preserves_original_reservation_and_separate_supplement(string input, string output, string totalOriginal, string consumed, string released)
    {
        var (client, user) = await Session(); using (client)
        {
            var f = await ScenarioAsync(client, user.UserId, stock: "150"); var order = await Order(client, user.UserId, f);
            order = await Success<OrderView>(await Post(client, $"{order.Id}/inicio", Start(order, f, user.UserId)));
            var original = Assert.Single(order.Reservas).Id;
            order = await Success<OrderView>(await Post(client, $"{order.Id}/confirmacao", Confirm(order, f, user.UserId, input, output)));
            ReservationBalances(order, decimal.Parse(totalOriginal), 0, decimal.Parse(consumed), decimal.Parse(released));
            Assert.Equal("100", order.Reservas.Single(r => r.Id == original).QuantidadeOriginal);
            if (input == "110") Assert.Equal("10", Assert.Single(order.Reservas, r => r.Id != original).QuantidadeOriginal);
            Assert.Equal((150m - decimal.Parse(input)).ToString(CultureInfo.InvariantCulture), (await Balance(client, f.Input))!.Quantidade);
        }
    }

    [Fact]
    public async Task T09_insufficient_supplement_retains_every_original_reservation_and_duplicate_allocation_is_rejected()
    {
        var (client, user) = await Session(); using (client)
        {
            var f = await ScenarioAsync(client, user.UserId); var order = await Order(client, user.UserId, f); var start = Start(order, f, user.UserId);
            var duplicate = start with { Entradas = [start.Entradas[0] with { Alocacoes = [start.Entradas[0].Alocacoes[0], start.Entradas[0].Alocacoes[0] with { Id = Guid.NewGuid() }] }] };
            await Error(await Post(client, $"{order.Id}/inicio", duplicate), HttpStatusCode.BadRequest);
            order = await Success<OrderView>(await Post(client, $"{order.Id}/inicio", start)); var before = await Snapshot();
            await Error(await Post(client, $"{order.Id}/confirmacao", Confirm(order, f, user.UserId, "101", "99")), HttpStatusCode.Conflict);
            Assert.Equal(before, await Snapshot()); ReservationBalances((await Get(client, order.Id))!, 100, 100, 0, 0);
        }
    }

    private Task<string> Snapshot() => Factory.ExecuteDbContextAsync(async db =>
    {
        var rows = await FormulationMigrationTests.RowsAsync(db);
        return JsonSerializer.Serialize(rows.Where(x => x.Key.Contains("Producao") || new[] { "LotesMateriais", "PosicoesEstoque", "EventosEstoque", "MovimentosEstoque", "HistoricoEstoque", "ComandosEstoque", "ExecucaoMovimentos", "GenealogiaMateriais" }.Contains(x.Key)).ToDictionary(x => x.Key, x => x.Value));
    });
}
