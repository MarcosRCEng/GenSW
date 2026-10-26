using System.Data;
using System.Net;
using GenSW.Application.Production;
using GenSW.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace GenSW.API.Tests;

#pragma warning disable EF1002 // Fixture-only trigger names and conditions are fixed InlineData constants.
public sealed partial class ProductionApiTests
{
    [Fact]
    public async Task T16_direct_sql_cannot_settle_one_of_multiple_reservations_without_a_physical_execution()
    {
        var (client, user) = await Session(); using (client)
        {
            var f = await ScenarioAsync(client, user.UserId); var second = await Stock(client, user.UserId, "10");
            var data = f.Version.Conteudo with { Entradas = [f.Version.Conteudo.Entradas[0], new(Guid.NewGuid(), second.Item.Id, "Alimentar", "10", "kg", "Variavel")] };
            f = f with { Version = await RecipeAsync(user.UserId, f.Input.Item.Id, f.Output.Id, data: data) };
            var order = await Order(client, user.UserId, f); var start = Start(order, f, user.UserId);
            start = start with { Entradas = [start.Entradas[0], new(data.Entradas[1].Id, [new(Guid.NewGuid(), second.Lot.Id, second.Local.Id, "10", "kg")])] };
            order = await Success<OrderView>(await Post(client, $"{order.Id}/inicio", start));
            var reservation = order.Reservas.Single(r => r.LoteId == f.Input.Lot.Id); var before = await Snapshot();
            var failure = await Assert.ThrowsAsync<PostgresException>(() => Factory.ExecuteDbContextAsync(db => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"ReservasProducao\" SET \"QuantidadeAtiva\"=0,\"QuantidadeConsumida\"=\"QuantidadeOriginal\",\"Revisao\"=\"Revisao\"+1 WHERE \"Id\"={reservation.Id}")));
            Assert.Equal("23514", failure.SqlState); Assert.Equal(before, await Snapshot());
            Assert.Equal("Iniciada", (await Get(client, order.Id))!.Estado); ReservationBalances((await Get(client, order.Id))!, 110, 110, 0, 0);
        }
    }

    [Fact]
    public async Task T16_direct_sql_cannot_add_consumed_reservation_without_a_matching_confirmed_debit()
    {
        var (client, user) = await Session(); using (client)
        {
            var f = await ScenarioAsync(client, user.UserId); var order = await Order(client, user.UserId, f);
            order = await Success<OrderView>(await Post(client, $"{order.Id}/inicio", Start(order, f, user.UserId)));
            order = await Success<OrderView>(await Post(client, $"{order.Id}/confirmacao", Confirm(order, f, user.UserId)));
            var existing = Assert.Single(order.Reservas); var id = Guid.NewGuid(); var before = await Snapshot();
            var failure = await Assert.ThrowsAsync<PostgresException>(() => Factory.ExecuteDbContextAsync(db => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"ReservasProducao\" (\"Id\",\"OrdemId\",\"LinhaId\",\"LoteId\",\"LocalId\",\"Unidade\",\"QuantidadeOriginal\",\"QuantidadeAtiva\",\"QuantidadeConsumida\",\"QuantidadeLiberada\",\"SnapshotJson\",\"Revisao\",\"CreatedAtUtc\") SELECT {id},\"OrdemId\",\"LinhaId\",\"LoteId\",\"LocalId\",\"Unidade\",1,0,1,0,jsonb_set(\"SnapshotJson\",'{{alocacao,id}}',to_jsonb({id.ToString()}::text)),2,now() FROM \"ReservasProducao\" WHERE \"Id\"={existing.Id}")));
            Assert.Equal("23514", failure.SqlState); Assert.Equal(before, await Snapshot());
        }
    }

    [Fact]
    public async Task T16_produced_lot_metadata_cannot_change_without_advancing_its_revision()
    {
        var (client, user) = await Session(true); using (client)
        {
            var f = await ScenarioAsync(client, user.UserId); var order = await Order(client, user.UserId, f);
            order = await Success<OrderView>(await Post(client, $"{order.Id}/inicio", Start(order, f, user.UserId)));
            order = await Success<OrderView>(await Post(client, $"{order.Id}/confirmacao", Confirm(order, f, user.UserId)));
            var lot = Assert.Single(order.Execucao!.LotesSaida); var before = await Snapshot();
            var failure = await Assert.ThrowsAsync<PostgresException>(() => Factory.ExecuteDbContextAsync(db => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"LotesMateriais\" SET \"CodigoExterno\"='alteracao-sem-revisao' WHERE \"Id\"={lot.LoteId}")));
            Assert.Equal("23514", failure.SqlState); Assert.Equal(before, await Snapshot());
            Assert.Contains("reversao", (await Get(client, order.Id))!.AcoesPermitidas);
        }
    }

    [Fact]
    public async Task T16_direct_sql_production_debit_without_execution_is_rejected_and_rolls_back_its_projection()
    {
        var (client, user) = await Session(); using (client)
        {
            var f = await ScenarioAsync(client, user.UserId); var before = await Snapshot();
            await Factory.ExecuteDbContextAsync(async db =>
            {
                await using var transaction = await db.Database.BeginTransactionAsync();
                var position = await db.PosicoesEstoque.SingleAsync(p => p.LoteId == f.Input.Lot.Id && p.LocalId == f.Input.Local.Id);
                var eventId = Guid.NewGuid(); var now = DateTimeOffset.UtcNow; var today = InventoryRules.Today(TimeProvider.System);
                var sequence = await db.EventosEstoque.MaxAsync(e => e.Sequencia) + 100;
                var snapshot = InventoryJson.Write(new
                {
                    previa = new { quantidade = new { declarada = "1", unidadeDeclarada = "kg", calculada = "1", normalizada = "1", residuo = "0", unidade = "kg", exigeAceite = false } },
                    pedido = new { aceiteQuantizacao = (object?)null },
                    producao = new { execucaoId = Guid.NewGuid(), linhaId = f.Version.Conteudo.Entradas[0].Id }
                });
                position.SetBalance(99, position.Revisao);
                db.AddRange(new EventoEstoque(eventId, sequence, "ProducaoConsumo", user.UserId, user.UserId, now, today, null, Evidence, null, null, "gensw-production/1.0", snapshot),
                    new MovimentoEstoque(Guid.NewGuid(), eventId, 1, f.Input.Lot.Id, f.Input.Local.Id, f.Input.Item.Id, "kg", "Saida", 1, "1", "kg", "0", 100, 99, null, null, snapshot, "ProducaoConsumo"));
                await db.SaveChangesAsync();
                var failure = await Assert.ThrowsAsync<PostgresException>(() => transaction.CommitAsync());
                Assert.Equal("23514", failure.SqlState);
            });
            Assert.Equal(before, await Snapshot()); Assert.Equal("100", (await Balance(client, f.Input))!.Quantidade);
        }
    }

    [Fact]
    public async Task T16_direct_sql_cannot_append_a_new_original_consumption_to_a_confirmed_execution()
    {
        var (client, user) = await Session(); using (client)
        {
            var f = await ScenarioAsync(client, user.UserId, stock: "101"); var order = await Order(client, user.UserId, f);
            order = await Success<OrderView>(await Post(client, $"{order.Id}/inicio", Start(order, f, user.UserId)));
            order = await Success<OrderView>(await Post(client, $"{order.Id}/confirmacao", Confirm(order, f, user.UserId)));
            var before = await Snapshot(); var execution = order.Execucao!.Id;
            await Factory.ExecuteDbContextAsync(async db =>
            {
                var position = await db.PosicoesEstoque.SingleAsync(p => p.LoteId == f.Input.Lot.Id && p.LocalId == f.Input.Local.Id);
                var now = DateTimeOffset.UtcNow; var eventId = Guid.NewGuid(); var movementId = Guid.NewGuid();
                var line = f.Version.Conteudo.Entradas[0].Id;
                var snapshot = InventoryJson.Write(new
                {
                    previa = new { quantidade = new { declarada = "1", unidadeDeclarada = "kg", calculada = "1", normalizada = "1", residuo = "0", unidade = "kg", exigeAceite = false } },
                    pedido = new { aceiteQuantizacao = (object?)null },
                    producao = new { execucaoId = execution, linhaId = line }
                });
                position.SetBalance(0, position.Revisao);
                db.AddRange(new EventoEstoque(eventId, await db.EventosEstoque.MaxAsync(e => e.Sequencia) + 100, "ProducaoConsumo", user.UserId, user.UserId, now, InventoryRules.Today(TimeProvider.System), null, Evidence, null, null, "gensw-production/1.0", snapshot),
                    new MovimentoEstoque(movementId, eventId, 1, f.Input.Lot.Id, f.Input.Local.Id, f.Input.Item.Id, "kg", "Saida", 1, "1", "kg", "0", 1, 0, null, null, snapshot, "ProducaoConsumo"),
                    new GenSW.Domain.Production.ExecucaoMovimento(Guid.NewGuid(), execution, line, Guid.NewGuid(), movementId, "Entrada"));
                var failure = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
                Assert.Equal("23514", Assert.IsType<PostgresException>(failure.InnerException).SqlState);
            });
            Assert.Equal(before, await Snapshot()); Assert.Equal("1", (await Balance(client, f.Input))!.Quantidade);
        }
    }

    [Fact]
    public async Task T16_false_output_state_snapshot_is_rejected_with_complete_rollback()
    {
        var (client, user) = await Session(); using (client)
        {
            var f = await ScenarioAsync(client, user.UserId); var order = await Order(client, user.UserId, f);
            order = await Success<OrderView>(await Post(client, $"{order.Id}/inicio", Start(order, f, user.UserId)));
            var command = Confirm(order, f, user.UserId); var key = Unique(); var before = await Snapshot();
            await Factory.ExecuteDbContextAsync(db => db.Database.ExecuteSqlRawAsync("""
                CREATE FUNCTION "ProductionTestFalseOutputState"() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN NEW."SituacaoOriginal":='Bloqueado'; RETURN NEW; END $$;
                CREATE TRIGGER "TR_ProductionTestFalseOutputState" BEFORE INSERT ON "SaidasProducao"
                  FOR EACH ROW EXECUTE FUNCTION "ProductionTestFalseOutputState"();
                """));
            try
            {
                await Error(await Post(client, $"{order.Id}/confirmacao", command, key), HttpStatusCode.InternalServerError);
                Assert.Equal(before, await Snapshot()); ReservationBalances((await Get(client, order.Id))!, 100, 100, 0, 0);
            }
            finally
            {
                await Factory.ExecuteDbContextAsync(db => db.Database.ExecuteSqlRawAsync("""
                    DROP TRIGGER "TR_ProductionTestFalseOutputState" ON "SaidasProducao";
                    DROP FUNCTION "ProductionTestFalseOutputState"();
                    """));
            }
            Assert.Equal("Confirmada", (await Success<OrderView>(await Post(client, $"{order.Id}/confirmacao", command, key))).Estado);
        }
    }

    [Theory]
    [InlineData("ExecucoesProducao", "INSERT", "true")]
    [InlineData("MovimentosEstoque", "INSERT", "NEW.\"Sentido\"='Saida'")]
    [InlineData("LotesMateriais", "INSERT", "true")]
    [InlineData("PosicoesEstoque", "INSERT", "true")]
    [InlineData("ExecucaoMovimentos", "INSERT", "true")]
    [InlineData("SaidasProducao", "INSERT", "true")]
    [InlineData("GenealogiaMateriais", "INSERT", "true")]
    [InlineData("ReservasProducao", "UPDATE", "NEW.\"QuantidadeAtiva\"=0")]
    [InlineData("ComandosProducao", "INSERT", "true")]
    public async Task T08_every_persistence_phase_rolls_back_all_facts_and_same_key_can_retry(string table, string operation, string condition)
    {
        var (client, user) = await Session(); using (client)
        {
            var f = await ScenarioAsync(client, user.UserId); var order = await Order(client, user.UserId, f);
            order = await Success<OrderView>(await Post(client, $"{order.Id}/inicio", Start(order, f, user.UserId)));
            var command = Confirm(order, f, user.UserId); var key = Unique(); var before = await Snapshot();
            await Factory.ExecuteDbContextAsync(db => db.Database.ExecuteSqlRawAsync($$"""
                CREATE FUNCTION "ProductionTestRejectPhase"() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN RAISE EXCEPTION 'Synthetic isolated fault' USING ERRCODE='P0001'; END $$;
                CREATE TRIGGER "TR_ProductionTestRejectPhase" BEFORE {{operation}} ON "{{table}}"
                  FOR EACH ROW WHEN ({{condition}}) EXECUTE FUNCTION "ProductionTestRejectPhase"();
                """));
            try
            {
                var error = await Error(await Post(client, $"{order.Id}/confirmacao", command, key), HttpStatusCode.InternalServerError);
                Assert.DoesNotContain("Synthetic isolated fault", error); Assert.DoesNotContain("P0001", error);
                Assert.Equal(before, await Snapshot()); ReservationBalances((await Get(client, order.Id))!, 100, 100, 0, 0);
            }
            finally
            {
                await Factory.ExecuteDbContextAsync(db => db.Database.ExecuteSqlRawAsync($$"""
                    DROP TRIGGER "TR_ProductionTestRejectPhase" ON "{{table}}";
                    DROP FUNCTION "ProductionTestRejectPhase"();
                    """));
            }
            Assert.Equal("Confirmada", (await Success<OrderView>(await Post(client, $"{order.Id}/confirmacao", command, key))).Estado);
        }
    }

    [Fact]
    public async Task T16_direct_sql_cannot_edit_reservation_identity_or_exceed_physical_balance()
    {
        var (client, user) = await Session(); using (client)
        {
            var f = await ScenarioAsync(client, user.UserId); var order = await Order(client, user.UserId, f);
            order = await Success<OrderView>(await Post(client, $"{order.Id}/inicio", Start(order, f, user.UserId)));
            var reservation = Assert.Single(order.Reservas); var before = await Snapshot();
            var invalid = new FormattableString[] {
                $"UPDATE \"ReservasProducao\" SET \"QuantidadeOriginal\"=101,\"QuantidadeAtiva\"=101,\"Revisao\"=\"Revisao\"+1 WHERE \"Id\"={reservation.Id}",
                $"UPDATE \"ReservasProducao\" SET \"QuantidadeAtiva\"=-1 WHERE \"Id\"={reservation.Id}",
                $"UPDATE \"ReservasProducao\" SET \"SnapshotJson\"='{{}}'::jsonb,\"Revisao\"=\"Revisao\"+1 WHERE \"Id\"={reservation.Id}",
                $"UPDATE \"ReservasProducao\" SET \"Unidade\"='L',\"Revisao\"=\"Revisao\"+1 WHERE \"Id\"={reservation.Id}",
                $"DELETE FROM \"ReservasProducao\" WHERE \"Id\"={reservation.Id}",
                $"UPDATE \"OrdensProducao\" SET \"Estado\"='Confirmada',\"Revisao\"=\"Revisao\"+1 WHERE \"Id\"={order.Id}"
            };
            foreach (var statement in invalid)
            {
                var failure = await Assert.ThrowsAsync<PostgresException>(() => Factory.ExecuteDbContextAsync(db => db.Database.ExecuteSqlInterpolatedAsync(statement)));
                Assert.Equal("23514", failure.SqlState);
            }
            Assert.Equal(before, await Snapshot());
        }
    }

    [Theory]
    [InlineData(IsolationLevel.ReadCommitted)]
    [InlineData(IsolationLevel.RepeatableRead)]
    public async Task T16_noncooperating_sql_writers_serialize_on_position_without_application_advisory_locks(IsolationLevel isolation)
    {
        var (client, user) = await Session(); using (client)
        {
            var f = await ScenarioAsync(client, user.UserId, planned: "60", output: "58");
            var a = await Order(client, user.UserId, f); var b = await Order(client, user.UserId, f);
            var connectionString = await Factory.ExecuteDbContextAsync(db => Task.FromResult(db.Database.GetConnectionString()!));
            await using var first = new NpgsqlConnection(connectionString); await first.OpenAsync();
            await using var second = new NpgsqlConnection(connectionString); await second.OpenAsync();
            await using var tx1 = await first.BeginTransactionAsync(isolation); await using var tx2 = await second.BeginTransactionAsync(isolation);
            await DirectReservation(first, tx1, a.Id, f);
            var secondWrite = DirectReservation(second, tx2, b.Id, f);
            var blocked = false; var deadline = DateTimeOffset.UtcNow.AddSeconds(4);
            while (DateTimeOffset.UtcNow < deadline)
            {
                await using var query = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM pg_stat_activity WHERE pid=@pid AND wait_event_type='Lock')", first, tx1); query.Parameters.AddWithValue("pid", second.ProcessID);
                if ((bool)(await query.ExecuteScalarAsync())!) { blocked = true; break; }
                if (secondWrite.IsCompleted) break;
                await Task.Delay(20);
            }
            Assert.True(blocked, "A writer bypassing all application advisory locks must wait on the physical position tuple.");
            await tx1.CommitAsync();
            var failure = await Assert.ThrowsAsync<PostgresException>(async () => { await secondWrite; await tx2.CommitAsync(); });
            Assert.Contains(failure.SqlState, new[] { "23514", "40001" });
            // Failed COMMIT may leave a completed NpgsqlTransaction with a
            // non-null Connection; Dispose handles both that case and an abort
            // raised earlier by a RepeatableRead serialization failure.
            await tx2.DisposeAsync();
            ReservationBalances((await Get(client, a.Id))!, 60, 60, 0, 0); Assert.Empty((await Get(client, b.Id))!.Reservas);
            Assert.Equal("Planejada", (await Get(client, b.Id))!.Estado); Assert.Equal("100", (await Balance(client, f.Input))!.Quantidade);
        }
    }

    private static async Task DirectReservation(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid order, Scenario f)
    {
        await using var update = new NpgsqlCommand("UPDATE \"OrdensProducao\" SET \"Estado\"='Iniciada',\"Revisao\"=\"Revisao\"+1,\"IniciadaEmUtc\"=now(),\"UpdatedAtUtc\"=now() WHERE \"Id\"=@order", connection, transaction);
        update.Parameters.AddWithValue("order", order); await update.ExecuteNonQueryAsync();
        var allocationId = Guid.NewGuid();
        var snapshot = InventoryJson.Write(new
        {
            Alocacao = new AllocationCommand(allocationId, f.Input.Lot.Id, f.Input.Local.Id, "60", "kg"),
            Quantidade = InventoryRules.Normalize("60", "kg", "kg", null, f.Input.Item.Id),
            Lote = f.Input.Lot, Local = f.Input.Local, Item = f.Input.Item,
            Conversao = (object?)null, Perfil = (object?)null
        });
        await using var insert = new NpgsqlCommand("""
            INSERT INTO "ReservasProducao" ("Id","OrdemId","LinhaId","LoteId","LocalId","Unidade","QuantidadeOriginal","QuantidadeAtiva","QuantidadeConsumida","QuantidadeLiberada","SnapshotJson","Revisao","CreatedAtUtc")
            VALUES (@id,@order,@line,@lot,@local,'kg',60,60,0,0,@snapshot,1,now())
            """, connection, transaction);
        insert.Parameters.AddWithValue("id", allocationId); insert.Parameters.AddWithValue("order", order); insert.Parameters.AddWithValue("line", f.Version.Conteudo.Entradas[0].Id);
        insert.Parameters.AddWithValue("snapshot", NpgsqlTypes.NpgsqlDbType.Jsonb, snapshot);
        insert.Parameters.AddWithValue("lot", f.Input.Lot.Id); insert.Parameters.AddWithValue("local", f.Input.Local.Id); await insert.ExecuteNonQueryAsync();
    }
}
