using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GenSW.Application.Authentication;
using GenSW.Application.Formulation;
using GenSW.Domain.Catalog;
using GenSW.Domain.Formulation;
using GenSW.Domain.Optimization;
using Microsoft.Extensions.DependencyInjection;

namespace GenSW.Application.Optimization;

public sealed partial class OptimizationService(IOptimizationRepository repository, IFormulationRepository catalog,
    FormulationService formulation, IAuthenticationSessionService authentication, OptimizationEngine engine,
    OptimizationWorkGate gate, IServiceScopeFactory scopes, TimeProvider clock)
{
    private const string Running = "EmCalculo";
    private const string Finished = "Finalizada";
    private static readonly TimeSpan DurableDeadline = TimeSpan.FromSeconds(30);
    private static readonly string ApplicationVersion =
        typeof(OptimizationService).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "indisponivel";

    public async Task<OptimizationInputs> PreviewAsync(OptimizationCommand command, Guid actor, CancellationToken ct)
    {
        command = OptimizationInputResolver.Canonicalize(command);
        await using var transaction = await catalog.BeginAsync(ct);
        await AuthorizeAsync(actor, ct);
        var (inputs, _) = await new OptimizationInputResolver(catalog).ResolveAsync(command, Now(), ct);
        await transaction.CommitAsync(ct);
        return inputs;
    }

    public async Task<OptimizationView> ExecuteAsync(OptimizationCommand command, string key, Guid actor, CancellationToken ct)
    {
        await AuthorizeAsync(actor, ct);
        key = Key(key);
        command = OptimizationInputResolver.Canonicalize(command);
        var requestJson = FormulationJson.Write(command);
        var hash = HashJson(requestJson);
        if (await repository.FindByKeyAsync(actor, key, ct) is { } replay)
            return await ReplayAsync(replay, hash, ct);

        var lease = gate.TryEnter(actor, key, out var matchingCapture);
        if (lease is null)
        {
            if (matchingCapture is not null) await matchingCapture.WaitAsync(ct);
            if (await repository.FindByKeyAsync(actor, key, ct) is { } concurrent)
                return await ReplayAsync(concurrent, hash, ct);
            lease = gate.TryEnter(actor, key, out _);
            if (lease is null) throw new FormulationException(429, "capacidade_otimizacao", "O cálculo está ocupado. Tente novamente com a mesma chave.");
        }

        var handedOff = false;
        try
        {
            var (execution, created) = await CaptureAsync(command, requestJson, hash, key, actor, ct);
            lease.SignalCapture();
            if (!created) return await ReplayAsync(execution, hash, ct);
            handedOff = true;
            var completion = Task.Run(() => CompleteInScopeAsync(execution, lease), CancellationToken.None);
            await completion.WaitAsync(ct);
            return await ReadAsync(await FindAsync(execution.Id, ct), ct);
        }
        finally
        {
            if (!handedOff) lease.Dispose();
        }
    }

    private async Task<(OptimizationExecution, bool)> CaptureAsync(OptimizationCommand command, string requestJson,
        string hash, string key, Guid actor, CancellationToken ct)
    {
        await using var transaction = await catalog.BeginAsync(ct);
        await AuthorizeAsync(actor, ct);
        if (await repository.FindByKeyAsync(actor, key, ct) is { } replay)
        {
            await transaction.CommitAsync(ct);
            return (replay, false);
        }
        if (command.AnteriorId is { } previous) await FindAsync(previous, ct);
        var cut = Now();
        var (inputs, problem) = await new OptimizationInputResolver(catalog).ResolveAsync(command, cut, ct);
        var now = Now();
        var execution = new OptimizationExecution
        {
            Id = Guid.NewGuid(), AutorId = actor, Chave = key, Hash = hash, OwnerToken = Guid.NewGuid(),
            CreatedAtUtc = now, DeadlineUtc = now.Add(DurableDeadline), EspecieId = command.EspecieId, Fase = command.Fase,
            PedidoJson = requestJson, EntradasJson = FormulationJson.Write(inputs),
            ModeloJson = problem is null ? null : FormulationJson.Write(problem)
        };
        repository.Add(execution);
        await catalog.SaveAsync(ct);
        await transaction.CommitAsync(ct);
        return (execution, true);
    }

    private async Task CompleteInScopeAsync(OptimizationExecution execution, OptimizationWorkGate.Lease lease)
    {
        using (lease)
        {
            // The accepted execution owns its deadline. A disconnected HTTP client only stops waiting for it.
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var worker = scope.ServiceProvider.GetRequiredService<OptimizationService>();
                await worker.CompleteAsync(execution);
            }
            catch (Exception)
            {
                // The startup/periodic recovery owns the pending receipt if persistence could not finish.
            }
        }
    }

    private async Task CompleteAsync(OptimizationExecution execution)
    {
        OptimizationResultContent content;
        try
        {
            if (execution.ModeloJson is { } model)
            {
                var result = await engine.EvaluateAsync(FormulationJson.Read<OptimizationProblem>(model), CancellationToken.None);
                var composition = result.Candidate?.QuantizedMasses is { } masses
                    ? OptimizationInputResolver.SummarizeComposition(FormulationJson.Read<OptimizationCommand>(execution.PedidoJson),
                        FormulationJson.Read<OptimizationInputs>(execution.EntradasJson).Candidatos, masses)
                    : null;
                content = Content(execution, result.Outcome, result.Code, result,
                    result.Outcome == OptimizationOutcome.ErroAlgoritmo ? execution.Id.ToString("D") : null, composition);
            }
            else
            {
                var inputs = FormulationJson.Read<OptimizationInputs>(execution.EntradasJson);
                var primary = inputs.Diagnosticos.FirstOrDefault(x => x.Desfecho == OptimizationOutcome.DadosIncompativeis)
                    ?? inputs.Diagnosticos.First();
                content = Content(execution, primary.Desfecho, primary.Codigo);
            }
        }
        catch (Exception)
        {
            content = Content(execution, OptimizationOutcome.ErroAlgoritmo, "FalhaCalculo", correlation: execution.Id.ToString("D"));
        }
        var now = Now();
        if (now >= execution.DeadlineUtc) return;
        OptimizationResultRow row;
        try
        {
            row = ResultRow(execution, content, now);
        }
        catch (Exception error) when (error is ArgumentException or JsonException or NotSupportedException)
        {
            var failure = Content(execution, OptimizationOutcome.ErroAlgoritmo, "ResultadoNaoSerializavel",
                correlation: execution.Id.ToString("D"));
            row = ResultRow(execution, failure, now);
        }
        await repository.TryCompleteAsync(execution.Id, execution.OwnerToken, row, expired: false, CancellationToken.None);
    }

    public async Task RecoverExpiredAsync(CancellationToken ct)
    {
        var now = Now();
        foreach (var execution in await repository.FindExpiredAsync(now, 20, ct))
        {
            var content = Content(execution, OptimizationOutcome.ErroAlgoritmo, "ExecucaoInterrompida",
                correlation: execution.Id.ToString("D"));
            await repository.TryCompleteAsync(execution.Id, execution.OwnerToken, ResultRow(execution, content, now), expired: true, ct);
        }
    }

    public async Task<OptimizationView> GetAsync(Guid id, Guid actor, CancellationToken ct)
    {
        await AuthorizeAsync(actor, ct);
        return await ReadAsync(await FindAsync(id, ct), ct);
    }

    public async Task<OptimizationView> GetByKeyAsync(string key, Guid actor, CancellationToken ct)
    {
        await AuthorizeAsync(actor, ct);
        var execution = await repository.FindByKeyAsync(actor, Key(key), ct) ?? throw Missing();
        return await ReadAsync(execution, ct);
    }

    public async Task<CatalogPage<OptimizationSummary>> ListAsync(OptimizationQuery query, Guid actor, CancellationToken ct)
    {
        await AuthorizeAsync(actor, ct);
        query.Validate();
        return await repository.ListAsync(query, ct);
    }

    private async Task AuthorizeAsync(Guid actor, CancellationToken ct)
    {
        if (actor == Guid.Empty || await authentication.GetCurrentUserAsync(actor, ct) is null)
            throw new FormulationException(401, "sessao_invalida", "Entre novamente para continuar.");
    }

    private async Task<OptimizationExecution> FindAsync(Guid id, CancellationToken ct) =>
        await repository.FindAsync(id, ct) ?? throw Missing();

    private Task<OptimizationView> ReplayAsync(OptimizationExecution execution, string hash, CancellationToken ct)
    {
        if (execution.Hash != hash) throw DivergentKey();
        return ReadAsync(execution, ct);
    }

    private async Task<OptimizationView> ReadAsync(OptimizationExecution execution, CancellationToken ct)
    {
        var result = await repository.FindResultAsync(execution.Id, ct);
        return new(execution.Id, execution.AutorId, execution.CreatedAtUtc,
            result is null ? Running : Finished, result is not null && execution.Estado == Running ? execution.Revisao + 1 : execution.Revisao,
            FormulationJson.Read<OptimizationCommand>(execution.PedidoJson), FormulationJson.Read<OptimizationInputs>(execution.EntradasJson),
            execution.ModeloJson is null ? null : FormulationJson.Read<OptimizationProblem>(execution.ModeloJson),
            result is null ? null : FormulationJson.Read<OptimizationResultContent>(result.ConteudoJson), result?.CreatedAtUtc,
            execution.Hash, result is null ? null : HashJson(result.ConteudoJson), execution.DeadlineUtc);
    }

    private static OptimizationResultContent Content(OptimizationExecution execution, OptimizationOutcome outcome,
        string code, OptimizationResult? motor = null, string? correlation = null, OptimizationComposition? composition = null) =>
        new(outcome, code, OptimizationEngine.ModelVersion, ApplicationVersion, HashJson(execution.EntradasJson),
            execution.ModeloJson is null ? null : HashJson(execution.ModeloJson), motor, correlation,
            motor?.Model?.Native is { } native ? Hash(native) : null, composition);

    private static OptimizationResultRow ResultRow(OptimizationExecution execution, OptimizationResultContent content, DateTimeOffset now) =>
        new() { ExecutionId = execution.Id, CreatedAtUtc = now, Desfecho = content.Desfecho.ToString(), ConteudoJson = FormulationJson.Write(content) };

    private static FormulationException Missing() => new(404, "otimizacao_nao_encontrada", "O registro de otimização não foi encontrado.");
    private static FormulationException DivergentKey() => new(409, "idempotencia_divergente", "A chave já foi usada com outro pedido.");
    private static string Key(string key) => CatalogRules.Text(key, 100, "Idempotency-Key");

    private DateTimeOffset Now()
    {
        var utc = clock.GetUtcNow().ToUniversalTime();
        return utc.AddTicks(-(utc.Ticks % 10));
    }

    private static string Hash<T>(T value) => HashJson(FormulationJson.Write(value));

    private static string HashJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(CanonicalJson(document.RootElement))));
    }

    private static string CanonicalJson(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => "{" + string.Join(",", element.EnumerateObject()
            .OrderBy(x => x.Name, StringComparer.Ordinal).Select(x => JsonSerializer.Serialize(x.Name) + ":" + CanonicalJson(x.Value))) + "}",
        JsonValueKind.Array => "[" + string.Join(",", element.EnumerateArray().Select(CanonicalJson)) + "]",
        JsonValueKind.String => JsonSerializer.Serialize(element.GetString()),
        _ => element.GetRawText()
    };
}
