using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GenSW.Application.Formulation;
using GenSW.Application.Inventory;
using GenSW.Domain.Catalog;
using GenSW.Domain.Inventory;
using GenSW.Infrastructure.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Xunit;

namespace GenSW.API.Tests;

[Collection(PostgreSqlAnimalIntegrationCollection.Name)]
#pragma warning disable EF1002 // Trigger fixture identifiers are generated Guids, never input SQL.
public sealed class InventoryApiTests(AnimalApiPostgreSqlFixture fixture)
{
    private AuthWebApplicationFactory Factory => fixture.Factory;
    private async Task<(HttpClient Client, SeededUser User)> Session(bool admin = false)
    {
        var user = await Factory.SeedUserAsync($"stock_{Guid.NewGuid():N}", roles: admin ? ["Admin"] : []);
        var client = Factory.CreateHttpsClient();
        using var login = await client.LoginAsync(user.UserName, user.Password);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.ReadAccessTokenAsync()).AccessToken);
        return (client, user);
    }
    private static async Task<HttpResponseMessage> Post(HttpClient client, string path, object command, string? key = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/estoque/" + path) { Content = JsonContent.Create(command) };
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
    private async Task<(CatalogItem Item, LoteView Lot, LocalView A, LocalView B)> Material(HttpClient client, Guid responsible, string unit = "kg", DateOnly? expiry = null)
    {
        using var itemResponse = await client.PostAsJsonAsync("/api/v1/itens", new ItemData(Guid.NewGuid().ToString("N"), "Material sintético F01", null, null, "Alimentar", unit, true, true, true, false));
        Assert.Equal(HttpStatusCode.Created, itemResponse.StatusCode);
        var item = (await itemResponse.Content.ReadFromJsonAsync<CatalogItem>())!;
        var a = await Created<LocalView>(await Post(client, "locais", new LocalCommand(Guid.NewGuid().ToString("N"), "Origem sintética")));
        var b = await Created<LocalView>(await Post(client, "locais", new LocalCommand(Guid.NewGuid().ToString("N"), "Destino sintético")));
        var lot = await Created<LoteView>(await Post(client, "lotes", new LoteCommand(item.Id, Guid.NewGuid().ToString("N"), "Inventário sintético", "Fixture isolada", responsible,
            Validade: expiry, FonteValidade: expiry is null ? null : "Fixture", ResponsavelValidadeId: expiry is null ? null : responsible, ItemVersaoEsperada: item.Revisao)));
        return (item, lot, a, b);
    }
    private static InventoryMovementCommand Command(CatalogItem item, LoteView lot, LocalView local, Guid responsible, string quantity, int position = 0) =>
        new(lot.Id, quantity, lot.Unidade, responsible, "Conferência física sintética", new(item.Revisao, lot.Revisao, local.Revisao, position), LocalId: local.Id,
            DataObservada: new DateOnly(2026, 1, 1), Origem: lot.Origem, Fonte: lot.Fonte, Destino: "Uso sintético", Evidencia: "Contagem física sintética, sem material real");
    private static Task<SaldoView?> Balance(HttpClient client, Guid lot, Guid local) => client.GetFromJsonAsync<SaldoView>($"/api/v1/estoque/saldos/{lot}/{local}");
    private static InventoryMovementCommand Transfer(CatalogItem item, LoteView lot, LocalView a, LocalView b, Guid responsible, string amount, int aVersion, int bVersion = 0) =>
        new(lot.Id, amount, lot.Unidade, responsible, "Transferência física sintética", new(item.Revisao, lot.Revisao, OrigemLocal: a.Revisao, DestinoLocal: b.Revisao, OrigemPosicao: aVersion, DestinoPosicao: bVersion),
            OrigemLocalId: a.Id, DestinoLocalId: b.Id, Evidencia: "Fixture isolada", Destino: "Local de destino conferido");
    private async Task<HttpResponseMessage[]> Race(Func<Task<HttpResponseMessage>> first, Func<Task<HttpResponseMessage>> second)
    {
        var locked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var holder = Factory.ExecuteDbContextAsync(async db =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(413, 421)"); locked.SetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(20)); await tx.CommitAsync();
        });
        Task<HttpResponseMessage>? one = null, two = null;
        try
        {
            await locked.Task.WaitAsync(TimeSpan.FromSeconds(10)); one = first(); two = second();
            var deadline = DateTimeOffset.UtcNow.AddSeconds(4); var bothBlocked = false;
            while (DateTimeOffset.UtcNow < deadline)
            {
                var count = await Factory.ExecuteDbContextAsync(async db =>
                {
                    await db.Database.OpenConnectionAsync(); await using var sql = db.Database.GetDbConnection().CreateCommand();
                    sql.CommandText = "SELECT count(*)::integer FROM pg_stat_activity WHERE datname=current_database() AND wait_event='advisory' AND query LIKE '%413%421%';";
                    return (int)(await sql.ExecuteScalarAsync())!;
                });
                if (count >= 2) { bothBlocked = true; break; }
                if (one.IsCompleted || two.IsCompleted) break;
                await Task.Delay(20);
            }
            Assert.True(bothBlocked, "Both PostgreSQL writers must wait on the catalogue advisory lock before release.");
        }
        finally { release.TrySetResult(); await holder; }
        return await Task.WhenAll(one!, two!).WaitAsync(TimeSpan.FromSeconds(15));
    }

    [Fact]
    public async Task Authenticated_metadata_zero_position_unit_fixation_history_and_readonly_preview()
    {
        using var anonymous = Factory.CreateHttpsClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/estoque/saldos")).StatusCode);
        var (client, user) = await Session(); using (client)
        {
            var f = await Material(client, user.UserId);
            var zero = (await Balance(client, f.Lot.Id, f.A.Id))!; Assert.Equal("0", zero.Quantidade); Assert.Equal(0, zero.Revisao);
            Assert.True((await client.GetFromJsonAsync<CatalogItem>($"/api/v1/itens/{f.Item.Id}"))!.UnidadeFixada);
            var command = Command(f.Item, f.Lot, f.A, user.UserId, "2");
            var before = await Factory.ExecuteDbContextAsync(db => db.Set<ComandoEstoque>().CountAsync());
            using var preview = await client.PostAsJsonAsync("/api/v1/estoque/previas", new InventoryPreviewCommand("Entrada", command));
            Assert.True(preview.StatusCode == HttpStatusCode.OK, await preview.Content.ReadAsStringAsync());
            Assert.Equal(before, await Factory.ExecuteDbContextAsync(db => db.Set<ComandoEstoque>().CountAsync()));
            Assert.Equal("0", (await Balance(client, f.Lot.Id, f.A.Id))!.Quantidade);
            var history = await client.GetFromJsonAsync<InventoryPage<HistoricoView>>($"/api/v1/estoque/lotes/{f.Lot.Id}/historico");
            Assert.NotEmpty(history!.Items); Assert.All(history.Items, h => Assert.Equal(user.UserId, h.AutorId));
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/estoque/locais?pageSize=101")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/estoque/lotes/{Guid.NewGuid()}" )).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/estoque/locais", new LocalCommand("KEY", "Sem chave"))).StatusCode);
        }
    }

    [Fact]
    public async Task Last_five_units_race_has_one_success_and_one_conflict_and_keeps_zero_row()
    {
        var (client, user) = await Session(true); using (client)
        {
            var f = await Material(client, user.UserId, "un");
            await Created<object>(await Post(client, "aberturas", Command(f.Item, f.Lot, f.A, user.UserId, "5")));
            var command = Command(f.Item, f.Lot, f.A, user.UserId, "5", 1);
            var responses = await Race(() => Post(client, "saidas-manuais", command), () => Post(client, "saidas-manuais", command));
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created); Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
            foreach (var r in responses) r.Dispose();
            var balance = (await Balance(client, f.Lot.Id, f.A.Id))!; Assert.Equal("0", balance.Quantidade); Assert.Equal(2, balance.Revisao);
            var events = await client.GetFromJsonAsync<InventoryPage<EventoView>>($"/api/v1/estoque/movimentos?loteId={f.Lot.Id}"); Assert.Equal(2, events!.TotalItems);
        }
    }

    [Fact]
    public async Task Transfer_has_equal_two_legs_one_event_and_observer_never_sees_partial_commit()
    {
        var (client, user) = await Session(true); using (client)
        {
            var f = await Material(client, user.UserId);
            await Created<object>(await Post(client, "aberturas", Command(f.Item, f.Lot, f.A, user.UserId, "10")));
            var command = Transfer(f.Item, f.Lot, f.A, f.B, user.UserId, "3", 1);
            // A delay on the destination credit makes the uncommitted debit observable
            // to a second session if transaction boundaries are accidentally split.
            await Factory.ExecuteDbContextAsync(db => db.Database.ExecuteSqlRawAsync($$"""
                CREATE FUNCTION "InventoryTestCreditDelay"() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
                  IF NEW."LocalId"='{{f.B.Id}}'::uuid THEN PERFORM pg_sleep(0.4); END IF; RETURN NEW; END; $$;
                CREATE TRIGGER "TR_InventoryTestCreditDelay" BEFORE INSERT OR UPDATE ON "PosicoesEstoque" FOR EACH ROW EXECUTE FUNCTION "InventoryTestCreditDelay"();
                """));
            try
            {
                var move = Post(client, "transferencias", command); var observations = 0;
                do
                {
                    var balances = await client.GetFromJsonAsync<InventoryPage<SaldoView>>($"/api/v1/estoque/saldos?loteId={f.Lot.Id}");
                    Assert.Equal(10m, balances!.Items.Sum(x => decimal.Parse(x.Quantidade, System.Globalization.CultureInfo.InvariantCulture))); observations++;
                    await Task.Delay(20);
                } while (!move.IsCompleted);
                var reply = await Created<MovementReply>(await move);
                Assert.Equal(2, reply.Evento.Movimentos.Count); Assert.All(reply.Evento.Movimentos, m => Assert.Equal("3", m.Quantidade));
                Assert.Equal("7", (await Balance(client, f.Lot.Id, f.A.Id))!.Quantidade); Assert.Equal("3", (await Balance(client, f.Lot.Id, f.B.Id))!.Quantidade);
                Assert.True(observations > 1);
                var list = await client.GetFromJsonAsync<InventoryPage<EventoView>>($"/api/v1/estoque/movimentos?loteId={f.Lot.Id}&tipo=Transferencia"); Assert.Single(list!.Items);
            }
            finally { await Factory.ExecuteDbContextAsync(db => db.Database.ExecuteSqlRawAsync("DROP TRIGGER \"TR_InventoryTestCreditDelay\" ON \"PosicoesEstoque\"; DROP FUNCTION \"InventoryTestCreditDelay\"();")); }
        }
    }

    [Fact]
    public async Task Injected_failure_after_debit_rolls_back_positions_ledger_audit_and_replay()
    {
        var (client, user) = await Session(true); using (client)
        {
            var f = await Material(client, user.UserId);
            await Created<object>(await Post(client, "aberturas", Command(f.Item, f.Lot, f.A, user.UserId, "10")));
            var before = await Counts(); var key = Guid.NewGuid().ToString("N");
            await Factory.ExecuteDbContextAsync(db => db.Database.ExecuteSqlRawAsync($$"""
                CREATE FUNCTION "InventoryTestRejectCredit"() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
                  IF NEW."LocalId"='{{f.B.Id}}'::uuid THEN RAISE EXCEPTION 'Injected isolated credit failure' USING ERRCODE='P0001'; END IF; RETURN NEW; END; $$;
                CREATE TRIGGER "TR_InventoryTestRejectCredit" BEFORE INSERT OR UPDATE ON "PosicoesEstoque" FOR EACH ROW EXECUTE FUNCTION "InventoryTestRejectCredit"();
                """));
            try
            {
                await Assert.ThrowsAsync<DbUpdateException>(() => Factory.ExecuteScopeAsync(s => s.GetRequiredService<InventoryService>().MoveAsync("Transferencia", Transfer(f.Item, f.Lot, f.A, f.B, user.UserId, "3", 1), key, user.UserId, default)));
                Assert.Equal(before, await Counts());
                Assert.Equal("10", (await Balance(client, f.Lot.Id, f.A.Id))!.Quantidade); Assert.Equal(0, (await Balance(client, f.Lot.Id, f.B.Id))!.Revisao);
            }
            finally { await Factory.ExecuteDbContextAsync(db => db.Database.ExecuteSqlRawAsync("DROP TRIGGER \"TR_InventoryTestRejectCredit\" ON \"PosicoesEstoque\"; DROP FUNCTION \"InventoryTestRejectCredit\"();")); }
            await Created<object>(await Post(client, "transferencias", Transfer(f.Item, f.Lot, f.A, f.B, user.UserId, "3", 1), key));
        }
    }
    private Task<(int Events, int Lines, int Audits, int Commands)> Counts() => Factory.ExecuteDbContextAsync(async db =>
        (await db.Set<EventoEstoque>().CountAsync(), await db.Set<MovimentoEstoque>().CountAsync(), await db.Set<HistoricoEstoque>().CountAsync(), await db.Set<ComandoEstoque>().CountAsync()));

    [Fact]
    public async Task Idempotent_race_equivalent_decimals_replay_original_bytes_after_inactivation_and_reject_changed_payload()
    {
        var (client, user) = await Session(true); using (client)
        {
            var f = await Material(client, user.UserId); var key = Guid.NewGuid().ToString("N"); var command = Command(f.Item, f.Lot, f.A, user.UserId, "10");
            var replies = await Race(() => Post(client, "entradas", command, key), () => Post(client, "entradas", command with { Quantidade = "10.000000" }, key));
            Assert.All(replies, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
            var original = await replies[0].Content.ReadAsStringAsync(); Assert.Equal(original, await replies[1].Content.ReadAsStringAsync());
            var location = replies[0].Headers.Location; foreach (var r in replies) r.Dispose();
            using var status = await client.PatchAsJsonAsync($"/api/v1/itens/{f.Item.Id}/ativo", new StatusCommand(false, 1)); Assert.Equal(HttpStatusCode.OK, status.StatusCode);
            using var replay = await Post(client, "entradas", command, key);
            Assert.Equal(HttpStatusCode.Created, replay.StatusCode); Assert.Equal(original, await replay.Content.ReadAsStringAsync()); Assert.Equal(location, replay.Headers.Location);
            Assert.Equal("true", replay.Headers.GetValues("Idempotency-Replayed").Single());
            using var changed = await Post(client, "entradas", command with { Motivo = "Outro motivo" }, key); Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
            using var newKey = await Post(client, "entradas", command with { VersoesEsperadas = command.VersoesEsperadas with { Item = 2, Posicao = 1 } }); Assert.Equal(HttpStatusCode.Conflict, newKey.StatusCode);
        }
    }

    [Fact]
    public async Task Common_user_denied_admin_preview_and_command_and_removed_current_role_denies_replay()
    {
        var (common, actor) = await Session(); using (common)
        {
            var f = await Material(common, actor.UserId); var command = Command(f.Item, f.Lot, f.A, actor.UserId, "5");
            foreach (var operation in new[] { "Abertura", "Ajuste", "Segregacao", "RetornoSegregacao", "Descarte" })
            {
                using var preview = await common.PostAsJsonAsync("/api/v1/estoque/previas", new InventoryPreviewCommand(operation, command)); Assert.Equal(HttpStatusCode.Forbidden, preview.StatusCode);
            }
            using var open = await Post(common, "aberturas", command); Assert.Equal(HttpStatusCode.Forbidden, open.StatusCode);
            await Created<object>(await Post(common, "entradas", command));
        }
        var (admin, user) = await Session(true); using (admin)
        {
            var f = await Material(admin, user.UserId); var command = Command(f.Item, f.Lot, f.A, user.UserId, "5"); var key = Guid.NewGuid().ToString("N");
            await Created<object>(await Post(admin, "aberturas", command, key));
            await Factory.ExecuteScopeAsync(async s =>
            {
                var users = s.GetRequiredService<UserManager<ApplicationUser>>(); var current = (await users.FindByIdAsync(user.UserId.ToString()))!;
                Assert.True((await users.RemoveFromRoleAsync(current, "Admin")).Succeeded); return 0;
            });
            using var replay = await Post(admin, "aberturas", command, key); Assert.Equal(HttpStatusCode.Forbidden, replay.StatusCode);
            await Factory.SetUserActiveAsync(user.UserId, false);
            using var inactive = await Post(admin, "entradas", command); Assert.Equal(HttpStatusCode.Unauthorized, inactive.StatusCode);
        }
    }

    [Fact]
    public async Task Quantization_preview_requires_exact_acceptance_and_fractional_count_is_never_rounded()
    {
        var (client, user) = await Session(); using (client)
        {
            var f = await Material(client, user.UserId); var command = Command(f.Item, f.Lot, f.A, user.UserId, "0.0015") with { Unidade = "g" };
            using var previewResponse = await client.PostAsJsonAsync("/api/v1/estoque/previas", new InventoryPreviewCommand("Entrada", command));
            Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode); var preview = (await previewResponse.Content.ReadFromJsonAsync<InventoryPreview>())!;
            Assert.Equal("0.0000015", preview.Quantidade.Calculada); Assert.Equal("0.000002", preview.Quantidade.Normalizada); Assert.Equal("-0.0000005", preview.Quantidade.Residuo);
            using var missing = await Post(client, "entradas", command); Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
            var acceptance = new QuantizationAcceptance(preview.Quantidade.Calculada, preview.Quantidade.Normalizada, preview.Quantidade.Residuo, "Resolução conferida na fixture");
            using var divergent = await Post(client, "entradas", command with { AceiteQuantizacao = acceptance with { Normalizado = "0.000001" } }); Assert.Equal(HttpStatusCode.BadRequest, divergent.StatusCode);
            await Created<object>(await Post(client, "entradas", command with { AceiteQuantizacao = acceptance }));
            Assert.Equal("0.000002", (await Balance(client, f.Lot.Id, f.A.Id))!.Quantidade);
            var counted = await Material(client, user.UserId, "un");
            using var fractional = await Post(client, "entradas", Command(counted.Item, counted.Lot, counted.A, user.UserId, "1.5")); Assert.Equal(HttpStatusCode.BadRequest, fractional.StatusCode);
        }
    }

    [Fact]
    public async Task Catalogue_inactivation_and_physical_withdrawal_are_serialized_by_same_lock()
    {
        var (client, user) = await Session(true); using (client)
        {
            var f = await Material(client, user.UserId); await Created<object>(await Post(client, "aberturas", Command(f.Item, f.Lot, f.A, user.UserId, "5")));
            var responses = await Race(() => client.PatchAsJsonAsync($"/api/v1/itens/{f.Item.Id}/ativo", new StatusCommand(false, 1)),
                () => Post(client, "saidas-manuais", Command(f.Item, f.Lot, f.A, user.UserId, "5", 1)));
            Assert.Equal(HttpStatusCode.OK, responses[0].StatusCode); Assert.Contains(responses[1].StatusCode, new[] { HttpStatusCode.Created, HttpStatusCode.Conflict });
            var remaining = (await Balance(client, f.Lot.Id, f.A.Id))!;
            Assert.Equal(responses[1].StatusCode == HttpStatusCode.Created ? "0" : "5", remaining.Quantidade); Assert.False(remaining.Elegivel);
            foreach (var r in responses) r.Dispose();
        }
    }

    [Fact]
    public async Task Clock_advancing_while_writer_waits_denies_expired_ordinary_use()
    {
        var (client, user) = await Session(true); using (client)
        {
            var operationalDate = InventoryRules.Today(TimeProvider.System);
            var f = await Material(client, user.UserId, expiry: operationalDate);
            await Created<object>(await Post(client, "aberturas", Command(f.Item, f.Lot, f.A, user.UserId, "5")));
            var clock = new InventoryClock(new DateTimeOffset(operationalDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).AddDays(1).AddHours(3).AddMinutes(-1));
            using var host = Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => { services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(clock); }));
            using var timed = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false }); timed.DefaultRequestHeaders.Authorization = client.DefaultRequestHeaders.Authorization;
            var locked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var holder = Factory.ExecuteDbContextAsync(async db => { await using var tx = await db.Database.BeginTransactionAsync(); await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(413,421)"); locked.SetResult(); await release.Task.WaitAsync(TimeSpan.FromSeconds(20)); await tx.CommitAsync(); });
            Task<HttpResponseMessage>? command = null;
            try
            {
                await locked.Task; command = Post(timed, "saidas-manuais", Command(f.Item, f.Lot, f.A, user.UserId, "1", 1));
                var observed = false; var deadline = DateTimeOffset.UtcNow.AddSeconds(4);
                while (DateTimeOffset.UtcNow < deadline)
                {
                    var blocked = await Factory.ExecuteDbContextAsync(db => db.Database.SqlQueryRaw<int>("SELECT count(*)::integer AS \"Value\" FROM pg_stat_activity WHERE datname=current_database() AND wait_event='advisory' AND query LIKE '%413%421%'").SingleAsync());
                    if (blocked > 0) { observed = true; break; } await Task.Delay(20);
                }
                Assert.True(observed, "The inventory writer must wait before crossing the operational midnight.");
                clock.Advance(TimeSpan.FromMinutes(2));
            }
            finally { release.TrySetResult(); await holder; }
            using var response = await command!; Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("5", (await Balance(client, f.Lot.Id, f.A.Id))!.Quantidade);
        }
    }

    [Fact]
    public async Task Segregation_blocks_whole_lot_release_and_return_are_explicit_and_discard_keeps_history()
    {
        var (client, user) = await Session(true); using (client)
        {
            var f = await Material(client, user.UserId);
            var segregation = await Created<LocalView>(await Post(client, "locais", new LocalCommand(Guid.NewGuid().ToString("N"), "Segregação sintética", Finalidade: "Segregacao")));
            await Created<object>(await Post(client, "aberturas", Command(f.Item, f.Lot, f.A, user.UserId, "5")));
            await Created<object>(await Post(client, "segregacoes", Transfer(f.Item, f.Lot, f.A, segregation, user.UserId, "3", 1)));
            var lot = (await client.GetFromJsonAsync<LoteView>($"/api/v1/estoque/lotes/{f.Lot.Id}"))!; Assert.Equal("Bloqueado", lot.Situacao);
            Assert.Equal("2", (await Balance(client, lot.Id, f.A.Id))!.Quantidade); Assert.False((await Balance(client, lot.Id, f.A.Id))!.Elegivel);
            using var denied = await Post(client, "consumos-internos", Command(f.Item, lot, f.A, user.UserId, "1", 2)); Assert.Equal(HttpStatusCode.Conflict, denied.StatusCode);
            using var release = await Post(client, $"lotes/{lot.Id}/liberacao", new InventoryStateCommand(lot.Revisao, "Conferência final", "Material presente e admissível")); Assert.Equal(HttpStatusCode.OK, release.StatusCode);
            lot = InventoryJson.Read<LoteView>(await release.Content.ReadAsStringAsync());
            await Created<object>(await Post(client, "retornos-segregacao", Transfer(f.Item, lot, segregation, f.B, user.UserId, "2", 1)));
            await Created<object>(await Post(client, "descartes", Command(f.Item, lot, segregation, user.UserId, "1", 2)));
            Assert.Equal("0", (await Balance(client, lot.Id, segregation.Id))!.Quantidade);
            await Created<object>(await Post(client, "ajustes", Command(f.Item, lot, f.A, user.UserId, "3", 2) with { QuantidadeContada = "3" }));
            await Created<object>(await Post(client, "consumos-internos", Command(f.Item, lot, f.A, user.UserId, "1", 3)));
            Assert.Equal("2", (await Balance(client, lot.Id, f.A.Id))!.Quantidade); Assert.Equal("2", (await Balance(client, lot.Id, f.B.Id))!.Quantidade);
            var events = await client.GetFromJsonAsync<InventoryPage<EventoView>>($"/api/v1/estoque/movimentos?loteId={lot.Id}"); Assert.Equal(6, events!.TotalItems);
            using var close = await Post(client, $"lotes/{lot.Id}/encerramento", new InventoryStateCommand(lot.Revisao, "Encerramento", "Conferência")); Assert.Equal(HttpStatusCode.Conflict, close.StatusCode);
        }
    }

    [Fact]
    public async Task Reconciliation_reports_corruption_without_writes_and_sequence_above_javascript_limit_stays_string()
    {
        var (client, user) = await Session(true); using (client)
        {
            var f = await Material(client, user.UserId); await Created<object>(await Post(client, "aberturas", Command(f.Item, f.Lot, f.A, user.UserId, "5")));
            var before = await Counts();
            // This fixture represents external corruption. Only the isolated test DB
            // disables its projection guard; no application repair command exists.
            await Factory.ExecuteDbContextAsync(db => db.Database.ExecuteSqlRawAsync($$"""
                ALTER TABLE "PosicoesEstoque" DISABLE TRIGGER "TR_PosicoesEstoque_Reconcile";
                UPDATE "PosicoesEstoque" SET "Quantidade"=6 WHERE "LoteId"='{{f.Lot.Id}}'::uuid AND "LocalId"='{{f.A.Id}}'::uuid;
                ALTER TABLE "PosicoesEstoque" ENABLE TRIGGER "TR_PosicoesEstoque_Reconcile";
                """));
            try
            {
                for (var i = 0; i < 2; i++)
                {
                    var report = await client.GetFromJsonAsync<InventoryPage<ReconciliacaoView>>($"/api/v1/estoque/reconciliacao?loteId={f.Lot.Id}");
                    var delta = Assert.Single(report!.Items); Assert.Equal("5", delta.QuantidadeLivro); Assert.Equal("6", delta.QuantidadeProjecao); Assert.Equal("1", delta.Diferenca);
                }
                Assert.Equal(before, await Counts()); Assert.Equal("6", (await Balance(client, f.Lot.Id, f.A.Id))!.Quantidade);
            }
            finally { await Factory.ExecuteDbContextAsync(db => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"PosicoesEstoque\" SET \"Quantidade\"=5 WHERE \"LoteId\"={f.Lot.Id} AND \"LocalId\"={f.A.Id}")); }
            await Factory.ExecuteDbContextAsync(db => db.Database.ExecuteSqlRawAsync("SELECT setval('\"SequenciaEstoque\"',9007199254740993,true)"));
            var result = await Created<MovementReply>(await Post(client, "consumos-internos", Command(f.Item, f.Lot, f.A, user.UserId, "1", 1)));
            Assert.Equal("9007199254740994", result.Evento.Sequencia);
            var book = await client.GetFromJsonAsync<InventoryPage<EventoView>>($"/api/v1/estoque/movimentos?loteId={f.Lot.Id}&seqDe=9007199254740994&seqAte=9007199254740994");
            Assert.Equal(result.Evento.Id, Assert.Single(book!.Items).Id);
        }
    }

    [Fact]
    public async Task Escaped_search_paging_and_inactive_responsible_lookup_preserve_historical_labels()
    {
        var (client, user) = await Session(); using (client)
        {
            var prefix = Guid.NewGuid().ToString("N") + "%_";
            for (var i = 0; i < 3; i++) await Created<LocalView>(await Post(client, "locais", new LocalCommand(prefix + i, "Busca literal")));
            var page = await client.GetFromJsonAsync<InventoryPage<LocalView>>($"/api/v1/estoque/locais?search={Uri.EscapeDataString(prefix)}&page=2&pageSize=2");
            Assert.Equal(3, page!.TotalItems); Assert.Single(page.Items); Assert.Equal(2, page.Page);
            var f = await Material(client, user.UserId);
            var inactive = await Factory.SeedUserAsync("historico_" + Guid.NewGuid().ToString("N"), isActive: false);
            var byId = await client.GetFromJsonAsync<ResponsavelView>($"/api/v1/estoque/responsaveis/{inactive.UserId}"); Assert.Equal(inactive.Nome, byId!.Nome); Assert.False(byId.Ativo);
            using var receipt = await Post(client, "entradas", Command(f.Item, f.Lot, f.A, inactive.UserId, "1")); Assert.Equal(HttpStatusCode.Conflict, receipt.StatusCode);
        }
    }

    private sealed class InventoryClock(DateTimeOffset initial) : TimeProvider
    {
        private long ticks = initial.UtcTicks;
        public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref ticks), TimeSpan.Zero);
        public void Advance(TimeSpan value) => Interlocked.Add(ref ticks, value.Ticks);
    }
    private sealed record CatalogItem(Guid Id, int Revisao, bool UnidadeFixada);
    private sealed record MovementReply(EventoView Evento, InventoryPreview Previa);
}
