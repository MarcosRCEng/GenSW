using System.Net;
using System.Net.Http.Json;
using System.Data.Common;
using GenSW.Application.Production;
using GenSW.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Xunit;

namespace GenSW.API.Tests;

#pragma warning disable EF1002 // Fixture-only generated UUIDs; never user SQL.
public sealed partial class ProductionApiTests
{
    [Fact]
    public async Task T07_lost_http_response_after_commit_recovers_same_execution_with_original_key()
    {
        var (client, user) = await Session(); using (client)
        {
            var f = await ScenarioAsync(client, user.UserId); var order = await Order(client, user.UserId, f);
            order = await Success<OrderView>(await Post(client, $"{order.Id}/inicio", Start(order, f, user.UserId)));
            var command = Confirm(order, f, user.UserId); var key = Unique();
            using var lostReply = new HttpClient(new LoseConfirmedResponse(Factory.Server.CreateHandler())) { BaseAddress = new Uri("https://localhost") };
            lostReply.DefaultRequestHeaders.Authorization = client.DefaultRequestHeaders.Authorization;
            await Assert.ThrowsAsync<HttpRequestException>(() => Post(lostReply, $"{order.Id}/confirmacao", command, key));
            var committed = (await Get(client, order.Id))!; Assert.Equal("Confirmada", committed.Estado); Assert.NotNull(committed.Execucao);
            var before = await Snapshot();
            var replay = await Success<OrderView>(await Post(client, $"{order.Id}/confirmacao", command, key));
            Assert.Equal(committed.Execucao.Id, replay.Execucao!.Id);
            var recovered = (await client.GetFromJsonAsync<OrderView>($"/api/v1/ordens-producao/{order.Id}/comandos/confirmacao/{key}"))!;
            Assert.Equal(replay.Execucao.Id, recovered.Execucao!.Id); Assert.Equal(before, await Snapshot());
        }
    }

    private sealed class LoseConfirmedResponse(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var response = await base.SendAsync(request, ct);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            response.Dispose();
            throw new HttpRequestException("Synthetic transport failure after server committed the response.");
        }
    }

    [Fact]
    public async Task T17_reconciliation_reports_fixture_corruption_and_mutation_is_blocked_without_repair()
    {
        var (client, user) = await Session(true); using (client)
        {
            var f = await ScenarioAsync(client, user.UserId); var order = await Order(client, user.UserId, f);
            order = await Success<OrderView>(await Post(client, $"{order.Id}/inicio", Start(order, f, user.UserId)));
            // Deliberate external corruption is confined to this new ephemeral
            // fixture, restored in finally. Production exposes no repair path.
            await Factory.ExecuteDbContextAsync(db => db.Database.ExecuteSqlRawAsync($$"""
                ALTER TABLE "PosicoesEstoque" DISABLE TRIGGER "TR_PosicoesEstoque_Reconcile";
                UPDATE "PosicoesEstoque" SET "Quantidade"=101 WHERE "LoteId"='{{f.Input.Lot.Id}}'::uuid AND "LocalId"='{{f.Input.Local.Id}}'::uuid;
                SET CONSTRAINTS ALL IMMEDIATE;
                ALTER TABLE "PosicoesEstoque" ENABLE TRIGGER "TR_PosicoesEstoque_Reconcile";
                """));
            try
            {
                var before = await Snapshot();
                var reports = await client.GetFromJsonAsync<ProductionPage<ProductionReconciliation>>($"/api/v1/ordens-producao/reconciliacao?search={order.Codigo}&pageSize=100");
                var report = Assert.Single(reports!.Items, r => r.OrdemId == order.Id); Assert.False(report.Coerente); Assert.NotEmpty(report.Divergencias);
                var error = await Error(await Post(client, $"{order.Id}/confirmacao", Confirm(order, f, user.UserId)), HttpStatusCode.InternalServerError);
                Assert.DoesNotContain("SELECT", error); Assert.DoesNotContain("PosicoesEstoque", error);
                Assert.Equal(before, await Snapshot()); ReservationBalances((await Get(client, order.Id))!, 100, 100, 0, 0);
            }
            finally
            {
                await Factory.ExecuteDbContextAsync(db => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"PosicoesEstoque\" SET \"Quantidade\"=100 WHERE \"LoteId\"={f.Input.Lot.Id} AND \"LocalId\"={f.Input.Local.Id}"));
            }
        }
    }

    [Fact]
    public async Task T18_advisory_lock_timeout_is_sanitized_conflict_and_same_key_retries_once_after_release()
    {
        var (client, user) = await Session(); using (client)
        {
            var f = await ScenarioAsync(client, user.UserId); var order = await Order(client, user.UserId, f); var command = Start(order, f, user.UserId); var key = Unique(); var before = await Snapshot();
            var locked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var holder = Factory.ExecuteDbContextAsync(async db =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(); await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(413,421)"); locked.SetResult();
                await release.Task.WaitAsync(TimeSpan.FromSeconds(15)); await tx.CommitAsync();
            });
            try
            {
                await locked.Task.WaitAsync(TimeSpan.FromSeconds(10));
                var error = await Error(await Post(client, $"{order.Id}/inicio", command, key).WaitAsync(TimeSpan.FromSeconds(10)), HttpStatusCode.Conflict);
                Assert.Contains("conflito_transitorio", error); Assert.DoesNotContain("pg_advisory", error); Assert.Equal(before, await Snapshot());
            }
            finally { release.TrySetResult(); await holder; }
            order = await Success<OrderView>(await Post(client, $"{order.Id}/inicio", command, key));
            Assert.Equal("Iniciada", order.Estado); ReservationBalances(order, 100, 100, 0, 0);
        }
    }

    [Fact]
    public async Task T17_reconciliation_keeps_one_repeatable_snapshot_when_confirmation_commits_between_its_queries()
    {
        var (client, user) = await Session(true); using (client)
        {
            var f = await ScenarioAsync(client, user.UserId); var order = await Order(client, user.UserId, f);
            order = await Success<OrderView>(await Post(client, $"{order.Id}/inicio", Start(order, f, user.UserId)));
            var barrier = new ReconciliationReadBarrier();
            var isolatedConnection = await Factory.ExecuteDbContextAsync(db => Task.FromResult(db.Database.GetConnectionString()!));
            using var host = Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<GenSWDbContext>(); services.RemoveAll<DbContextOptions<GenSWDbContext>>();
                foreach (var stale in services.Where(d => d.ServiceType.IsGenericType && d.ServiceType.Name.StartsWith("IDbContextOptionsConfiguration", StringComparison.Ordinal) && d.ServiceType.GenericTypeArguments.Contains(typeof(GenSWDbContext))).ToArray()) services.Remove(stale);
                services.AddDbContext<GenSWDbContext>(options => options.UseNpgsql(isolatedConnection).AddInterceptors(barrier));
            }));
            using var reader = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false }); reader.DefaultRequestHeaders.Authorization = client.DefaultRequestHeaders.Authorization;
            var pending = reader.GetFromJsonAsync<ProductionPage<ProductionReconciliation>>($"/api/v1/ordens-producao/reconciliacao?search={order.Codigo}");
            try
            {
                await barrier.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.Equal("Confirmada", (await Success<OrderView>(await Post(client, $"{order.Id}/confirmacao", Confirm(order, f, user.UserId)))).Estado);
            }
            finally { barrier.Release.TrySetResult(); }
            var snapshot = Assert.Single((await pending)!.Items); Assert.Equal("Iniciada", snapshot.Estado); Assert.True(snapshot.Coerente);
            var latest = await client.GetFromJsonAsync<ProductionPage<ProductionReconciliation>>($"/api/v1/ordens-producao/reconciliacao?search={order.Codigo}");
            var report = Assert.Single(latest!.Items); Assert.Equal("Confirmada", report.Estado); Assert.True(report.Coerente);
        }
    }

    private sealed class ReconciliationReadBarrier : DbCommandInterceptor
    {
        private int intercepted;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken ct = default)
        {
            if (command.CommandText.Contains("FROM \"OrdensProducao\"", StringComparison.Ordinal) && Interlocked.Exchange(ref intercepted, 1) == 0)
            { Entered.TrySetResult(); await Release.Task.WaitAsync(TimeSpan.FromSeconds(15), ct); }
            return result;
        }
    }

    [Fact]
    public async Task T18_deliberately_inverted_external_lock_order_causes_sanitized_deadlock_conflict_then_safe_retry()
    {
        var (client, user) = await Session(); using (client)
        {
            var f = await ScenarioAsync(client, user.UserId); var order = await Order(client, user.UserId, f); var command = Start(order, f, user.UserId); var key = Unique(); var before = await Snapshot();
            var connectionString = await Factory.ExecuteDbContextAsync(db => Task.FromResult(db.Database.GetConnectionString()!));
            await using var blocker = new NpgsqlConnection(connectionString); await blocker.OpenAsync(); await using var transaction = await blocker.BeginTransactionAsync();
            await using (var lockInventory = new NpgsqlCommand("SET LOCAL deadlock_timeout='10s'; SELECT pg_advisory_xact_lock(430,1)", blocker, transaction)) await lockInventory.ExecuteNonQueryAsync();
            var pending = Post(client, $"{order.Id}/inicio", command, key);
            var observed = false; var deadline = DateTimeOffset.UtcNow.AddSeconds(4);
            while (DateTimeOffset.UtcNow < deadline)
            {
                var waits = await Factory.ExecuteDbContextAsync(db => db.Database.SqlQueryRaw<int>("SELECT count(*)::integer AS \"Value\" FROM pg_locks WHERE locktype='advisory' AND classid=430 AND objid=1 AND NOT granted").SingleAsync());
                if (waits > 0) { observed = true; break; } if (pending.IsCompleted) break; await Task.Delay(20);
            }
            Assert.True(observed, "The application must hold catalogue and wait for inventory in the documented order.");
            await using (var inverse = new NpgsqlCommand("SELECT pg_advisory_xact_lock(413,421)", blocker, transaction)) await inverse.ExecuteNonQueryAsync().WaitAsync(TimeSpan.FromSeconds(10));
            var error = await Error(await pending.WaitAsync(TimeSpan.FromSeconds(10)), HttpStatusCode.Conflict);
            Assert.Contains("conflito_transitorio", error); Assert.DoesNotContain("deadlock detected", error); Assert.Equal(before, await Snapshot());
            await transaction.RollbackAsync();
            order = await Success<OrderView>(await Post(client, $"{order.Id}/inicio", command, key)); ReservationBalances(order, 100, 100, 0, 0);
        }
    }
}
