using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GenSW.Application.Formulation;
using GenSW.Application.Optimization;
using GenSW.Domain.Catalog;
using GenSW.Domain.Formulation;
using GenSW.Domain.Optimization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Xunit;

namespace GenSW.API.Tests;

[Collection(PostgreSqlAnimalIntegrationCollection.Name)]
public sealed class OptimizationApiTests(AnimalApiPostgreSqlFixture fixture)
{
    private const string Route = "/api/v1/otimizacoes-formulacao";
    private AuthWebApplicationFactory Factory => fixture.Factory;

    [Fact]
    public async Task S05_01_25_26_27_replay_and_explicit_draft_preserve_published_recipe_and_sources()
    {
        using var client = await PropertyTestHttp.AuthenticateAsync(Factory);
        var data = await SeedAsync(imported: true);
        var key = Key();
        using var first = await SendAsync(client, Route, data.Command, key);
        var solution = await ReadAsync(first, HttpStatusCode.Created);
        var id = solution.GetProperty("id").GetGuid();
        Assert.Equal("Finalizada", solution.GetProperty("estado").GetString());
        Assert.Equal("SolucaoViavel", Outcome(solution));
        Assert.NotNull(first.Headers.Location);
        var candidate = solution.GetProperty("resultado").GetProperty("motor").GetProperty("candidate");
        var masses = candidate.GetProperty("quantizedMasses");
        foreach (var line in data.Command.Candidatos)
            Assert.Equal(50m, ReadNumber(masses.GetProperty(line.Id.ToString())));
        Assert.Equal(20m, ReadNumber(candidate.GetProperty("quantizedDeviationKg")));
        Assert.Equal(1, await VersionCount(data.Recipe.Id));

        using var replay = await SendAsync(client, Route, data.Command with
        {
            MassaKg = "100.000000",
            Candidatos = data.Command.Candidatos.Reverse().ToArray(),
        }, key);
        Assert.Equal(solution.GetRawText(), (await ReadAsync(replay, HttpStatusCode.Created)).GetRawText());
        using var conflict = await SendAsync(client, Route, data.Command with { MassaKg = "101" }, key);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);

        var target = new OptimizationDraftDestination(data.Output.Id, data.Output.Revisao, data.Recipe.Id, data.Recipe.Revisao);
        using var preview = await client.PostAsJsonAsync($"{Route}/{id}/previas-rascunho", target);
        var hash = (await ReadAsync(preview, HttpStatusCode.OK)).GetProperty("hash").GetString()!;
        Assert.Equal(1, await VersionCount(data.Recipe.Id));
        var draft = new OptimizationDraftCommand(target, hash, true, true);
        using var refused = await SendAsync(client, $"{Route}/{id}/rascunhos", draft with { AceitaAvisos = false }, Key());
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        var draftKey = Key();
        await Factory.ExecuteDbContextAsync(db => db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION gensw_test_f05_rollback() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'Falha sintética de derivação' USING ERRCODE = '23514'; END;
            $$;
            CREATE TRIGGER "TR_TestF05Rollback" BEFORE INSERT ON "DerivacoesOtimizacao"
                FOR EACH ROW EXECUTE FUNCTION gensw_test_f05_rollback();
            """));
        try
        {
            using var failed = await SendAsync(client, $"{Route}/{id}/rascunhos", draft, draftKey);
            Assert.Equal(HttpStatusCode.BadRequest, failed.StatusCode);
            Assert.Equal(1, await VersionCount(data.Recipe.Id));
            Assert.False(await Factory.ExecuteDbContextAsync(db => db.Set<OptimizationDerivation>().AnyAsync(x => x.ExecutionId == id)));
        }
        finally
        {
            await Factory.ExecuteDbContextAsync(db => db.Database.ExecuteSqlRawAsync("""
                DROP TRIGGER "TR_TestF05Rollback" ON "DerivacoesOtimizacao";
                DROP FUNCTION gensw_test_f05_rollback();
                """));
        }
        using var created = await SendAsync(client, $"{Route}/{id}/rascunhos", draft, draftKey);
        var receipt = await ReadAsync(created, HttpStatusCode.Created);
        var draftId = receipt.GetProperty("versaoId").GetGuid();
        using var repeatedDraft = await SendAsync(client, $"{Route}/{id}/rascunhos", draft, draftKey);
        Assert.Equal(receipt.GetRawText(), (await ReadAsync(repeatedDraft, HttpStatusCode.Created)).GetRawText());
        Assert.Equal(2, await VersionCount(data.Recipe.Id));
        Assert.Equal("Rascunho", (await Catalog(s => s.GetVersionAsync(draftId, default))).Estado);
        Assert.Equal(FormulationJson.Write(data.Baseline),
            FormulationJson.Write(await Catalog(s => s.GetVersionAsync(data.Baseline.Id, default))));

        var profile = data.ProfileA;
        await Catalog(s => s.ProfileStateAsync(profile.Id, profile.Revisao, false, Guid.NewGuid(), default));
        using var historical = await client.GetAsync($"{Route}/por-chave?chave={key}");
        Assert.Equal(solution.GetRawText(), (await ReadAsync(historical, HttpStatusCode.OK)).GetRawText());
        using var obsolete = await client.PostAsJsonAsync($"{Route}/{id}/previas-rascunho", target);
        Assert.Equal(HttpStatusCode.Conflict, obsolete.StatusCode);
        using var list = await client.GetAsync($"{Route}?desfecho=SolucaoViavel&pageSize=100");
        Assert.Contains((await ReadAsync(list, HttpStatusCode.OK)).GetProperty("items").EnumerateArray(),
            row => row.GetProperty("id").GetGuid() == id);
    }

    [Theory]
    [InlineData("inviavel", "InviabilidadeDemonstrada")]
    [InlineData("incompleto", "DadosIncompletos")]
    [InlineData("estimado", "DadosIncompletos")]
    [InlineData("estimado-aceito", "SolucaoViavel")]
    [InlineData("nao-aplicavel", "DadosIncompativeis")]
    [InlineData("minimo", "DadosIncompletos")]
    [InlineData("normalizacao-ms", "SolucaoViavel")]
    [InlineData("ms-ausente", "DadosIncompletos")]
    [InlineData("modalidade", "DadosIncompativeis")]
    [InlineData("meta-ms", "DadosIncompativeis")]
    [InlineData("preparo", "DadosIncompativeis")]
    [InlineData("economico", "DadosIncompativeis")]
    [InlineData("precisao", "Inconclusivo")]
    public async Task S05_04_09_12_13_17_18_terminal_outcomes_are_persisted_and_only_validated_solutions_derive(
        string scenario, string expected)
    {
        using var client = await PropertyTestHttp.AuthenticateAsync(Factory);
        var data = await SeedAsync(proteinState: scenario switch
        {
            "incompleto" => "Desconhecido",
            "estimado" or "estimado-aceito" => "Estimado",
            "nao-aplicavel" => "NaoAplicavel",
            "minimo" or "normalizacao-ms" or "ms-ausente" => scenario,
            _ => "Declarado",
        }, incompatibleEnergy: scenario == "modalidade");
        var command = data.Command;
        if (scenario == "inviavel") command = command with
        {
            Metas = command.Metas.Select(x => x.Componente == "EM" ? x with { Minimo = "10.1" } : x).ToArray(),
        };
        if (scenario == "estimado-aceito") command = command with
        {
            Metas = command.Metas.Select(x => x.Componente == "PB" ? x with { AceitarEstimados = true } : x).ToArray(),
        };
        if (scenario == "economico") command = command with { Modo = "Economico" };
        if (scenario == "meta-ms") command = command with { Metas = [command.Metas[0] with { Base = "MS" }] };
        if (scenario == "preparo") command = command with { Preparo = "Preparo sintético diferente" };
        if (scenario == "precisao") command = command with
        {
            MassaKg = "1",
            Candidatos = command.Candidatos.Select((x, index) => x with { ReferenciaKg = index == 0 ? "1" : "0" }).ToArray(),
            Metas = [command.Metas[0] with { Minimo = "200" }],
        };

        using var response = await SendAsync(client, Route, command, Key());
        var result = await ReadAsync(response, HttpStatusCode.Created);
        Assert.Equal(expected, Outcome(result));
        if (scenario == "incompleto")
        {
            var protein = Assert.Single(result.GetProperty("entradas").GetProperty("composicaoReferencia")
                .GetProperty("componentes").EnumerateArray(), value => value.GetProperty("componente").GetString() == "PB");
            Assert.Equal(60m, ReadNumber(protein.GetProperty("coberturaPercentual")));
            Assert.Equal(6000m, ReadNumber(protein.GetProperty("contribuicaoConhecida")));
            Assert.Equal(JsonValueKind.Null, protein.GetProperty("valorBN").ValueKind);
        }
        if (expected == "SolucaoViavel") return;
        using var draft = await client.PostAsJsonAsync($"{Route}/{result.GetProperty("id").GetGuid()}/previas-rascunho",
            new OptimizationDraftDestination(data.Output.Id, data.Output.Revisao, data.Recipe.Id, data.Recipe.Revisao));
        Assert.Equal(HttpStatusCode.Conflict, draft.StatusCode);
        Assert.Equal(1, await VersionCount(data.Recipe.Id));
    }

    [Fact]
    public async Task S05_21_25_pending_replay_precedes_capacity_and_timeout_remains_terminal_after_client_disconnect()
    {
        var data = await SeedAsync();
        var gate = new ControlledSolver();
        using var child = Factory.WithWebHostBuilder(host => host.ConfigureTestServices(services =>
        {
            services.RemoveAll<IOptimizationSolver>();
            services.AddSingleton<IOptimizationSolver>(gate);
        }));
        using var identity = await PropertyTestHttp.AuthenticateAsync(Factory);
        using var client = child.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        client.DefaultRequestHeaders.Authorization = identity.DefaultRequestHeaders.Authorization;
        var key = Key();
        using var cancellation = new CancellationTokenSource();
        var first = SendAsync(client, Route, data.Command, key, cancellation.Token);
        Guid id;
        try
        {
            await gate.Started.Task.WaitAsync(TimeSpan.FromSeconds(15));
            using var replay = await SendAsync(client, Route, data.Command, key);
            var pending = await ReadAsync(replay, HttpStatusCode.Accepted);
            Assert.Equal("EmCalculo", pending.GetProperty("estado").GetString());
            id = pending.GetProperty("id").GetGuid();
            using var busy = await SendAsync(client, Route, data.Command, Key());
            Assert.Equal(HttpStatusCode.TooManyRequests, busy.StatusCode);
            using var collision = await SendAsync(client, Route, data.Command with { MassaKg = "101" }, key);
            Assert.Equal(HttpStatusCode.Conflict, collision.StatusCode);
            cancellation.Cancel();
        }
        finally { gate.Release.TrySetResult(); }

        try { using var ignored = await first.WaitAsync(TimeSpan.FromSeconds(15)); }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        JsonElement terminal = default;
        for (var attempt = 0; attempt < 60; attempt++)
        {
            using var read = await client.GetAsync($"{Route}/por-chave?chave={key}");
            terminal = await ReadAsync(read, HttpStatusCode.OK);
            if (terminal.GetProperty("estado").GetString() == "Finalizada") break;
            await Task.Delay(50);
        }
        Assert.Equal(id, terminal.GetProperty("id").GetGuid());
        Assert.Equal("LimiteTempo", Outcome(terminal));
        Assert.Equal(1, gate.Calls);
    }

    [Fact]
    public async Task S05_28_recovery_completes_expired_execution_once_and_database_guards_reject_rewrites()
    {
        using var anonymous = Factory.CreateHttpsClient();
        using var unauthorized = await anonymous.GetAsync(Route);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        var user = await Factory.SeedUserAsync($"f05_expired_{Key()}");
        var command = (await SeedAsync()).Command;
        var now = DateTimeOffset.UtcNow;
        var pendingId = Guid.NewGuid();
        await Factory.ExecuteDbContextAsync(async db =>
        {
            db.Add(new OptimizationExecution
            {
                Id = pendingId, AutorId = user.UserId, Chave = Key(), Hash = new string('b', 64),
                CreatedAtUtc = now, DeadlineUtc = now.AddMinutes(10), OwnerToken = Guid.NewGuid(),
                PedidoJson = FormulationJson.Write(command),
                EntradasJson = FormulationJson.Write(new OptimizationInputs(now, [], [], [])),
            });
            return await db.SaveChangesAsync();
        });
        var premature = await Assert.ThrowsAsync<PostgresException>(() => Factory.ExecuteDbContextAsync(db =>
            db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"ExecucoesOtimizacao\" SET \"Estado\"='Finalizada', \"Revisao\"=2 WHERE \"Id\"={pendingId}")));
        Assert.Equal(PostgresErrorCodes.CheckViolation, premature.SqlState);
        Assert.Equal("EmCalculo", await Factory.ExecuteDbContextAsync(db => db.Set<OptimizationExecution>()
            .Where(x => x.Id == pendingId).Select(x => x.Estado).SingleAsync()));

        var execution = new OptimizationExecution
        {
            Id = Guid.NewGuid(), AutorId = user.UserId, Chave = Key(), Hash = new string('a', 64),
            CreatedAtUtc = now.AddMinutes(-2), DeadlineUtc = now.AddMinutes(-1), OwnerToken = Guid.NewGuid(),
            PedidoJson = FormulationJson.Write(command),
            EntradasJson = FormulationJson.Write(new OptimizationInputs(now.AddMinutes(-2), [], [], [])),
        };
        await Factory.ExecuteDbContextAsync(async db => { db.Add(execution); return await db.SaveChangesAsync(); });
        async Task Recover() => await Factory.ExecuteScopeAsync(async services =>
        {
            await services.GetRequiredService<OptimizationService>().RecoverExpiredAsync(default);
            return true;
        });
        await Task.WhenAll(Recover(), Recover());
        var persisted = await Factory.ExecuteDbContextAsync(db => db.Set<OptimizationResultRow>().SingleAsync(x => x.ExecutionId == execution.Id));
        Assert.Equal("ErroAlgoritmo", persisted.Desfecho);
        Assert.Contains("ExecucaoInterrompida", persisted.ConteudoJson);
        var error = await Assert.ThrowsAsync<PostgresException>(() => Factory.ExecuteDbContextAsync(db =>
            db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"ResultadosOtimizacao\" SET \"Desfecho\"='SolucaoViavel' WHERE \"ExecutionId\"={execution.Id}")));
        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        error = await Assert.ThrowsAsync<PostgresException>(() => Factory.ExecuteDbContextAsync(db =>
            db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"ExecucoesOtimizacao\" SET \"PedidoJson\"='{{}}'::jsonb WHERE \"Id\"={execution.Id}")));
        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
    }

    private async Task<Scenario> SeedAsync(bool imported = false, string proteinState = "Declarado", bool incompatibleEnergy = false)
    {
        var actor = Guid.NewGuid();
        Task<Item> ItemAsync(string name) => Catalog(s => s.CreateItemAsync(
            new(Key(), name, null, null, "Alimentar", "kg", true, true, true, false), actor, default));
        var a = await ItemAsync("Ingrediente sintético A");
        var b = await ItemAsync("Ingrediente sintético B");
        var output = await ItemAsync("Mistura sintética F05");
        async Task<ProfileView> PublishAsync(Item item, bool first)
        {
            var useMs = !first && proteinState is "normalizacao-ms" or "ms-ausente";
            var protein = first ? "100" : useMs ? "500" : "400";
            var estimated = !first && proteinState == "Estimado";
            var pb = !first && proteinState is "Desconhecido" or "NaoAplicavel"
                ? new NutritionValue("PB", proteinState, null, null, "BN", "g/kg", "Sintetico", "", Motivo: "Lacuna sintética")
                : new NutritionValue("PB", "Conhecido", protein, estimated ? "Estimado" : "Declarado",
                    useMs ? "MS" : "BN", "g/kg", "Sintetico", "", Qualificador: !first && proteinState == "minimo" ? "Minimo" : "Pontual",
                    Hipotese: estimated ? "Hipótese sintética controlada" : null);
            List<NutritionValue> values =
                [pb, Value(!first && incompatibleEnergy ? "EB" : "EM", first ? "12" : "8", "MJ/kg", "Sintetico"), Value("FB", first ? "20" : "80")];
            if (first || proteinState != "ms-ausente") values.Add(Value("UMIDADE", first ? "100" : "200"));
            var created = await Catalog(s => s.CreateProfileAsync(item.Id, new("Perfil sintético", "Fixture", "Sintetico",
                "Mistura", "Amostra sintética", "Verificação F05", null, null, null, null,
                values), actor, default));
            return await Catalog(s => s.ProfileStateAsync(created.Id, created.Revisao, true, actor, default));
        }

        var pa = await PublishAsync(a, true);
        var pb = await PublishAsync(b, false);
        var recipe = await Catalog(s => s.CreateRecipeAsync(new(Key(), "Receita sintética F05", "Validação"), actor, default));
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var version = await Catalog(s => s.CreateVersionAsync(recipe.Id, new("MisturaSimples", "Quantidade", "100", "kg",
            [new(firstId, a.Id, "Alimentar", "60", "kg", "Variavel", pa.Id), new(secondId, b.Id, "Alimentar", "40", "kg", "Variavel", pb.Id)],
            [new(Guid.NewGuid(), output.Id, "Saída", "100", "kg", "Variavel", true)], [], [], null, []), actor, default));
        version = await Catalog(s => s.VersionStateAsync(version.Id, version.Revisao, true, actor, default));
        version = await Catalog(s => s.GetVersionAsync(version.Id, default));

        var request = new OptimizationCommand(OptimizationEngine.ModelVersion, "N1", false, "100",
            [new(firstId, a.Id, a.Revisao, pa.Id, pa.Revisao, "60"), new(secondId, b.Id, b.Revisao, pb.Id, pb.Revisao, "40")],
            [Goal("PB", "g/kg", "250"), Goal("EM", "MJ/kg", "10", context: "Sintetico"), Goal("FB", "g/kg", null, "50")],
            ReferenciaDescricao: "F1 sintética", ReceitaId: imported ? recipe.Id : null,
            ReceitaRevisao: imported ? recipe.Revisao : null, ReceitaVersaoId: imported ? version.Id : null,
            ReceitaVersaoRevisao: imported ? version.Revisao : null, Preparo: "Mistura");
        return new(request, recipe, version, output, pa);
    }

    private static OptimizationGoalCommand Goal(string component, string unit, string? minimum, string? maximum = null, string context = "") =>
        new(Guid.NewGuid(), component, "Sintetico", context, "BN", unit, minimum, maximum, "InformadaUsuario",
            Justificativa: "Meta sintética de validação");

    private static NutritionValue Value(string component, string number, string unit = "g/kg", string context = "") =>
        new(component, "Conhecido", number, "Declarado", "BN", unit, "Sintetico", context);

    private Task<T> Catalog<T>(Func<FormulationService, Task<T>> action) =>
        Factory.ExecuteScopeAsync(services => action(services.GetRequiredService<FormulationService>()));

    private Task<int> VersionCount(Guid recipeId) =>
        Factory.ExecuteDbContextAsync(db => db.Set<RecipeVersion>().CountAsync(x => x.ReceitaId == recipeId));

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, string url, object body, string key,
        CancellationToken ct = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        message.Headers.Add("Idempotency-Key", key);
        return await client.SendAsync(message, ct);
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == expected, $"Esperado {expected}, recebido {response.StatusCode}. {body}");
        return JsonSerializer.Deserialize<JsonElement>(body);
    }

    private static string? Outcome(JsonElement view) => view.GetProperty("resultado").GetProperty("desfecho").GetString();
    private static decimal ReadNumber(JsonElement value) => decimal.Parse(value.GetString()!, CultureInfo.InvariantCulture);
    private static string Key() => Guid.NewGuid().ToString("N");

    private sealed record Scenario(OptimizationCommand Command, Recipe Recipe, RecipeVersionView Baseline, Item Output, ProfileView ProfileA);

    private sealed class ControlledSolver : IOptimizationSolver
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls;

        public async Task<OptimizationSolverResult> SolveAsync(OptimizationNativeModel model, TimeSpan budget, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Calls);
            Started.TrySetResult();
            await Release.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            return new(OptimizationNativeStatus.TimeLimit, "LimiteTempoTeste");
        }
    }
}
