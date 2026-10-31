using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GenSW.Application.EggBridge;
using GenSW.Application.Formulation;
using GenSW.Application.Inventory;
using GenSW.Domain.Animals;
using GenSW.Domain.Catalog;
using GenSW.Domain.EggBridge;
using GenSW.Domain.Inventory;
using GenSW.Domain.Species;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GenSW.API.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class EggBridgePostgreSqlCollection : ICollectionFixture<AnimalApiPostgreSqlFixture>
{ public const string Name = "Isolated egg bridge PostgreSQL"; }

[Collection(EggBridgePostgreSqlCollection.Name)]
#pragma warning disable EF1002 // Fixture-only trigger identifiers are generated UUIDs.
public sealed partial class EggBridgeApiTests(AnimalApiPostgreSqlFixture fixture)
{
    private AuthWebApplicationFactory Factory => fixture.Factory;
    private const string Root = "/api/v1/producao/ponte-ovos/";
    private const string Evidence = "Conferência sintética isolada; sem ovos ou saldos operacionais";
    private static string Unique() => Guid.NewGuid().ToString("N");
    private sealed record Material(Item Item, LoteView Lot, LocalView Local);

    private async Task<(HttpClient Client, SeededUser User)> Session(bool admin = false)
    {
        var user = await Factory.SeedUserAsync("f03_" + Unique(), roles: admin ? ["Admin"] : []);
        var client = Factory.CreateHttpsClient();
        using var login = await client.LoginAsync(user.UserName, user.Password);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.ReadAccessTokenAsync()).AccessToken);
        return (client, user);
    }
    private static async Task<HttpResponseMessage> Post(HttpClient client, string path, object command, string? key = null, bool inventory = false)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, (inventory ? "/api/v1/estoque/" : Root) + path) { Content = JsonContent.Create(command) };
        request.Headers.Add("Idempotency-Key", key ?? Unique());
        return await client.SendAsync(request);
    }
    private static async Task<T> Success<T>(HttpResponseMessage response, HttpStatusCode? expected = null)
    {
        using (response)
        {
            var text = await response.Content.ReadAsStringAsync();
            Assert.True(expected is null ? response.IsSuccessStatusCode : response.StatusCode == expected,
                $"Expected {expected?.ToString() ?? "success"}, got {(int)response.StatusCode}: {text}");
            return InventoryJson.Read<T>(text);
        }
    }
    private static async Task<string> Error(HttpResponseMessage response, HttpStatusCode expected)
    {
        using (response)
        {
            var text = await response.Content.ReadAsStringAsync();
            Assert.True(response.StatusCode == expected, $"Expected {(int)expected}, got {(int)response.StatusCode}: {text}");
            return text;
        }
    }
    private async Task<Material> Stock(HttpClient client, Guid actor, string quantity = "0", string unit = "un")
    {
        var item = await Factory.ExecuteScopeAsync(s => s.GetRequiredService<FormulationService>().CreateItemAsync(
            new(Unique(), "Material F03 sintético", null, null, "Alimentar", unit, true, true, true, false), actor, default));
        var local = await Success<LocalView>(await Post(client, "locais", new LocalCommand(Unique(), "Local sintético F03"), inventory: true));
        var lot = await Success<LoteView>(await Post(client, "lotes", new LoteCommand(item.Id, Unique(), "Inventário sintético", Evidence, actor, ItemVersaoEsperada: item.Revisao), inventory: true));
        var material = new Material(item, lot, local);
        if (quantity != "0") await Success<JsonElement>(await Post(client, "entradas", Movement(material, actor, quantity, 0), inventory: true));
        return material;
    }
    private static InventoryMovementCommand Movement(Material m, Guid actor, string quantity, int position) => new(m.Lot.Id, quantity, m.Lot.Unidade, actor,
        Evidence, new(m.Item.Revisao, m.Lot.Revisao, m.Local.Revisao, position), LocalId: m.Local.Id,
        DataObservada: InventoryRules.Today(TimeProvider.System), Origem: m.Lot.Origem, Fonte: m.Lot.Fonte, Destino: Evidence, Evidencia: Evidence);
    private async Task<ProducaoOvo[]> Eggs(int count)
    {
        return await Factory.ExecuteDbContextAsync(async db =>
        {
            var now = DateTimeOffset.UtcNow; var today = InventoryRules.Today(TimeProvider.System);
            var species = Especie.Criar("Espécie sintética F03 " + Unique(), null, true, 10m, now);
            var animal = Animal.Criar(Unique(), "Fêmea sintética", species.Id, null, null, SexoAnimal.Femea, null, EscopoAnimal.Operacional, today, now);
            var eggs = Enumerable.Range(0, count).Select(i => ProducaoOvo.Criar(animal.Id, today, 10m + i, "Registro zootécnico sintético", now)).ToArray();
            db.AddRange(species, animal); db.AddRange(eggs); await db.SaveChangesAsync();
            return eggs;
        });
    }
    private Task<Guid> CycleId() => Factory.ExecuteDbContextAsync(async db =>
    {
        var now = DateTimeOffset.UtcNow; var today = InventoryRules.Today(TimeProvider.System);
        var species = Especie.Criar("Espécie de ciclo sintético " + Unique(), null, true, 10m, now);
        var father = Animal.Criar(Unique(), "Pai sintético", species.Id, null, null, SexoAnimal.Macho, null, EscopoAnimal.Operacional, today, now);
        var mother = Animal.Criar(Unique(), "Mãe sintética", species.Id, null, null, SexoAnimal.Femea, null, EscopoAnimal.Operacional, today, now);
        var breeding = Cruzamento.Criar(father.Id, mother.Id, StatusCruzamento.Concluido, today, today, Evidence, null, now);
        var cycle = CicloReprodutivo.Criar(breeding.Id, TipoCicloReprodutivo.Oviparo, StatusCicloReprodutivo.Concluido,
            today, today, today, 1, 1, 1, 1, 0, 10m, null, null, null, null, null, null, null, Evidence, now);
        db.AddRange(species, father, mother, breeding, cycle); await db.SaveChangesAsync(); return cycle.Id;
    });
    private async Task<RecorteConciliacaoOvos> Scope(HttpClient client, Material m, Guid actor, IReadOnlyList<ProducaoOvo> eggs,
        string decision = "NaoRecebidoComprovado", bool individual = true, bool enroll = true, IReadOnlyDictionary<Guid, string>? decisions = null)
    {
        if (enroll) await Success<JsonElement>(await Post(client, "itens", new EggEnrollmentCommand(m.Item.Id, Evidence)));
        var result = await Success<JsonElement>(await Post(client, "recortes", new EggScopeCommand(m.Item.Id,
            "Grupo sintético selecionado; demais origens excluídas", "Contagem e documento sintéticos, inclusive acervo externo", "Nenhuma na fixture", actor, Evidence,
            eggs.Select(e => new EggDecisionCommand(e.Id, decisions is not null && decisions.TryGetValue(e.Id, out var value) ? value : decision, individual, Evidence)).ToArray())));
        return InventoryJson.Read<RecorteConciliacaoOvos>(result.GetProperty("recorte").GetRawText());
    }
    private static EggPreviewCommand Entry(Material m, Guid scope, Guid actor, params ProducaoOvo[] eggs) => new("Entrada", scope, actor,
        Evidence, Evidence, eggs.Select(e => new EggSelection(e.Id)).ToArray(), m.Lot.Id, m.Local.Id);
    private static EggPreviewCommand Association(Guid scope, Guid actor, Guid proof, IReadOnlyList<ProducaoOvo> eggs, long firstOrdinal) => new("Associacao", scope, actor,
        Evidence, Evidence, eggs.Select((e, i) => new EggSelection(e.Id, (firstOrdinal + i).ToString(System.Globalization.CultureInfo.InvariantCulture))).ToArray(), ProvaId: proof);
    private Task<EggPreview> Preview(HttpClient client, EggPreviewCommand command) => PreviewCore(client, command);
    private static async Task<EggPreview> PreviewCore(HttpClient client, EggPreviewCommand command) =>
        await Success<EggPreview>(await Post(client, "previas", command), HttpStatusCode.OK);
    private static async Task<EggBridgeDetail> Confirm(HttpClient client, EggPreview preview, string? key = null) =>
        await Success<EggBridgeDetail>(await Post(client, "confirmacoes", new EggConfirmCommand(preview.Token), key));
    private static Task<SaldoView?> Balance(HttpClient client, Material m) => client.GetFromJsonAsync<SaldoView>($"/api/v1/estoque/saldos/{m.Lot.Id}/{m.Local.Id}");
    private Task<string> Snapshot() => Factory.ExecuteDbContextAsync(async db => JsonSerializer.Serialize(await FormulationMigrationTests.RowsAsync(db,
        ["LotesMateriais", "EventosEstoque", "MovimentosEstoque", "PosicoesEstoque", "GruposPonteOvos", "PontesProducaoOvos", "AlocacoesProvaOvos", "ComandosPonteOvos", "TratamentosPonteOvos", "AncorasConciliacaoOvos"])));
}
