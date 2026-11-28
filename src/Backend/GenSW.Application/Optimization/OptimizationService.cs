using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GenSW.Application.Accounts;
using GenSW.Application.Formulation;
using GenSW.Domain.Catalog;
using GenSW.Domain.Formulation;
using GenSW.Domain.Optimization;
using Microsoft.Extensions.DependencyInjection;

namespace GenSW.Application.Optimization;

public sealed partial class OptimizationService(IOptimizationRepository repository, IFormulationRepository catalog,
    FormulationService formulation, IErpAccountScope accountScope, OptimizationEngine engine,
    OptimizationWorkGate gate, IServiceScopeFactory scopes, TimeProvider clock)
{
    private const string Running = "EmCalculo";
    private const string Finished = "Finalizada";
    private const string Interrupted = "Quarentenada";
    private const string InterruptionVersion = "gensw/20261128120100";
    private static readonly TimeSpan DurableDeadline = TimeSpan.FromSeconds(30);
    private static readonly string ApplicationVersion =
        typeof(OptimizationService).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "indisponivel";

    public async Task<OptimizationInputs> PreviewAsync(OptimizationCommand command, Guid actor, CancellationToken ct)
    {
        command = OptimizationInputResolver.Canonicalize(command);
        Authorize(actor);
        await using var transaction = await catalog.BeginAsync(ct);
        var (inputs, _) = await new OptimizationInputResolver(catalog).ResolveAsync(command, Now(), ct);
        await transaction.CommitAsync(ct);
        return inputs;
    }

    public async Task<OptimizationView> ExecuteAsync(OptimizationCommand command, string key, Guid actor, CancellationToken ct)
    {
        var account = Authorize(actor);
        key = Key(key);
        command = OptimizationInputResolver.Canonicalize(command);
        var requestJson = FormulationJson.Write(command);
        var hash = HashJson(requestJson);
        if (await repository.FindByKeyAsync(account, key, ct) is { } replay)
            return await ReplayAsync(replay, hash, ct);

        var lease = Enter(out var matchingCapture);
        if (lease is null)
        {
            if (matchingCapture is not null) await matchingCapture.WaitAsync(ct);
            if (await repository.FindByKeyAsync(account, key, ct) is { } concurrent)
                return await ReplayAsync(concurrent, hash, ct);
            lease = Enter(out _) ?? throw Capacity();
        }

        var handedOff = false;
        try
        {
            using (var admission = CancellationTokenSource.CreateLinkedTokenSource(ct, account.RevocationToken))
            {
                await lease.Ready.WaitAsync(admission.Token);
            }
            var (execution, created) = await CaptureAsync(command, requestJson, hash, key, account, ct);
            lease.SignalCapture();
            if (!created) return await ReplayAsync(execution, hash, ct);
            Task completion;
            using (ExecutionContext.SuppressFlow())
                completion = Task.Run(() => CompleteInScopeAsync(execution, lease), CancellationToken.None);
            handedOff = true;
            await completion.WaitAsync(ct);
            return await ReadAsync(await FindAsync(execution.Id, ct), ct);
        }
        catch (OperationCanceledException) when (account.RevocationToken.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            throw new AccountUnavailableException();
        }
        finally
        {
            if (!handedOff) lease.Dispose();
        }

        OptimizationWorkGate.Lease? Enter(out Task? capture) =>
            gate.TryEnter(account.AccountId, account.BindingId, account.BindingRevision, account.AccessGeneration, actor, key, out capture);
    }

    private async Task<(OptimizationExecution, bool)> CaptureAsync(OptimizationCommand command, string requestJson,
        string hash, string key, ErpAccountContext account, CancellationToken ct)
    {
        await using var transaction = await catalog.BeginAsync(ct);
        await accountScope.RevalidateAsync(ct);
        if (await repository.FindByKeyAsync(account, key, ct) is { } replay)
        {
            await transaction.CommitAsync(ct);
            return (replay, false);
        }
        if (await repository.HasActiveLeaseAsync(account, Now(), ct)) throw Capacity();
        if (command.AnteriorId is { } previous) await FindAsync(previous, ct);
        var cut = Now();
        var (inputs, problem) = await new OptimizationInputResolver(catalog).ResolveAsync(command, cut, ct);
        var now = Now();
        var execution = new OptimizationExecution
        {
            Id = Guid.NewGuid(), AutorId = account.ActorId, Chave = key, Hash = hash, OwnerToken = Guid.NewGuid(),
            AccountId = account.AccountId, BindingId = account.BindingId, BindingRevision = account.BindingRevision,
            AccessGeneration = account.AccessGeneration,
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
            var timeLeft = execution.DeadlineUtc - Now();
            if (timeLeft <= TimeSpan.Zero ||
                execution.AccountId is not { } accountId || execution.BindingId is not { } bindingId ||
                execution.BindingRevision is not { } revision || execution.AccessGeneration is not { } generation)
                return;

            using var deadline = new CancellationTokenSource(timeLeft, clock);
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var binding = scope.ServiceProvider.GetRequiredService<IErpAccountScope>();
                await binding.BindAsync(accountId, bindingId, revision, generation, execution.AutorId, deadline.Token);
                var worker = scope.ServiceProvider.GetRequiredService<OptimizationService>();
                await worker.CompleteAsync(execution, deadline.Token);
            }
            catch (Exception)
            {
                // The startup/periodic recovery owns the pending receipt if persistence could not finish.
            }
        }
    }

    private async Task CompleteAsync(OptimizationExecution captured, CancellationToken deadline)
    {
        var account = accountScope.Current;
        using var executionCancellation = CancellationTokenSource.CreateLinkedTokenSource(deadline, account.RevocationToken);
        var ct = executionCancellation.Token;
        var execution = await repository.FindAsync(captured.Id, ct);
        if (execution is null || execution.Estado != Running || execution.OwnerToken != captured.OwnerToken ||
            execution.AccountId != account.AccountId || execution.BindingId != account.BindingId ||
            execution.BindingRevision != account.BindingRevision || execution.AccessGeneration != account.AccessGeneration ||
            execution.AutorId != account.ActorId)
            return;

        using var monitorStop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var monitor = MonitorAuthorizationAsync(executionCancellation, monitorStop.Token);
        OptimizationResultContent content;
        try { content = await CalculateAsync(execution, ct); }
        finally
        {
            await monitorStop.CancelAsync();
            await monitor;
        }
        ct.ThrowIfCancellationRequested();

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
        await accountScope.RevalidateAsync(ct);
        await repository.TryCompleteAsync(account, execution.Id, execution.OwnerToken, row, expired: false, ct);
    }

    private async Task MonitorAuthorizationAsync(CancellationTokenSource execution, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1), clock);
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
                await accountScope.RevalidateAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
            await execution.CancelAsync();
        }
    }

    private async Task<OptimizationResultContent> CalculateAsync(OptimizationExecution execution, CancellationToken ct)
    {
        try
        {
            var inputs = FormulationJson.Read<OptimizationInputs>(execution.EntradasJson);
            if (execution.ModeloJson is not { } model)
            {
                var primary = inputs.Diagnosticos.FirstOrDefault(x => x.Desfecho == OptimizationOutcome.DadosIncompativeis)
                    ?? inputs.Diagnosticos.First();
                return Content(execution, primary.Desfecho, primary.Codigo);
            }

            var result = await engine.EvaluateAsync(FormulationJson.Read<OptimizationProblem>(model), ct);
            var composition = result.Candidate?.QuantizedMasses is { } masses
                ? OptimizationInputResolver.SummarizeComposition(FormulationJson.Read<OptimizationCommand>(execution.PedidoJson),
                    inputs.Candidatos, masses)
                : null;
            return Content(execution, result.Outcome, result.Code, result,
                result.Outcome == OptimizationOutcome.ErroAlgoritmo ? execution.Id.ToString("D") : null, composition);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return Content(execution, OptimizationOutcome.ErroAlgoritmo, "FalhaCalculo", correlation: execution.Id.ToString("D"));
        }
    }

    public async Task RecoverExpiredAsync(CancellationToken ct)
    {
        var account = accountScope.Current;
        var now = Now();
        foreach (var execution in await repository.FindExpiredAsync(account, now, 20, ct))
        {
            await accountScope.RevalidateAsync(ct);
            var content = Content(execution, OptimizationOutcome.ErroAlgoritmo, "ExecucaoInterrompida",
                correlation: execution.Id.ToString("D"));
            await repository.TryCompleteAsync(account, execution.Id, execution.OwnerToken, ResultRow(execution, content, now),
                expired: true, ct);
        }
    }

    public async Task<OptimizationView> GetAsync(Guid id, Guid actor, CancellationToken ct)
    {
        Authorize(actor);
        return await ReadAsync(await FindAsync(id, ct), ct);
    }

    public async Task<OptimizationView> GetByKeyAsync(string key, Guid actor, CancellationToken ct)
    {
        var execution = await repository.FindByKeyAsync(Authorize(actor), Key(key), ct) ?? throw Missing();
        return await ReadAsync(execution, ct);
    }

    public async Task<CatalogPage<OptimizationSummary>> ListAsync(OptimizationQuery query, Guid actor, CancellationToken ct)
    {
        Authorize(actor);
        query.Validate();
        return await repository.ListAsync(query, ct);
    }

    private ErpAccountContext Authorize(Guid actor)
    {
        var account = accountScope.Current;
        if (actor == Guid.Empty || account.ActorId != actor)
            throw new FormulationException(401, "sessao_invalida", "Entre novamente para continuar.");
        return account;
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
        var command = FormulationJson.Read<OptimizationCommand>(execution.PedidoJson);
        var result = await repository.FindResultAsync(execution.Id, ct);
        if (execution.Estado == Interrupted && execution.QuarantinedAtUtc is { } quarantinedAt)
        {
            var content = Content(execution, OptimizationOutcome.ErroAlgoritmo, "ExecucaoInterrompida",
                correlation: execution.Id.ToString("D")) with
            {
                VersaoModelo = command.VersaoModelo,
                VersaoAplicacao = InterruptionVersion
            };
            result = ResultRow(execution, content, quarantinedAt);
        }
        return new(execution.Id, execution.AutorId, execution.CreatedAtUtc,
            result is null ? Running : Finished, result is not null && execution.Estado == Running ? execution.Revisao + 1 : execution.Revisao,
            command, FormulationJson.Read<OptimizationInputs>(execution.EntradasJson),
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
    private static FormulationException Capacity() => new(429, "capacidade_otimizacao",
        "O cálculo está ocupado. Tente novamente com a mesma chave.");
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
