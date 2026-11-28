using GenSW.Application.Accounts;
using GenSW.Application.Optimization;
using GenSW.Domain.Formulation;
using GenSW.Domain.Optimization;
using GenSW.Domain.People;
using GenSW.Infrastructure.Identity;
using GenSW.Infrastructure.Optimization;
using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace GenSW.API.Tests;

public sealed class AccountJobIsolationTests
{
    private const string Baseline = "20261112205407_AddFormulationOptimization";
    private const string Latest = "20261128120100_AccountJobEnvelope";
    private const string DatabaseA = "gensw_auth_tests";
    private const string DatabaseB = "gensw_jobs_b_synthetic";
    private static readonly Guid Actor = Guid.Parse("58100000-0000-0000-0000-000000000001");

    [Fact]
    public async Task Scoped_leases_recover_once_and_fence_quarantines_only_old_uncommitted_work()
    {
        await using var postgres = await EphemeralPostgreSql.StartAsync();
        await using (var owner = await OpenAsync(postgres.ConnectionString))
        {
            Assert.Equal(DatabaseA, await ScalarAsync(owner, "SELECT current_database()"));
            Assert.Equal("gensw_test", await ScalarAsync(owner, "SELECT current_user"));
            await RunAsync(owner, "CREATE DATABASE gensw_jobs_b_synthetic");
            await RunAsync(owner, """
                CREATE ROLE gensw_jobs_runtime_a LOGIN;
                CREATE ROLE gensw_jobs_runtime_b LOGIN;
                CREATE ROLE gensw_jobs_fence_a LOGIN;
                REVOKE CONNECT ON DATABASE gensw_auth_tests, gensw_jobs_b_synthetic FROM PUBLIC;
                GRANT CONNECT ON DATABASE gensw_auth_tests TO gensw_jobs_runtime_a, gensw_jobs_fence_a;
                GRANT CONNECT ON DATABASE gensw_jobs_b_synthetic TO gensw_jobs_runtime_b;
                """);
        }

        var legacyId = Guid.NewGuid();
        var recoveredId = Guid.NewGuid();
        var committedId = Guid.NewGuid();
        var lateId = Guid.NewGuid();
        var a = await PrepareAsync(postgres.ConnectionString, DatabaseA, "gensw_jobs_runtime_a", "Conta A", legacyId);
        var b = await PrepareAsync(postgres.ConnectionString, DatabaseB, "gensw_jobs_runtime_b", "Conta B", legacyId);

        var crossConnection = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await using var _ = await OpenAsync(Connection(postgres.ConnectionString, DatabaseB, a.RuntimeRole));
        });
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, crossConnection.SqlState);

        await using var runtimeA = await OpenRuntimeAsync(a);
        await using var runtimeB = await OpenRuntimeAsync(b);
        await using var competingA = await OpenRuntimeAsync(a);
        await using var contextA = Context(runtimeA);
        await using var contextB = Context(runtimeB);
        await using var competingContextA = Context(competingA);
        var scopeA = new TestAccountScope(a.Context);
        var scopeB = new TestAccountScope(b.Context);
        var repoA = new OptimizationRepository(contextA, scopeA);
        var repoB = new OptimizationRepository(contextB, scopeB);
        var competingRepoA = new OptimizationRepository(competingContextA, scopeA);

        var now = DateTimeOffset.UtcNow;
        var recoveredLease = Guid.NewGuid();
        var committedLease = Guid.NewGuid();
        var lateLease = Guid.NewGuid();
        var key = Guid.NewGuid().ToString("N");
        foreach (var (database, repository, target) in new[] { (contextA, repoA, a), (contextB, repoB, b) })
        {
            repository.Add(Job(target, recoveredId, recoveredLease, key, now.AddMinutes(-2), now.AddMinutes(-1)));
            repository.Add(Job(target, committedId, committedLease, $"commit-{key}", now, now.AddMinutes(2)));
            repository.Add(Job(target, lateId, lateLease, $"late-{key}", now, now.AddMinutes(2)));
            await database.SaveChangesAsync();
        }

        var receiptA = await repoA.FindByKeyAsync(a.Context, key, default);
        var receiptB = await repoB.FindByKeyAsync(b.Context, key, default);
        Assert.Equal(recoveredId, receiptA?.Id);
        Assert.Equal(recoveredId, receiptB?.Id);
        Assert.Equal(Actor, receiptA?.AutorId);
        Assert.Equal(Actor, receiptB?.AutorId);
        Assert.Contains("Conta A", receiptA!.PedidoJson);
        Assert.Contains("Conta B", receiptB!.PedidoJson);
        Assert.Null(await repoA.FindByKeyAsync(b.Context, key, default));
        Assert.True(await repoA.HasActiveLeaseAsync(a.Context, now, default));
        Assert.True(await repoB.HasActiveLeaseAsync(b.Context, now, default));

        await Task.WhenAll(RecoverAsync(repoA, scopeA), RecoverAsync(competingRepoA, scopeA), RecoverAsync(repoB, scopeB));
        var recoveredA = FormulationJson.Read<OptimizationResultContent>((await repoA.FindResultAsync(recoveredId, default))!.ConteudoJson);
        var recoveredB = FormulationJson.Read<OptimizationResultContent>((await repoB.FindResultAsync(recoveredId, default))!.ConteudoJson);
        Assert.Equal("ExecucaoInterrompida", recoveredA.Codigo);
        Assert.Equal("ExecucaoInterrompida", recoveredB.Codigo);
        Assert.NotEqual(recoveredA.HashEntradas, recoveredB.HashEntradas);
        Assert.False(await competingRepoA.TryCompleteAsync(a.Context, recoveredId, recoveredLease,
            Result(recoveredId, a.Name), expired: true, default));

        await using var maintenance = await OpenAsync(Connection(postgres.ConnectionString, DatabaseA, "gensw_jobs_fence_a"));
        await using (var beforeBarrier = await runtimeA.BeginTransactionAsync())
        {
            Assert.Equal(1, await RunAsync(runtimeA, """
                UPDATE public."ExecucoesOtimizacao" SET "Estado" = 'Finalizada', "Revisao" = 2
                WHERE "Id" = $1 AND "OwnerToken" = $2 AND "Estado" = 'EmCalculo';
                """, committedId, committedLease));
            Assert.Equal(1, await RunAsync(runtimeA, """
                INSERT INTO public."ResultadosOtimizacao" ("ExecutionId", "CreatedAtUtc", "Desfecho", "ConteudoJson")
                VALUES ($1, clock_timestamp(), 'ErroAlgoritmo', $2::jsonb);
                """, committedId, Result(committedId, a.Name).ConteudoJson));

            var fence = ScalarAsync(maintenance, "SELECT gensw_scope.advance_fence($1, $2, $3, $4, 2, true)",
                a.DatabaseId, a.Context.AccountId, a.Context.BindingId, a.Context.BindingRevision);
            try
            {
                await using var observer = await OpenAsync(a.OwnerConnection);
                await WaitForBlockedFenceAsync(observer, maintenance.ProcessID);
                Assert.True(await repoB.TryCompleteAsync(b.Context, committedId, committedLease,
                    Result(committedId, b.Name), expired: false, default));
                await beforeBarrier.CommitAsync();
                Assert.Equal(true, await fence);
            }
            finally
            {
                await beforeBarrier.DisposeAsync();
                await fence;
            }
        }

        var late = await Assert.ThrowsAsync<PostgresException>(() => competingRepoA.TryCompleteAsync(a.Context, lateId, lateLease,
            Result(lateId, a.Name), expired: false, default));
        Assert.Equal("GS001", late.SqlState);
        Assert.Equal("Finalizada", (await repoA.FindAsync(committedId, default))?.Estado);
        Assert.Equal("Quarentenada", (await repoA.FindAsync(lateId, default))?.Estado);
        Assert.Equal("Quarentenada", (await repoA.FindAsync(legacyId, default))?.Estado);
        Assert.Null(await repoA.FindResultAsync(lateId, default));
        Assert.Contains("Conta A", (await repoA.FindResultAsync(committedId, default))!.ConteudoJson);
        Assert.Contains("Conta B", (await repoB.FindResultAsync(committedId, default))!.ConteudoJson);
        Assert.True(await repoB.TryCompleteAsync(b.Context, lateId, lateLease,
            Result(lateId, b.Name), expired: false, default));
    }

    private static async Task<Destination> PrepareAsync(string cluster, string name, string runtime, string label, Guid legacyId)
    {
        var ownerConnection = Connection(cluster, name, "gensw_test");
        await using var owner = await OpenAsync(ownerConnection);
        Assert.Equal(name, await ScalarAsync(owner, "SELECT current_database()"));
        Assert.Equal("gensw_test", await ScalarAsync(owner, "SELECT current_user"));
        await using var db = Context(owner);
        Assert.Empty(await db.Database.GetAppliedMigrationsAsync());
        var migrator = db.GetService<IMigrator>();
        var baselineScript = migrator.GenerateScript(toMigration: Baseline, options: MigrationsSqlGenerationOptions.Idempotent);
        Assert.Contains("CK_ExecucoesOtimizacao_Estado", baselineScript);
        await RunAsync(owner, baselineScript);
        var prior = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
        Assert.Equal(22, prior.Length);
        Assert.Equal(Baseline, prior[^1]);

        var now = DateTimeOffset.UtcNow;
        var person = Pessoa.Criar(TipoPessoa.Fisica, label, null, now);
        db.Add(person);
        db.Add(new ApplicationUser
        {
            Id = Actor, Pessoa = person, IsActive = true, CreatedAtUtc = now, UpdatedAtUtc = now
        });
        await db.SaveChangesAsync();
        await RunAsync(owner, """
            INSERT INTO public."ExecucoesOtimizacao" ("Id", "AutorId", "Chave", "Hash", "CreatedAtUtc",
                "DeadlineUtc", "OwnerToken", "Estado", "Revisao", "PedidoJson", "EntradasJson")
            VALUES ($1, $2, 'legacy', $3, clock_timestamp(), clock_timestamp() + interval '1 minute',
                $4, 'EmCalculo', 1, '{}'::jsonb, '{}'::jsonb);
            """, legacyId, Actor, new string('a', 64), Guid.NewGuid());

        var upgrade = migrator.GenerateScript(Baseline, Latest, MigrationsSqlGenerationOptions.Idempotent);
        Assert.Contains("FOR EACH STATEMENT EXECUTE FUNCTION gensw_scope.guard_write()", upgrade);
        Assert.Contains("REVOKE EXECUTE ON FUNCTION public.gensw_quarantine_optimization_jobs(bigint) FROM PUBLIC", upgrade);
        Assert.Contains("UX_ExecucoesOtimizacao_Escopo_Chave", upgrade);
        await RunAsync(owner, upgrade);
        await RunAsync(owner, upgrade);
        Assert.Equal(new[] { "20261128120000_AccountIsolation", Latest },
            (await db.Database.GetAppliedMigrationsAsync()).Except(prior).ToArray());
        Assert.False(db.Database.HasPendingModelChanges());

        var target = new Destination(ownerConnection, Connection(cluster, name, runtime), runtime, label, Guid.NewGuid(),
            new ErpAccountContext(Guid.NewGuid(), Guid.NewGuid(), 1, 1, Actor, person.Id, label, ["Admin"], default));
        await RunAsync(owner, """
            INSERT INTO gensw_scope.destination (database_id, account_id, binding_id, binding_revision, generation, enabled)
            VALUES ($1, $2, $3, 1, 1, true);
            """, target.DatabaseId, target.Context.AccountId, target.Context.BindingId);
        // Runtime role names originate exclusively from the fixed, synthetic fixture.
        await RunAsync(owner, $"""
            GRANT USAGE ON SCHEMA gensw_scope TO {runtime};
            GRANT EXECUTE ON FUNCTION gensw_scope.verify_generation() TO {runtime};
            GRANT SELECT, INSERT, UPDATE ON public."ExecucoesOtimizacao", public."ResultadosOtimizacao" TO {runtime};
            """);
        if (name == DatabaseA)
        {
            await RunAsync(owner, """
                GRANT USAGE ON SCHEMA gensw_scope TO gensw_jobs_fence_a;
                GRANT EXECUTE ON FUNCTION gensw_scope.advance_fence(uuid, uuid, uuid, bigint, bigint, boolean) TO gensw_jobs_fence_a;
                """);
        }
        return target;
    }

    private static OptimizationExecution Job(Destination target, Guid id, Guid lease, string key, DateTimeOffset created,
        DateTimeOffset deadline) => new()
    {
        Id = id, AutorId = Actor, AccountId = target.Context.AccountId, BindingId = target.Context.BindingId,
        BindingRevision = target.Context.BindingRevision, AccessGeneration = target.Context.AccessGeneration,
        Chave = key, Hash = new string('b', 64), CreatedAtUtc = created, DeadlineUtc = deadline, OwnerToken = lease,
        PedidoJson = FormulationJson.Write(new OptimizationCommand(OptimizationEngine.ModelVersion, "N1", false, "1", [], [],
            ReferenciaDescricao: target.Name)),
        EntradasJson = FormulationJson.Write(new OptimizationInputs(created, [], [],
            [new(OptimizationOutcome.DadosIncompletos, "Fixture", target.Name)]))
    };

    private static OptimizationResultRow Result(Guid job, string content) => new()
    {
        ExecutionId = job, CreatedAtUtc = DateTimeOffset.UtcNow, Desfecho = "ErroAlgoritmo",
        ConteudoJson = FormulationJson.Write(new { Codigo = content })
    };

    private static Task RecoverAsync(IOptimizationRepository repository, IErpAccountScope scope) =>
        new OptimizationService(repository, null!, null!, scope, null!, new(), null!, TimeProvider.System)
            .RecoverExpiredAsync(default);

    private static async Task WaitForBlockedFenceAsync(NpgsqlConnection observer, int processId)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!Equals("Lock", await ScalarAsync(observer, "SELECT wait_event_type FROM pg_stat_activity WHERE pid = $1", processId)))
            await Task.Delay(20, timeout.Token);
    }

    private static string Connection(string cluster, string database, string user) =>
        new NpgsqlConnectionStringBuilder(cluster) { Database = database, Username = user }.ConnectionString;

    private static GenSWDbContext Context(NpgsqlConnection open) =>
        new(new DbContextOptionsBuilder<GenSWDbContext>().UseNpgsql(open).Options);

    private static async Task<NpgsqlConnection> OpenAsync(string connectionString)
    {
        var connection = new NpgsqlConnection(connectionString);
        try
        {
            await connection.OpenAsync();
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private static async Task<NpgsqlConnection> OpenRuntimeAsync(Destination target)
    {
        var connection = await OpenAsync(target.RuntimeConnection);
        try
        {
            await RunAsync(connection, """
                SELECT set_config('gensw.binding', $1, false), set_config('gensw.revision', $2, false),
                    set_config('gensw.generation', $3, false);
                """, target.Context.BindingId.ToString("D"), "1", "1");
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private static async Task<int> RunAsync(NpgsqlConnection connection, string sql, params object[] args)
    {
        await using var command = Command(connection, sql, args);
        return await command.ExecuteNonQueryAsync();
    }

    private static async Task<object?> ScalarAsync(NpgsqlConnection connection, string sql, params object[] args)
    {
        await using var command = Command(connection, sql, args);
        return await command.ExecuteScalarAsync();
    }

    private static NpgsqlCommand Command(NpgsqlConnection connection, string sql, object[] args)
    {
        var command = new NpgsqlCommand(sql, connection);
        foreach (var arg in args) command.Parameters.Add(new NpgsqlParameter { Value = arg });
        return command;
    }

    private sealed record Destination(string OwnerConnection, string RuntimeConnection, string RuntimeRole, string Name,
        Guid DatabaseId, ErpAccountContext Context);

    private sealed class TestAccountScope(ErpAccountContext context) : IErpAccountScope
    {
        public ErpAccountContext Current => context;
        public Task BindAsync(Guid accountId, Guid bindingId, long bindingRevision, long accessGeneration, Guid actorId,
            CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<ErpAccountKey>> ListActiveAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task RevalidateAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
