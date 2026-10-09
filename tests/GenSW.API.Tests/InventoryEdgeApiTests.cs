using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GenSW.Application.Formulation;
using GenSW.Application.Inventory;
using GenSW.Domain.Catalog;
using GenSW.Domain.Formulation;
using GenSW.Domain.Inventory;
using GenSW.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GenSW.API.Tests;

[Collection(PostgreSqlAnimalIntegrationCollection.Name)]
public sealed class InventoryEdgeApiTests(AnimalApiPostgreSqlFixture fixture)
{
    private AuthWebApplicationFactory Factory => fixture.Factory;
    private async Task<(HttpClient Client, SeededUser User)> Session(bool admin = false)
    {
        var user = await Factory.SeedUserAsync($"stock_edge_{Guid.NewGuid():N}", roles: admin ? ["Admin"] : []);
        var client = Factory.CreateHttpsClient();
        using var login = await client.LoginAsync(user.UserName, user.Password);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.ReadAccessTokenAsync()).AccessToken);
        return (client, user);
    }
    private static Task<HttpResponseMessage> Post(HttpClient client, string path, object command, string? key = null) => Send(client, HttpMethod.Post, path, command, key);
    private static async Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string path, object command, string? key = null)
    {
        using var request = new HttpRequestMessage(method, "/api/v1/estoque/" + path) { Content = JsonContent.Create(command) };
        request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString("N"));
        return await client.SendAsync(request);
    }
    private static async Task<T> Created<T>(HttpResponseMessage response)
    {
        using (response)
        {
            Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
            Assert.NotNull(response.Headers.Location);
            return InventoryJson.Read<T>(await response.Content.ReadAsStringAsync());
        }
    }
    private static async Task<CatalogItem> Item(HttpClient client, string unit = "kg")
    {
        using var response = await client.PostAsJsonAsync("/api/v1/itens", new ItemData(Guid.NewGuid().ToString("N"), "Material sintético de limites F01", null, null, "Alimentar", unit, true, true, true, false));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CatalogItem>())!;
    }
    private static Task<LocalView> Local(HttpClient client, string purpose = "Ordinario") => Created<LocalView>(Post(client, "locais", new LocalCommand(Guid.NewGuid().ToString("N"), "Local sintético de limites", Finalidade: purpose)));
    private static async Task<T> Created<T>(Task<HttpResponseMessage> response) => await Created<T>(await response);
    private static LoteCommand LotCommand(CatalogItem item, Guid responsible) => new(item.Id, Guid.NewGuid().ToString("N"), "Origem sintética de limites", "Fixture PostgreSQL isolada", responsible, ItemVersaoEsperada: item.Revisao);
    private static InventoryMovementCommand Command(CatalogItem item, LoteView lot, LocalView local, Guid responsible, string quantity, int position = 0) =>
        new(lot.Id, quantity, lot.Unidade, responsible, "Conferência sintética de limites", new(item.Revisao, lot.Revisao, local.Revisao, position), LocalId: local.Id,
            DataObservada: new DateOnly(2026, 1, 1), Origem: lot.Origem, Fonte: lot.Fonte, Destino: "Uso sintético", Evidencia: "Fixture isolada, sem material real");
    private static Task<SaldoView?> Balance(HttpClient client, Guid lot, Guid local) => client.GetFromJsonAsync<SaldoView>($"/api/v1/estoque/saldos/{lot}/{local}");

    [Fact]
    public async Task Exceptional_lot_creation_replay_requires_current_Admin_even_after_item_reactivation()
    {
        var (client, user) = await Session(true); using (client)
        {
            var item = await Item(client);
            using var inactive = await client.PatchAsJsonAsync($"/api/v1/itens/{item.Id}/ativo", new StatusCommand(false, item.Revisao));
            Assert.Equal(HttpStatusCode.OK, inactive.StatusCode); item = (await inactive.Content.ReadFromJsonAsync<CatalogItem>())!;
            var command = LotCommand(item, user.UserId); var key = Guid.NewGuid().ToString("N");
            var lot = await Created<LoteView>(Post(client, "lotes", command, key)); Assert.Equal("Bloqueado", lot.Situacao);
            using var active = await client.PatchAsJsonAsync($"/api/v1/itens/{item.Id}/ativo", new StatusCommand(true, item.Revisao));
            Assert.Equal(HttpStatusCode.OK, active.StatusCode);
            await Factory.ExecuteScopeAsync(async services =>
            {
                var users = services.GetRequiredService<UserManager<ApplicationUser>>();
                var current = (await users.FindByIdAsync(user.UserId.ToString()))!;
                Assert.True((await users.RemoveFromRoleAsync(current, "Admin")).Succeeded); return 0;
            });
            // The access token still carries Admin. The service must use the durable
            // original requirement and the current account, before stale revisions.
            using var replay = await Post(client, "lotes", command, key); Assert.Equal(HttpStatusCode.Forbidden, replay.StatusCode);
            Assert.Equal(1, await Factory.ExecuteDbContextAsync(db => db.Set<LoteMaterial>().CountAsync(x => x.Id == lot.Id)));
        }
    }

    [Fact]
    public async Task Admin_can_record_expiry_with_an_active_ordinary_responsible_and_history_retains_it()
    {
        var (admin, author) = await Session(true); using (admin)
        {
            var responsible = await Factory.SeedUserAsync($"stock_resp_{Guid.NewGuid():N}", roles: []);
            var item = await Item(admin); var lot = await Created<LoteView>(Post(admin, "lotes", LotCommand(item, responsible.UserId)));
            var expiry = InventoryRules.Today(TimeProvider.System).AddDays(10);
            using var change = await Post(admin, $"lotes/{lot.Id}/validade", new InventoryStateCommand(lot.Revisao, "Fonte sintética de validade", "Documento sintético conferido", Validade: expiry, FonteValidade: "Fixture", ResponsavelId: responsible.UserId));
            Assert.True(change.StatusCode == HttpStatusCode.OK, await change.Content.ReadAsStringAsync());
            var current = InventoryJson.Read<LoteView>(await change.Content.ReadAsStringAsync());
            Assert.Equal(expiry, current.Validade); Assert.Equal(responsible.UserId, current.ResponsavelValidadeId);
            var history = await admin.GetFromJsonAsync<InventoryPage<HistoricoView>>($"/api/v1/estoque/lotes/{lot.Id}/historico");
            var fact = Assert.Single(history!.Items, x => x.Operacao == "Validade"); Assert.Equal(author.UserId, fact.AutorId);
            using var snapshot = JsonDocument.Parse(fact.DepoisJson);
            Assert.Equal(responsible.UserId, snapshot.RootElement.GetProperty("lote").GetProperty("responsavelValidadeId").GetGuid());
            await Factory.SetUserActiveAsync(responsible.UserId, false);
            var historical = await admin.GetFromJsonAsync<ResponsavelView>($"/api/v1/estoque/responsaveis/{responsible.UserId}");
            Assert.False(historical!.Ativo); Assert.Equal(responsible.Nome, historical.Nome);
            using var forbiddenNewUse = await Post(admin, $"lotes/{lot.Id}/validade", new InventoryStateCommand(current.Revisao, "Outra fonte", "Outro documento", Validade: expiry.AddDays(1), FonteValidade: "Fixture", ResponsavelId: responsible.UserId));
            Assert.Equal(HttpStatusCode.Conflict, forbiddenNewUse.StatusCode);
        }
    }

    [Fact]
    public async Task Inverse_conversion_is_exact_and_canonical_transfer_freezes_both_documentary_references()
    {
        var (client, user) = await Session(true); using (client)
        {
            var item = await Item(client, "un");
            using var conversionResponse = await client.PostAsJsonAsync($"/api/v1/itens/{item.Id}/conversoes", new ConversionData("un", "kg", "3", "Fixture", "Medição sintética", new(2026, 1, 1), "Amostra de três kg por unidade", "Fixture", "Medido"));
            Assert.Equal(HttpStatusCode.Created, conversionResponse.StatusCode); var conversion = (await conversionResponse.Content.ReadFromJsonAsync<CatalogReference>())!;
            var profile = await Factory.ExecuteScopeAsync(async services =>
            {
                var service = services.GetRequiredService<FormulationService>();
                var content = new ProfileData("Perfil sintético de limites", "Fixture", "Sintético", "Fixture", "Fixture", "Amostra sintética", null, null, null, null,
                    [new NutritionValue("PB", "Conhecido", "100", "Medido", "BN", "g/kg", "Sintético", "Fixture")]);
                var draft = await service.CreateProfileAsync(item.Id, content, user.UserId, default);
                return await service.ProfileStateAsync(draft.Id, draft.Revisao, true, user.UserId, default);
            });
            var a = await Local(client); var b = await Local(client);
            var lot = await Created<LoteView>(Post(client, "lotes", LotCommand(item, user.UserId) with { ConversaoItemId = conversion.Id, PerfilNutricionalId = profile.Id, Aplicabilidade = "Mesma amostra sintética" }));
            var received = await Created<MovementReply>(Post(client, "entradas", Command(item, lot, a, user.UserId, "3") with { Unidade = "kg", ConversaoItemId = conversion.Id }));
            Assert.Equal("1", received.Previa.Quantidade.Calculada); Assert.Equal("1", received.Previa.Quantidade.Normalizada); Assert.False(received.Previa.Quantidade.ExigeAceite);
            var transfer = new InventoryMovementCommand(lot.Id, "1", "un", user.UserId, "Transferência sintética", new(item.Revisao, lot.Revisao, OrigemLocal: a.Revisao, DestinoLocal: b.Revisao, OrigemPosicao: 1), OrigemLocalId: a.Id, DestinoLocalId: b.Id);
            var moved = await Created<MovementReply>(Post(client, "transferencias", transfer));
            Assert.Null(moved.Previa.Quantidade.Fator); Assert.Null(moved.Previa.Quantidade.Sentido); Assert.Equal(2, moved.Evento.Movimentos.Count);
            Assert.All(moved.Evento.Movimentos, m => { Assert.Equal(conversion.Id, m.ConversaoItemId); Assert.Equal(profile.Id, m.PerfilNutricionalId); });
            using var snapshot = JsonDocument.Parse(moved.Evento.SnapshotJson);
            Assert.Equal(conversion.Id, snapshot.RootElement.GetProperty("conversao").GetProperty("id").GetGuid());
            Assert.Equal("3", snapshot.RootElement.GetProperty("conversao").GetProperty("fator").GetString());
            Assert.Equal(profile.Id, snapshot.RootElement.GetProperty("perfil").GetProperty("id").GetGuid());
            Assert.NotEmpty(snapshot.RootElement.GetProperty("perfil").GetProperty("conteudoJson").GetString()!);
            Assert.Equal(a.Codigo, snapshot.RootElement.GetProperty("locais")[0].GetProperty("codigo").GetString());
            Assert.Equal(b.Codigo, snapshot.RootElement.GetProperty("locais")[1].GetProperty("codigo").GetString());
            var before = (await client.GetFromJsonAsync<EventoView>($"/api/v1/estoque/movimentos/{moved.Evento.Id}"))!.SnapshotJson;
            using var rename = await Send(client, HttpMethod.Put, $"locais/{a.Id}", new LocalCommand(Guid.NewGuid().ToString("N"), "Nome atual alterado", Finalidade: a.Finalidade, VersaoEsperada: a.Revisao));
            Assert.Equal(HttpStatusCode.OK, rename.StatusCode);
            using var clear = await Post(client, $"lotes/{lot.Id}/referencias", new InventoryStateCommand(lot.Revisao, "Retirada da referência atual", "Documento sintético"));
            Assert.Equal(HttpStatusCode.OK, clear.StatusCode);
            await Factory.ExecuteScopeAsync(async services => { await services.GetRequiredService<FormulationService>().ProfileStateAsync(profile.Id, profile.Revisao, false, user.UserId, default); return 0; });
            var old = (await client.GetFromJsonAsync<EventoView>($"/api/v1/estoque/movimentos/{moved.Evento.Id}"))!;
            Assert.Equal(before, old.SnapshotJson); Assert.All(old.Movimentos, m => Assert.Equal(conversion.Id, m.ConversaoItemId));
            Assert.Equal("1", (await Balance(client, lot.Id, b.Id))!.Quantidade);
        }
    }

    [Fact]
    public async Task Ordinary_receipt_of_expired_material_requires_segregation_and_stays_ineligible()
    {
        var (client, user) = await Session(); using (client)
        {
            var item = await Item(client); var normal = await Local(client); var segregated = await Local(client, "Segregacao");
            var lot = await Created<LoteView>(Post(client, "lotes", LotCommand(item, user.UserId) with { Validade = InventoryRules.Today(TimeProvider.System).AddDays(-1), FonteValidade = "Fixture", ResponsavelValidadeId = user.UserId }));
            Assert.Equal("Bloqueado", lot.Situacao);
            using var ordinary = await Post(client, "entradas", Command(item, lot, normal, user.UserId, "2")); Assert.Equal(HttpStatusCode.Conflict, ordinary.StatusCode);
            await Created<MovementReply>(Post(client, "entradas", Command(item, lot, segregated, user.UserId, "2")));
            var balance = (await Balance(client, lot.Id, segregated.Id))!;
            Assert.Equal("2", balance.Quantidade); Assert.Equal("0", balance.QuantidadeElegivel); Assert.False(balance.Elegivel);
            Assert.Contains(balance.MotivosIndisponibilidade, x => x.Contains("vencid", StringComparison.OrdinalIgnoreCase));
            using var withdrawal = await Post(client, "saidas-manuais", Command(item, lot, segregated, user.UserId, "1", 1)); Assert.Equal(HttpStatusCode.Conflict, withdrawal.StatusCode);
            var events = (await client.GetFromJsonAsync<InventoryPage<EventoView>>($"/api/v1/estoque/movimentos?loteId={lot.Id}"))!;
            Assert.Single(events.Items); Assert.Equal(user.UserId, events.Items[0].AutorId);
        }
    }

    [Fact]
    public async Task Inactive_material_inventory_requires_Admin_and_segregation_but_physical_amount_remains_visible()
    {
        var (admin, author) = await Session(true); var (common, responsible) = await Session(); using (admin) using (common)
        {
            var item = await Item(admin); var normal = await Local(admin); var segregated = await Local(admin, "Segregacao");
            using var inactive = await admin.PatchAsJsonAsync($"/api/v1/itens/{item.Id}/ativo", new StatusCommand(false, item.Revisao));
            Assert.Equal(HttpStatusCode.OK, inactive.StatusCode); item = (await inactive.Content.ReadFromJsonAsync<CatalogItem>())!;
            using var forbidden = await Post(common, "lotes", LotCommand(item, responsible.UserId)); Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
            var lot = await Created<LoteView>(Post(admin, "lotes", LotCommand(item, responsible.UserId)));
            using var ordinary = await Post(admin, "aberturas", Command(item, lot, normal, responsible.UserId, "2")); Assert.Equal(HttpStatusCode.Conflict, ordinary.StatusCode);
            var opened = await Created<MovementReply>(Post(admin, "aberturas", Command(item, lot, segregated, responsible.UserId, "2")));
            Assert.Equal(author.UserId, opened.Evento.AutorId); Assert.Equal(responsible.UserId, opened.Evento.ResponsavelId);
            var balance = (await Balance(common, lot.Id, segregated.Id))!;
            Assert.Equal("2", balance.Quantidade); Assert.Equal("0", balance.QuantidadeElegivel); Assert.False(balance.Elegivel);
            Assert.Contains("Validade não informada", balance.Avisos!);
            using var normalReceipt = await Post(common, "entradas", Command(item, lot, segregated, responsible.UserId, "1", 1)); Assert.Equal(HttpStatusCode.Conflict, normalReceipt.StatusCode);
            Assert.Equal("2", (await Balance(common, lot.Id, segregated.Id))!.Quantidade);
        }
    }

    [Fact]
    public async Task Quantization_acceptance_accepts_equivalent_decimal_strings_in_the_real_database()
    {
        var (client, user) = await Session(); using (client)
        {
            var item = await Item(client); var local = await Local(client); var lot = await Created<LoteView>(Post(client, "lotes", LotCommand(item, user.UserId)));
            var command = Command(item, lot, local, user.UserId, "0.0015") with
            {
                Unidade = "g", AceiteQuantizacao = new("0.0000015000", "0.00000200", "-0.0000005000", "Resolução de seis casas conferida")
            };
            var before = await Counts();
            using var previewResponse = await client.PostAsJsonAsync("/api/v1/estoque/previas", new InventoryPreviewCommand("Entrada", command with { AceiteQuantizacao = null }));
            Assert.True(previewResponse.StatusCode == HttpStatusCode.OK, await previewResponse.Content.ReadAsStringAsync());
            var preview = (await previewResponse.Content.ReadFromJsonAsync<InventoryPreview>())!;
            Assert.True(preview.Quantidade.ExigeAceite); Assert.Equal("0.000002", preview.Quantidade.Normalizada);
            Assert.Equal(before, await Counts()); Assert.Equal("0", (await Balance(client, lot.Id, local.Id))!.Quantidade);
            var key = Guid.NewGuid().ToString("N");
            using var first = await Post(client, "entradas", command, key); Assert.True(first.StatusCode == HttpStatusCode.Created, await first.Content.ReadAsStringAsync());
            var original = await first.Content.ReadAsStringAsync();
            using var replay = await Post(client, "entradas", command with { AceiteQuantizacao = new("0.0000015", "0.000002", "-0.0000005", command.AceiteQuantizacao.Motivo) }, key);
            Assert.Equal(HttpStatusCode.Created, replay.StatusCode); Assert.Equal(original, await replay.Content.ReadAsStringAsync());
            Assert.Equal("true", Assert.Single(replay.Headers.GetValues("Idempotency-Replayed")));
            Assert.Equal("0.000002", (await Balance(client, lot.Id, local.Id))!.Quantidade);
        }
    }

    [Theory]
    [InlineData("Item")]
    [InlineData("Lote")]
    [InlineData("Local")]
    public async Task Inactivation_preserves_stock_denies_ordinary_withdrawal_and_allows_Admin_segregation(string reference)
    {
        var (admin, author) = await Session(true); var (common, responsible) = await Session(); using (admin) using (common)
        {
            var item = await Item(admin); var origin = await Local(admin); var target = await Local(admin, "Segregacao");
            var lot = await Created<LoteView>(Post(admin, "lotes", LotCommand(item, responsible.UserId)));
            await Created<MovementReply>(Post(admin, "aberturas", Command(item, lot, origin, responsible.UserId, "4")));
            if (reference == "Item")
            {
                using var change = await admin.PatchAsJsonAsync($"/api/v1/itens/{item.Id}/ativo", new StatusCommand(false, item.Revisao));
                Assert.Equal(HttpStatusCode.OK, change.StatusCode); item = (await change.Content.ReadFromJsonAsync<CatalogItem>())!;
            }
            else
            {
                var id = reference == "Lote" ? lot.Id : origin.Id; var version = reference == "Lote" ? lot.Revisao : origin.Revisao;
                using var change = await Send(admin, HttpMethod.Patch, $"{(reference == "Lote" ? "lotes" : "locais")}/{id}/ativo", new InventoryStateCommand(version, "Inativação sintética com saldo", "Documento sintético", Ativo: false));
                Assert.Equal(HttpStatusCode.OK, change.StatusCode);
                if (reference == "Lote") lot = InventoryJson.Read<LoteView>(await change.Content.ReadAsStringAsync());
                else origin = InventoryJson.Read<LocalView>(await change.Content.ReadAsStringAsync());
            }
            var physical = (await Balance(common, lot.Id, origin.Id))!; Assert.Equal("4", physical.Quantidade); Assert.False(physical.Elegivel);
            using var ordinary = await Post(common, "saidas-manuais", Command(item, lot, origin, responsible.UserId, "1", 1)); Assert.Equal(HttpStatusCode.Conflict, ordinary.StatusCode);
            var segregate = new InventoryMovementCommand(lot.Id, "4", lot.Unidade, responsible.UserId, "Segregação de material inativo", new(item.Revisao, lot.Revisao, OrigemLocal: origin.Revisao, DestinoLocal: target.Revisao, OrigemPosicao: 1), OrigemLocalId: origin.Id, DestinoLocalId: target.Id, Evidencia: "Conferência sintética");
            var moved = await Created<MovementReply>(Post(admin, "segregacoes", segregate)); Assert.Equal(author.UserId, moved.Evento.AutorId);
            Assert.Equal("0", (await Balance(common, lot.Id, origin.Id))!.Quantidade);
            var segregated = (await Balance(common, lot.Id, target.Id))!; Assert.Equal("4", segregated.Quantidade); Assert.False(segregated.Elegivel);
            Assert.Equal("Bloqueado", (await common.GetFromJsonAsync<LoteView>($"/api/v1/estoque/lotes/{lot.Id}"))!.Situacao);
        }
    }

    private sealed record CatalogItem(Guid Id, int Revisao, bool UnidadeFixada);
    private sealed record CatalogReference(Guid Id);
    private sealed record MovementReply(EventoView Evento, InventoryPreview Previa);
    private Task<(int Events, int Movements, int Commands, int Positions, int History)> Counts() => Factory.ExecuteDbContextAsync(async db =>
        (await db.Set<EventoEstoque>().CountAsync(), await db.Set<MovimentoEstoque>().CountAsync(), await db.Set<ComandoEstoque>().CountAsync(), await db.Set<PosicaoEstoque>().CountAsync(), await db.Set<HistoricoEstoque>().CountAsync()));
}
