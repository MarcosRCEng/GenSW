using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GenSW.Application.Formulation;
using GenSW.Domain.Catalog;
using GenSW.Domain.Formulation;
using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace GenSW.API.Tests;

[Collection(PostgreSqlAnimalIntegrationCollection.Name)]
public sealed class FormulationApiTests(AnimalApiPostgreSqlFixture fixture)
{
    private AuthWebApplicationFactory Factory => fixture.Factory;
    private static readonly Guid Actor = Guid.NewGuid();
    private Task<T> Service<T>(Func<FormulationService, Task<T>> action) => Factory.ExecuteScopeAsync(s => action(s.GetRequiredService<FormulationService>()));
    private static ItemData ItemData(string? code = null, string unit = "kg", string material = "Alimentar") => new(code ?? Guid.NewGuid().ToString("N"), "Fixture sintética", null, null, material, unit, true, true, true, false);
    private static NutritionValue Value(string code, string? number, string unit = "g/kg", string context = "") => new(code, number is null ? "Desconhecido" : "Conhecido", number, number is null ? null : "Declarado", "BN", unit, "Sintetico", context, Motivo: number is null ? "Lacuna sintética" : null);
    private static ProfileData Profile(string protein = "100", string moisture = "100", string energy = "12", string fiber = "20") => new("Perfil sintético", "Fixture de teste", "Sintético", "Fixture", "Amostra sintética", "Teste isolado", null, null, null, null, [Value("PB", protein), Value("UMIDADE", moisture), Value("EM", energy, "MJ/kg", "Sintetico"), Value("FB", fiber)]);
    private static RecipeData RecipeData(Guid a, Guid b, Guid output, Guid? pa, Guid? pb) => new("MisturaSimples", "Quantidade", "100", "kg",
        [new(Guid.NewGuid(), a, "Alimentar", "60", "kg", "Variavel", pa), new(Guid.NewGuid(), b, "Alimentar", "40", "kg", "Variavel", pb)], [new(Guid.NewGuid(), output, "Mistura sintética", "100", "kg", "Variavel", true)], [], [], "Fixture sintética; não operacional", []);
    private async Task<(Item A, Item B, Item Output, ProfileView PA, ProfileView PB, Recipe Recipe, RecipeVersionView Version)> FixtureAsync()
    {
        var a = await Service(s => s.CreateItemAsync(ItemData(), Actor, default)); var b = await Service(s => s.CreateItemAsync(ItemData(), Actor, default)); var output = await Service(s => s.CreateItemAsync(ItemData(), Actor, default));
        var pa = await Service(s => s.CreateProfileAsync(a.Id, Profile(), Actor, default)); pa = await Service(s => s.ProfileStateAsync(pa.Id, 1, true, Actor, default));
        var pb = await Service(s => s.CreateProfileAsync(b.Id, Profile("400", "200", "8", "80"), Actor, default)); pb = await Service(s => s.ProfileStateAsync(pb.Id, 1, true, Actor, default));
        var r = await Service(s => s.CreateRecipeAsync(new(Guid.NewGuid().ToString("N"), "Fixture F1", "Teste sintético isolado"), Actor, default));
        var v = await Service(s => s.CreateVersionAsync(r.Id, RecipeData(a.Id, b.Id, output.Id, pa.Id, pb.Id), Actor, default)); v = await Service(s => s.VersionStateAsync(v.Id, 1, true, Actor, default));
        return (a, b, output, pa, pb, r, v);
    }
    private static SimulationCommand Command(Guid version, RecipeData? variation = null) => new(version, "100", "kg", null, null, [new("PB", "Sintetico", "", "BN", "g/kg", "250", null, "InformadaUsuario", null)], variation);
    private async Task<HttpResponseMessage[]> RaceAsync(Func<Task<HttpResponseMessage>> first, Func<Task<HttpResponseMessage>> second)
    {
        var locked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var holder = Factory.ExecuteDbContextAsync(async db => { await using var tx = await db.Database.BeginTransactionAsync(); await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(413, 421)"); locked.SetResult(); await release.Task.WaitAsync(TimeSpan.FromSeconds(20)); await tx.CommitAsync(); });
        Task<HttpResponseMessage>? one = null, two = null;
        try
        {
            await locked.Task.WaitAsync(TimeSpan.FromSeconds(10)); one = first(); two = second();
            var timeout = DateTimeOffset.UtcNow.AddSeconds(4); bool blocked = false;
            while (DateTimeOffset.UtcNow < timeout)
            {
                var count = await Factory.ExecuteDbContextAsync(async db => { await db.Database.OpenConnectionAsync(); await using var query = db.Database.GetDbConnection().CreateCommand(); query.CommandText = "SELECT count(*)::integer FROM pg_stat_activity WHERE datname=current_database() AND wait_event='advisory' AND query LIKE '%413, 421%';"; return (int)(await query.ExecuteScalarAsync())!; });
                if (count >= 2) { blocked = true; break; }
                if (one.IsCompleted || two.IsCompleted) break; await Task.Delay(20);
            }
            Assert.True(blocked, "Both real PostgreSQL sessions must be blocked before release. " + (one.IsCompleted ? await (await one).Content.ReadAsStringAsync() : ""));
        }
        finally { release.TrySetResult(); await holder; }
        return await Task.WhenAll(one!, two!).WaitAsync(TimeSpan.FromSeconds(15));
    }
    [Fact]
    public async Task Authenticated_catalog_search_escaping_pagination_status_revision_and_history()
    {
        using var anonymous = Factory.CreateHttpsClient(); Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/itens")).StatusCode);
        using var client = await PropertyTestHttp.AuthenticateAsync(Factory);
        var prefix = Guid.NewGuid().ToString("N");
        for (int i = 0; i < 3; i++) { using var r = await client.PostAsJsonAsync("/api/v1/itens", ItemData(prefix + "%_" + i)); Assert.True(r.StatusCode == HttpStatusCode.Created, await r.Content.ReadAsStringAsync()); }
        using var list = await client.GetAsync($"/api/v1/itens?search={Uri.EscapeDataString(prefix + "%_")}&page=2&pageSize=2"); Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var json = await list.Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal(3, json.GetProperty("totalItems").GetInt32()); Assert.Single(json.GetProperty("items").EnumerateArray());
        var item = JsonSerializer.Deserialize<ItemData>(json.GetProperty("items")[0], FormulationJson.Options)!;
        var id = json.GetProperty("items")[0].GetProperty("id").GetGuid();
        using var stale = await client.PutAsJsonAsync($"/api/v1/itens/{id}", new ItemCommand(item, 0)); Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var status = await client.PatchAsJsonAsync($"/api/v1/itens/{id}/ativo", new StatusCommand(false, 1)); Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/itens/{id}")).StatusCode);
        var history = await client.GetFromJsonAsync<CatalogPage<CatalogAudit>>($"/api/v1/itens/{id}/historico"); Assert.Equal(2, history!.TotalItems);
        Assert.All(history.Items, h => Assert.NotEqual(Guid.Empty, h.AutorId));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/itens?pageSize=101")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/itens/{Guid.NewGuid()}")).StatusCode);
    }
    [Fact]
    public async Task Concurrent_case_insensitive_codes_are_unique_in_PostgreSQL()
    {
        using var client = await PropertyTestHttp.AuthenticateAsync(Factory); var code = Guid.NewGuid().ToString("N");
        var responses = await RaceAsync(() => client.PostAsJsonAsync("/api/v1/itens", ItemData(code)), () => client.PostAsJsonAsync("/api/v1/itens", ItemData(code.ToUpperInvariant())));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created); Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict); foreach (var r in responses) r.Dispose();
        var count = await Factory.ExecuteDbContextAsync(db => db.Set<Item>().CountAsync(x => x.CodigoNormalizado == code.ToUpperInvariant())); Assert.Equal(1, count);
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Concurrent_profile_and_recipe_publications_return_one_stale_conflict(bool profile)
    {
        var f = await FixtureAsync(); using var client = await PropertyTestHttp.AuthenticateAsync(Factory);
        Guid id; string path;
        if (profile) { var p = await Service(s => s.CreateProfileAsync(f.A.Id, Profile(), Actor, default)); id = p.Id; path = "perfis-nutricionais"; }
        else { var v = await Service(s => s.CreateVersionAsync(f.Recipe.Id, f.Version.Conteudo, Actor, default)); id = v.Id; path = "receitas/versoes"; }
        var responses = await RaceAsync(() => client.PostAsJsonAsync($"/api/v1/{path}/{id}/publicacao", new VersionCommand(1)), () => client.PostAsJsonAsync($"/api/v1/{path}/{id}/publicacao", new VersionCommand(1)));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK); Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict); foreach (var r in responses) r.Dispose();
    }
    [Fact]
    public async Task Idempotent_simulation_race_replays_same_snapshot_and_rejects_changed_payload()
    {
        var f = await FixtureAsync(); using var client = await PropertyTestHttp.AuthenticateAsync(Factory); client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N")); var command = Command(f.Version.Id);
        var responses = await RaceAsync(() => client.PostAsJsonAsync("/api/v1/simulacoes-formulacao", command), () => client.PostAsJsonAsync("/api/v1/simulacoes-formulacao", command with { Tamanho = "100.000000" }));
        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        var first = (await responses[0].Content.ReadFromJsonAsync<SimulationView>())!; var second = (await responses[1].Content.ReadFromJsonAsync<SimulationView>())!;
        Assert.Equal(first.Id, second.Id); Assert.Equal("220", first.Conteudo.Resultado.Componentes.Single(x => x.Componente == "PB").ValorBN); foreach (var r in responses) r.Dispose();
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/v1/simulacoes-formulacao", command with { Tamanho = "200" })).StatusCode);
        Assert.Equal(1, await Factory.ExecuteDbContextAsync(db => db.Set<FormulationSnapshot>().CountAsync(x => x.Id == first.Id)));
        client.DefaultRequestHeaders.Remove("Idempotency-Key"); client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        var documented = command with { Variacao = f.Version.Conteudo with { Observacao = "100" } };
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/v1/simulacoes-formulacao", documented)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/v1/simulacoes-formulacao", documented with { Variacao = documented.Variacao! with { Observacao = "100.0" } })).StatusCode);
    }
    [Fact]
    public async Task Snapshot_and_comparison_remain_immutable_after_reference_revisions_and_inactivation()
    {
        var f = await FixtureAsync(); var first = await Service(s => s.SimulateAsync(Command(f.Version.Id), Guid.NewGuid().ToString(), Actor, default));
        var f2 = f.Version.Conteudo with { Entradas = [f.Version.Conteudo.Entradas[0] with { Quantidade = "30" }, f.Version.Conteudo.Entradas[1] with { Quantidade = "70" }] };
        var second = await Service(s => s.SimulateAsync(Command(f.Version.Id, f2), Guid.NewGuid().ToString(), Actor, default));
        Assert.Equal("310", second.Conteudo.Resultado.Componentes.Single(x => x.Componente == "PB").ValorBN);
        var compare = await Service(s => s.CompareAsync(new([first.Id, second.Id], "MS"), Guid.NewGuid().ToString(), Actor, default)); Assert.All(compare.Conteudo.Linhas, row => Assert.True(row.Comparavel));
        var before = FormulationJson.Write(await Service(s => s.GetSimulationAsync(first.Id, default)));
        await Service(s => s.CreateProfileAsync(f.A.Id, Profile("200"), Actor, default));
        await Service(s => s.UpdateItemAsync(f.A.Id, new(f.A.Data() with { Nome = "Renomeado" }, 1), Actor, default));
        await Service(s => s.ItemStatusAsync(f.A.Id, new(false, 2), Actor, default));
        await Service(s => s.ProfileStateAsync(f.PA.Id, 2, false, Actor, default));
        Assert.Equal(before, FormulationJson.Write(await Service(s => s.GetSimulationAsync(first.Id, default))));
        var rejected = await Assert.ThrowsAsync<FormulationException>(() => Service(s => s.SimulateAsync(Command(f.Version.Id), Guid.NewGuid().ToString(), Actor, default))); Assert.Equal(409, rejected.Status);
        Assert.Equal(compare.Id, (await Service(s => s.GetComparisonAsync(compare.Id, default))).Id);
        var exception = await Assert.ThrowsAsync<PostgresException>(() => Factory.ExecuteDbContextAsync(db => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"SnapshotsFormulacao\" SET \"Hash\"='changed' WHERE \"Id\"={first.Id}"))); Assert.Equal("23514", exception.SqlState);
    }
    [Fact]
    public async Task Failed_audit_rolls_back_snapshot_and_new_version_atomically()
    {
        var f = await FixtureAsync(); var key = Guid.NewGuid().ToString(); var before = await Service(s => s.VersionsAsync(f.Recipe.Id, new(), default));
        await Factory.ExecuteDbContextAsync(db => db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION "FormulationTestRejectAudit"() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'Injected isolated test failure' USING ERRCODE='P0001'; END; $$;
            CREATE TRIGGER "TR_FormulationTestRejectAudit" BEFORE INSERT ON "HistoricoFormulacao" FOR EACH ROW EXECUTE FUNCTION "FormulationTestRejectAudit"();
            """));
        try
        {
            await Assert.ThrowsAsync<DbUpdateException>(() => Service(s => s.SimulateAsync(Command(f.Version.Id), key, Actor, default)));
            await Assert.ThrowsAsync<DbUpdateException>(() => Service(s => s.CreateVersionAsync(f.Recipe.Id, f.Version.Conteudo, Actor, default)));
            Assert.Equal(0, await Factory.ExecuteDbContextAsync(db => db.Set<FormulationSnapshot>().CountAsync(x => x.Chave == key)));
            Assert.Equal(before.TotalItems, (await Service(s => s.VersionsAsync(f.Recipe.Id, new(), default))).TotalItems);
        }
        finally { await Factory.ExecuteDbContextAsync(db => db.Database.ExecuteSqlRawAsync("DROP TRIGGER \"TR_FormulationTestRejectAudit\" ON \"HistoricoFormulacao\"; DROP FUNCTION \"FormulationTestRejectAudit\"();")); }
    }
    [Fact]
    public async Task Sub_recipe_expands_once_and_same_recipe_transitive_cycle_is_rejected()
    {
        var f = await FixtureAsync(); var parent = await Service(s => s.CreateRecipeAsync(new(Guid.NewGuid().ToString("N"), "Pai", "Teste"), Actor, default));
        var d = f.Version.Conteudo with { Entradas = [new(Guid.NewGuid(), f.Output.Id, "Alimentar", "100", "kg", "Variavel", SubReceitaVersaoId: f.Version.Id, SubSaidaId: f.Version.Conteudo.Saidas[0].Id)] };
        var v = await Service(s => s.CreateVersionAsync(parent.Id, d, Actor, default)); v = await Service(s => s.VersionStateAsync(v.Id, 1, true, Actor, default));
        var sim = await Service(s => s.SimulateAsync(Command(v.Id), Guid.NewGuid().ToString(), Actor, default)); Assert.Equal("100", sim.Conteudo.Resultado.MassaKg); Assert.Equal(2, sim.Conteudo.Linhas.Count); Assert.Equal("220", sim.Conteudo.Resultado.Componentes.Single(x => x.Componente == "PB").ValorBN);
        var cycleData = d with { Entradas = [d.Entradas[0] with { SubReceitaVersaoId = v.Id, SubSaidaId = v.Conteudo.Saidas[0].Id }] };
        var cycle = await Service(s => s.CreateVersionAsync(f.Recipe.Id, cycleData, Actor, default));
        await Assert.ThrowsAsync<ArgumentException>(() => Service(s => s.VersionStateAsync(cycle.Id, 1, true, Actor, default)));
        Assert.Equal("Rascunho", (await Service(s => s.GetVersionAsync(cycle.Id, default))).Estado);
    }
    [Fact]
    public async Task Processing_100_to_90_needs_explicit_retention_and_output_MS()
    {
        var f = await FixtureAsync(); var d = f.Version.Conteudo with { Tipo = "Processamento", Entradas = [f.Version.Conteudo.Entradas[0] with { Quantidade = "100" }], Saidas = [f.Version.Conteudo.Saidas[0] with { Quantidade = "90" }], Perdas = [new("10", "kg", "Perda hipotética de água")] };
        var recipe = await Service(s => s.CreateRecipeAsync(new(Guid.NewGuid().ToString("N"), "Secagem", "Teste"), Actor, default));
        var v = await Service(s => s.CreateVersionAsync(recipe.Id, d, Actor, default)); v = await Service(s => s.VersionStateAsync(v.Id, 1, true, Actor, default));
        var unknown = await Service(s => s.SimulateAsync(Command(v.Id), Guid.NewGuid().ToString(), Actor, default)); Assert.Null(unknown.Conteudo.Resultado.Componentes.Single(x => x.Componente == "PB").ValorBN);
        var retained = d with { Retencoes = [new("PB", "1", "Fixture: PB retida integralmente"), new("MS", "1", "Fixture: perde somente água")] };
        var estimate = await Service(s => s.SimulateAsync(Command(v.Id, retained), Guid.NewGuid().ToString(), Actor, default));
        var pb = estimate.Conteudo.Resultado.Componentes.Single(x => x.Componente == "PB"); Assert.True(pb.Estimado); Assert.StartsWith("111.111111", pb.ValorBN); Assert.Equal("90", estimate.Conteudo.Resultado.MateriaSecaKg); Assert.Equal("0", estimate.Conteudo.BalancoKg);
    }
    [Fact]
    public async Task Inclusion_limit_proves_local_maximum_160_without_claiming_global_infeasibility()
    {
        var f = await FixtureAsync(); var d = f.Version.Conteudo with { Entradas = [f.Version.Conteudo.Entradas[0], f.Version.Conteudo.Entradas[1] with { InclusaoMaxima = "20" }] };
        var sim = await Service(s => s.SimulateAsync(Command(f.Version.Id, d), Guid.NewGuid().ToString(), Actor, default));
        Assert.True(sim.Conteudo.Resultado.Metas[0].ImpossibilidadeLocal);
        Assert.Contains("160", sim.Conteudo.Resultado.Metas[0].Motivo);
        Assert.Contains(sim.Conteudo.Problemas, x => x.Contains("fora dos limites"));
    }
    [Fact]
    public async Task Transitive_depth_and_expanded_line_limits_leave_publication_draft()
    {
        var f = await FixtureAsync(); var last = f.Version;
        for (var depth = 2; depth <= 11; depth++)
        {
            var recipe = await Service(s => s.CreateRecipeAsync(new(Guid.NewGuid().ToString("N"), "Profundidade sintética", "Teste"), Actor, default));
            var d = f.Version.Conteudo with { Entradas = [new(Guid.NewGuid(), f.Output.Id, "Alimentar", "100", "kg", "Variavel", SubReceitaVersaoId: last.Id, SubSaidaId: last.Conteudo.Saidas[0].Id)] };
            var v = await Service(s => s.CreateVersionAsync(recipe.Id, d, Actor, default));
            if (depth <= 10) last = await Service(s => s.VersionStateAsync(v.Id, 1, true, Actor, default));
            else { var error = await Assert.ThrowsAsync<ArgumentException>(() => Service(s => s.VersionStateAsync(v.Id, 1, true, Actor, default))); Assert.Contains("Profundidade", error.Message); Assert.Equal("Rascunho", (await Service(s => s.GetVersionAsync(v.Id, default))).Estado); }
        }
        var wideRecipe = await Service(s => s.CreateRecipeAsync(new(Guid.NewGuid().ToString("N"), "Expansão sintética", "Teste"), Actor, default));
        var wide = f.Version.Conteudo with { Entradas = Enumerable.Range(0, 200).Select(_ => new RecipeInput(Guid.NewGuid(), f.Output.Id, "Alimentar", "0.5", "kg", "Variavel", SubReceitaVersaoId: f.Version.Id, SubSaidaId: f.Version.Conteudo.Saidas[0].Id)).ToArray() };
        var wideVersion = await Service(s => s.CreateVersionAsync(wideRecipe.Id, wide, Actor, default));
        var limit = await Assert.ThrowsAsync<ArgumentException>(() => Service(s => s.VersionStateAsync(wideVersion.Id, 1, true, Actor, default))); Assert.Contains("500", limit.Message);
    }
    [Fact]
    public async Task Contextual_conversions_are_strings_and_snapshotted_without_hidden_density()
    {
        using var client = await PropertyTestHttp.AuthenticateAsync(Factory);
        var item = await Service(s => s.CreateItemAsync(ItemData(unit: "L"), Actor, default));
        using var response = await client.PostAsJsonAsync($"/api/v1/itens/{item.Id}/conversoes", new ConversionData("L", "kg", "0.8", "Fixture", "Medição", new(2026, 1, 1), "Condição sintética", "Amostra", "Medido"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode); var json = await response.Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal(JsonValueKind.String, json.GetProperty("fator").ValueKind);
        await Assert.ThrowsAsync<CatalogConflictException>(() => Service(s => s.UpdateItemAsync(item.Id, new(item.Data() with { Unidade = "kg" }, 1), Actor, default)));
    }
}
