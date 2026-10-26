using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GenSW.Application.Formulation;
using GenSW.Application.Inventory;
using GenSW.Application.Production;
using GenSW.Domain.Formulation;
using GenSW.Domain.Inventory;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace GenSW.API.Tests;

public sealed partial class ProductionApiTests
{
    [Theory]
    [InlineData("mL", "g", "1", "0.8", "0.2")]
    [InlineData("kg", "L", "2", "0.4", "0.1")]
    public async Task T15_mass_balance_uses_explicit_conversion_subunits_and_inverse_without_requantizing_stock(string origin, string destination, string factor, string produced, string loss)
    {
        var (client, user) = await Session(true); using (client)
        {
            var input = await Stock(client, user.UserId, "2", "L");
            var conversion = await Formulation(s => s.CreateConversionAsync(input.Item.Id, new(origin, destination, factor, Evidence, "Medição sintética", InventoryRules.Today(TimeProvider.System), Evidence, "Amostra documentada", "Medido"), user.UserId, default));
            var linked = await Success<LoteView>(await Post(client, $"lotes/{input.Lot.Id}/referencias", new InventoryStateCommand(input.Lot.Revisao, "Conversão de massa selecionada", Evidence, ConversaoItemId: conversion.Id, Aplicabilidade: Evidence), inventory: true));
            input = input with { Lot = linked };
            var output = await Formulation(s => s.CreateItemAsync(new(Unique(), "Produto em massa sintético", null, null, "Alimentar", "kg", true, true, true, false), user.UserId, default));
            var local = await Success<LocalView>(await Post(client, "locais", new LocalCommand(Unique(), "Destino em massa sintético"), inventory: true), HttpStatusCode.Created);
            var data = new RecipeData("Processamento", "Quantidade", "1", "L",
                [new(Guid.NewGuid(), input.Item.Id, "Alimentar", "1", "L", "Variavel", ConversaoId: conversion.Id)],
                [new(Guid.NewGuid(), output.Id, "Produto medido", produced, "kg", "Variavel", true)], [], [], Evidence, []);
            var version = await RecipeAsync(user.UserId, input.Item.Id, output.Id, data: data);
            var scenario = new Scenario(input, output, local, version); var order = await Order(client, user.UserId, scenario);
            var start = Start(order, scenario, user.UserId);
            start = start with { Entradas = [start.Entradas[0] with { Alocacoes = [start.Entradas[0].Alocacoes[0] with { ConversaoItemId = conversion.Id }] }] };
            order = await Success<OrderView>(await Post(client, $"{order.Id}/inicio", start));
            var confirm = Confirm(order, scenario, user.UserId, "1", produced, "0") with
            { Entradas = start.Entradas, Perdas = [new(loss, "kg", "Perda de processo medida em massa")] };
            order = await Success<OrderView>(await Post(client, $"{order.Id}/confirmacao", confirm));
            using var snapshot = JsonDocument.Parse(order.Execucao!.SnapshotJson);
            Assert.Equal("0", snapshot.RootElement.GetProperty("balancoKg").GetString());
            Assert.Empty(snapshot.RootElement.GetProperty("avisosBalanco").EnumerateArray());
            Assert.Equal("1", (await Balance(client, input))!.Quantidade);
            Assert.Equal(produced, Assert.Single(order.Execucao.LotesSaida).Quantidade);
        }
    }

    [Theory]
    [InlineData("Apto", "FatoApto")]
    [InlineData("Irregular", "ConsumoIrregular")]
    public async Task T10_changed_eligibility_retains_reservation_and_only_documented_admin_fact_can_resolve_it(string aptitude, string result)
    {
        var (admin, user) = await Session(true); var (ordinary, _) = await Session(); using (admin) using (ordinary)
        {
            var f = await ScenarioAsync(admin, user.UserId); var order = await Order(admin, user.UserId, f);
            order = await Success<OrderView>(await Post(admin, $"{order.Id}/inicio", Start(order, f, user.UserId)));
            await Success<LoteView>(await Post(admin, $"lotes/{f.Input.Lot.Id}/bloqueio", new InventoryStateCommand(f.Input.Lot.Revisao, "Mudança durante execução", Evidence), inventory: true));
            var ordinaryCommand = Confirm(order, f, user.UserId); var before = await Snapshot();
            await Error(await Post(admin, $"{order.Id}/confirmacao", ordinaryCommand), HttpStatusCode.Conflict);
            Assert.Equal(before, await Snapshot()); ReservationBalances((await Get(admin, order.Id))!, 100, 100, 0, 0);
            var segregation = await Success<LocalView>(await Post(admin, "locais", new LocalCommand(Unique(), "Segregação para fato passado", Finalidade: "Segregacao"), inventory: true), HttpStatusCode.Created);
            var fact = ordinaryCommand with { Saidas = [ordinaryCommand.Saidas[0] with { LocalId = segregation.Id, Bloqueado = false }], DataObservada = InventoryRules.Today(TimeProvider.System), Evidencia = Evidence, AptidaoNoFato = aptitude };
            await Error(await Post(ordinary, $"{order.Id}/confirmacao-ocorrencia", fact), HttpStatusCode.Forbidden);
            await Error(await Post(admin, $"{order.Id}/confirmacao-ocorrencia", fact with { Evidencia = null }), HttpStatusCode.BadRequest);
            order = await Success<OrderView>(await Post(admin, $"{order.Id}/confirmacao-ocorrencia", fact));
            Assert.Equal(result, order.Execucao!.Resultado); ReservationBalances(order, 100, 0, 100, 0);
            var output = Assert.Single(order.Execucao.LotesSaida); Assert.Equal(segregation.Id, output.LocalId);
            var lot = (await admin.GetFromJsonAsync<LoteView>($"/api/v1/estoque/lotes/{output.LoteId}"))!; Assert.Equal("Bloqueado", lot.Situacao);
            var balance = (await admin.GetFromJsonAsync<SaldoView>($"/api/v1/estoque/saldos/{output.LoteId}/{output.LocalId}"))!; Assert.False(balance.Elegivel); Assert.Equal("98", balance.Quantidade);
        }
    }

    [Fact]
    public async Task T13_even_a_released_descendant_reservation_permanently_blocks_parent_reversal()
    {
        var (client, user) = await Session(true); using (client)
        {
            var f = await ScenarioAsync(client, user.UserId); var parent = await Order(client, user.UserId, f);
            parent = await Success<OrderView>(await Post(client, $"{parent.Id}/inicio", Start(parent, f, user.UserId)));
            parent = await Success<OrderView>(await Post(client, $"{parent.Id}/confirmacao", Confirm(parent, f, user.UserId)));
            var produced = Assert.Single(parent.Execucao!.LotesSaida);
            var lot = (await client.GetFromJsonAsync<LoteView>($"/api/v1/estoque/lotes/{produced.LoteId}"))!;
            var version = await RecipeAsync(user.UserId, f.Output.Id, f.Input.Item.Id, "60", "58");
            var childFixture = new Scenario(new(f.Output, lot, f.Destination), f.Input.Item, f.Input.Local, version);
            var child = await Order(client, user.UserId, childFixture);
            child = await Success<OrderView>(await Post(client, $"{child.Id}/inicio", Start(child, childFixture, user.UserId)));
            child = await Success<OrderView>(await Post(client, $"{child.Id}/aborto-sem-consumo", new AbortCommand(child.Revisao, true, "Reserva liberada sem consumo", Evidence)));
            ReservationBalances(child, 60, 0, 0, 60);
            var before = await Snapshot();
            await Error(await Post(client, $"{parent.Id}/reversao", new ReverseCommand(parent.Revisao, true, "Reserva já liberada", Evidence)), HttpStatusCode.Conflict);
            Assert.Equal(before, await Snapshot());
        }
    }

    [Fact]
    public async Task T14_A_to_B_to_C_consumes_only_intermediate_and_additional_receipt_is_forbidden()
    {
        var (client, user) = await Session(true); using (client)
        {
            var f = await ScenarioAsync(client, user.UserId); var first = await Order(client, user.UserId, f);
            first = await Success<OrderView>(await Post(client, $"{first.Id}/inicio", Start(first, f, user.UserId)));
            first = await Success<OrderView>(await Post(client, $"{first.Id}/confirmacao", Confirm(first, f, user.UserId)));
            var b = Assert.Single(first.Execucao!.LotesSaida); var bLot = (await client.GetFromJsonAsync<LoteView>($"/api/v1/estoque/lotes/{b.LoteId}"))!;
            var third = await Formulation(s => s.CreateItemAsync(new(Unique(), "Produto C sintético", null, null, "Alimentar", "kg", true, true, true, false), user.UserId, default));
            var recipe = await RecipeAsync(user.UserId, f.Output.Id, third.Id, "60", "57");
            var secondFixture = new Scenario(new(f.Output, bLot, f.Destination), third, f.Input.Local, recipe);
            var second = await Order(client, user.UserId, secondFixture);
            second = await Success<OrderView>(await Post(client, $"{second.Id}/inicio", Start(second, secondFixture, user.UserId)));
            second = await Success<OrderView>(await Post(client, $"{second.Id}/confirmacao", Confirm(second, secondFixture, user.UserId, "60", "57", "3")));
            Assert.Equal("0", (await Balance(client, f.Input))!.Quantidade); Assert.Equal("38", (await Balance(client, secondFixture.Input))!.Quantidade);
            var c = Assert.Single(second.Execucao!.LotesSaida); Assert.Equal("57", c.Quantidade);
            var edges = await client.GetFromJsonAsync<ProductionPage<GenealogyEdge>>($"/api/v1/estoque/lotes/{c.LoteId}/genealogia?direcao=ancestrais&profundidade=3&pageSize=100");
            Assert.Contains(edges!.Items, e => e.LoteEntradaId == f.Input.Lot.Id && e.LoteSaidaId == b.LoteId);
            Assert.Contains(edges.Items, e => e.LoteEntradaId == b.LoteId && e.LoteSaidaId == c.LoteId);
            var before = await Snapshot();
            await Error(await Post(client, "entradas", Movement(secondFixture.Input, user.UserId, "1", 2), inventory: true), HttpStatusCode.Conflict);
            await Error(await Post(client, "aberturas", Movement(secondFixture.Input, user.UserId, "1", 2), inventory: true), HttpStatusCode.Conflict);
            await Error(await Post(client, $"{first.Id}/reversao", new ReverseCommand(first.Revisao, true, "Descendente já consumiu", Evidence)), HttpStatusCode.Conflict);
            Assert.Equal(before, await Snapshot());
            var adjustment = Movement(secondFixture.Input, user.UserId, "40", 2) with { QuantidadeContada = "40", ExecucaoReferenciaId = first.Execucao.Id };
            await Success<JsonElement>(await Post(client, "ajustes", adjustment, inventory: true), HttpStatusCode.Created);
            Assert.Equal("98", Assert.Single((await Get(client, first.Id))!.Execucao!.LotesSaida).Quantidade);
            Assert.Equal("40", (await Balance(client, secondFixture.Input))!.Quantidade);
        }
    }

    [Theory]
    [InlineData("un", "1.5")]
    [InlineData("L", "1")]
    public async Task T15_fractional_count_or_cross_dimension_without_explicit_factor_is_rejected(string unit, string quantity)
    {
        var (client, user) = await Session(); using (client)
        {
            var f = await ScenarioAsync(client, user.UserId, unit: unit == "un" ? "un" : "kg"); var order = await Order(client, user.UserId, f); var command = Start(order, f, user.UserId);
            command = command with { Entradas = [command.Entradas[0] with { Alocacoes = [command.Entradas[0].Alocacoes[0] with { Unidade = unit, Quantidade = quantity }] }] };
            var before = await Snapshot(); await Error(await Post(client, $"{order.Id}/inicio", command), HttpStatusCode.BadRequest); Assert.Equal(before, await Snapshot());
        }
    }

    [Fact]
    public async Task T15_quantization_requires_individual_acceptance_for_each_allocation_and_preview_has_no_effect()
    {
        var (client, user) = await Session(); using (client)
        {
            var f = await ScenarioAsync(client, user.UserId, stock: "1", planned: "0.000002", output: "0.000002");
            var second = new RecipeInput(Guid.NewGuid(), f.Input.Item.Id, "Alimentar", "0.000002", "kg", "Variavel");
            var data = f.Version.Conteudo with { Entradas = [f.Version.Conteudo.Entradas[0], second], Saidas = [f.Version.Conteudo.Saidas[0] with { Quantidade = "0.000004" }] };
            f = f with { Version = await RecipeAsync(user.UserId, f.Input.Item.Id, f.Output.Id, data: data) };
            var order = await Order(client, user.UserId, f);
            var allocations = data.Entradas.Select(line => new InputCommand(line.Id, [new(Guid.NewGuid(), f.Input.Lot.Id, f.Input.Local.Id, "0.0015", "g")])).ToArray();
            var command = new StartCommand(order.Revisao, user.UserId, allocations, "Quantização por linha conferida"); var before = await Snapshot();
            var preview = await Success<ProductionPreview>(await Post(client, $"{order.Id}/previas", new ProductionPreviewCommand("inicio", Inicio: command)));
            Assert.Equal(2, preview.Quantidades.Count); Assert.All(preview.Quantidades, p => { Assert.Equal("0.000002", p.Quantidade.Normalizada); Assert.Equal("-0.0000005", p.Quantidade.Residuo); });
            Assert.Equal(before, await Snapshot());
            await Error(await Post(client, $"{order.Id}/inicio", command), HttpStatusCode.BadRequest);
            var acceptance = new QuantizationAcceptance("0.0000015", "0.000002", "-0.0000005", "Resolução da medição aceita por alocação");
            var partial = command with { Entradas = [allocations[0] with { Alocacoes = [allocations[0].Alocacoes[0] with { AceiteQuantizacao = acceptance }] }, allocations[1]] };
            await Error(await Post(client, $"{order.Id}/inicio", partial), HttpStatusCode.BadRequest); Assert.Equal(before, await Snapshot());
            var accepted = command with { Entradas = allocations.Select(line => line with { Alocacoes = [line.Alocacoes[0] with { AceiteQuantizacao = acceptance }] }).ToArray() };
            order = await Success<OrderView>(await Post(client, $"{order.Id}/inicio", accepted)); ReservationBalances(order, 0.000004m, 0.000004m, 0, 0);
        }
    }

    [Fact]
    public async Task T15_each_output_parcel_creates_a_distinct_lot_and_keeps_all_physical_genealogy_edges()
    {
        var (client, user) = await Session(); using (client)
        {
            var f = await ScenarioAsync(client, user.UserId); var order = await Order(client, user.UserId, f);
            order = await Success<OrderView>(await Post(client, $"{order.Id}/inicio", Start(order, f, user.UserId)));
            var destination = await Success<LocalView>(await Post(client, "locais", new LocalCommand(Unique(), "Segunda parcela sintética"), inventory: true), HttpStatusCode.Created);
            var command = Confirm(order, f, user.UserId); var output = command.Saidas[0];
            command = command with { Saidas = [output with { Quantidade = "60" }, output with { Id = Guid.NewGuid(), Quantidade = "38", NovoCodigoLote = Unique(), LocalId = destination.Id }] };
            order = await Success<OrderView>(await Post(client, $"{order.Id}/confirmacao", command));
            Assert.Equal(2, order.Execucao!.LotesSaida.Count); Assert.Equal(2, order.Execucao.LotesSaida.Select(x => x.LoteId).Distinct().Count());
            Assert.Equal(98m, order.Execucao.LotesSaida.Sum(x => decimal.Parse(x.Quantidade, System.Globalization.CultureInfo.InvariantCulture)));
            var genealogy = await client.GetFromJsonAsync<ProductionPage<GenealogyEdge>>($"/api/v1/estoque/lotes/{f.Input.Lot.Id}/genealogia?direcao=destinos&pageSize=100");
            Assert.Equal(2, genealogy!.TotalItems); Assert.All(genealogy.Items, edge => Assert.Equal(f.Input.Lot.Id, edge.LoteEntradaId));
        }
    }

    [Fact]
    public async Task T12_T15_inverse_one_third_conversion_accepts_precise_residue_and_reversal_restores_original_normalized_fact()
    {
        var (client, user) = await Session(true); using (client)
        {
            var f = await ScenarioAsync(client, user.UserId, stock: "1", planned: "0.333333", output: "0.333333");
            var conversion = await Formulation(s => s.CreateConversionAsync(f.Input.Item.Id, new("kg", "L", "3", Evidence, "Medição sintética", InventoryRules.Today(TimeProvider.System), Evidence, "Amostra sintética", "Medido"), user.UserId, default));
            var linked = await Success<LoteView>(await Post(client, $"lotes/{f.Input.Lot.Id}/referencias", new InventoryStateCommand(f.Input.Lot.Revisao, "Conversão específica do lote", Evidence, ConversaoItemId: conversion.Id, Aplicabilidade: Evidence), inventory: true));
            f = f with { Input = f.Input with { Lot = linked } };
            var order = await Order(client, user.UserId, f); var start = Start(order, f, user.UserId);
            var allocation = start.Entradas[0].Alocacoes[0] with { Quantidade = "1", Unidade = "L", ConversaoItemId = conversion.Id };
            start = start with { Entradas = [start.Entradas[0] with { Alocacoes = [allocation] }] };
            var preview = await Success<ProductionPreview>(await Post(client, $"{order.Id}/previas", new ProductionPreviewCommand("inicio", Inicio: start)));
            var quantity = Assert.Single(preview.Quantidades).Quantidade;
            Assert.Equal("0.3333333333333333333333333333", quantity.Calculada); Assert.Equal("0.333333", quantity.Normalizada); Assert.True(quantity.ExigeAceite);
            var accepted = allocation with { AceiteQuantizacao = new(quantity.Calculada, quantity.Normalizada, quantity.Residuo, "Resíduo de divisão por três medido e aceito") };
            start = start with { Entradas = [start.Entradas[0] with { Alocacoes = [accepted] }] };
            order = await Success<OrderView>(await Post(client, $"{order.Id}/inicio", start));
            var reserved = Assert.Single(order.Reservas);
            Assert.Equal(conversion.Id, reserved.ConversaoItemId); Assert.Equal("1", reserved.QuantidadeDeclarada);
            Assert.Equal("L", reserved.UnidadeDeclarada); Assert.Equal(accepted.AceiteQuantizacao, reserved.AceiteQuantizacao);
            using (var reservationSnapshot = JsonDocument.Parse(await Factory.ExecuteDbContextAsync(db => db.Set<GenSW.Domain.Production.ReservaProducao>().Where(r => r.Id == reserved.Id).Select(r => r.SnapshotJson).SingleAsync())))
            {
                var document = reservationSnapshot.RootElement.GetProperty("conversao");
                Assert.Equal(conversion.Id, document.GetProperty("id").GetGuid()); Assert.Equal("3", document.GetProperty("fator").GetString());
                Assert.Equal(Evidence, document.GetProperty("fonte").GetString()); Assert.Equal(Evidence, document.GetProperty("contexto").GetString());
            }
            var confirm = Confirm(order, f, user.UserId, "0.333333", "0.333333", "0") with { Entradas = start.Entradas };
            order = await Success<OrderView>(await Post(client, $"{order.Id}/confirmacao", confirm));
            Assert.Equal("0.666667", (await Balance(client, f.Input))!.Quantidade);
            var current = await Formulation(s => s.CreateConversionAsync(f.Input.Item.Id, new("kg", "L", "6", Evidence, "Nova medição sintética", InventoryRules.Today(TimeProvider.System), Evidence, "Outra amostra sintética", "Medido"), user.UserId, default));
            await Success<LoteView>(await Post(client, $"lotes/{f.Input.Lot.Id}/referencias", new InventoryStateCommand(linked.Revisao, "Referência atual mudou após confirmação", Evidence, ConversaoItemId: current.Id, Aplicabilidade: Evidence), inventory: true));
            order = await Success<OrderView>(await Post(client, $"{order.Id}/reversao", new ReverseCommand(order.Revisao, true, "Correção sintética e coerência física comprovada", Evidence)));
            Assert.Equal("1", (await Balance(client, f.Input))!.Quantidade); Assert.Equal("Revertida", order.Estado);
            var events = await client.GetFromJsonAsync<InventoryPage<EventoView>>($"/api/v1/estoque/movimentos?loteId={f.Input.Lot.Id}");
            var original = Assert.Single(events!.Items, e => e.Tipo == "ProducaoConsumo");
            var compensation = Assert.Single(events.Items, e => e.Tipo == "ReversaoProducaoConsumo");
            Assert.Equal(original.Id, compensation.EventoReferenciaId); Assert.Equal("0.333333", Assert.Single(compensation.Movimentos).Quantidade);
            Assert.Equal("1", Assert.Single(original.Movimentos).QuantidadeDeclarada); Assert.Equal("L", Assert.Single(original.Movimentos).UnidadeDeclarada);
            Assert.Contains(quantity.Residuo, Assert.Single(original.Movimentos).SnapshotJson);
            using var movementSnapshot = JsonDocument.Parse(Assert.Single(original.Movimentos).SnapshotJson);
            var frozenConversion = movementSnapshot.RootElement.GetProperty("conversao");
            Assert.Equal(conversion.Id, frozenConversion.GetProperty("id").GetGuid()); Assert.Equal("3", frozenConversion.GetProperty("fator").GetString());
            Assert.Equal(Evidence, frozenConversion.GetProperty("fonte").GetString()); Assert.Equal(Evidence, frozenConversion.GetProperty("contexto").GetString());
        }
    }

    [Fact]
    public async Task T10_expiry_is_inclusive_and_next_operational_day_blocks_confirmation_without_releasing_reservations()
    {
        var (client, user) = await Session(); using (client)
        {
            var date = InventoryRules.Today(TimeProvider.System); var f = await ScenarioAsync(client, user.UserId, expiry: date);
            var clock = new ProductionClock(new DateTimeOffset(date.ToDateTime(new TimeOnly(15, 0)), TimeSpan.Zero));
            using var host = Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => { services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(clock); }));
            using var timed = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false }); timed.DefaultRequestHeaders.Authorization = client.DefaultRequestHeaders.Authorization;
            var order = await Order(timed, user.UserId, f); order = await Success<OrderView>(await Post(timed, $"{order.Id}/inicio", Start(order, f, user.UserId)));
            Assert.Equal("Iniciada", order.Estado); clock.Advance(TimeSpan.FromDays(1));
            var before = await Snapshot(); await Error(await Post(timed, $"{order.Id}/confirmacao", Confirm(order, f, user.UserId)), HttpStatusCode.Conflict);
            Assert.Equal(before, await Snapshot()); ReservationBalances((await Get(timed, order.Id))!, 100, 100, 0, 0);
        }
    }

    private sealed class ProductionClock(DateTimeOffset initial) : TimeProvider
    {
        private long ticks = initial.UtcTicks;
        public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref ticks), TimeSpan.Zero);
        public void Advance(TimeSpan value) => Interlocked.Add(ref ticks, value.Ticks);
    }
}
