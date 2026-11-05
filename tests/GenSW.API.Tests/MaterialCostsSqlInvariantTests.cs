using System.Data;
using GenSW.Application.Production;
using GenSW.Domain.MaterialCosts;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace GenSW.API.Tests;

public sealed partial class ProductionApiTests
{
    [Theory]
    [InlineData(IsolationLevel.ReadCommitted)]
    [InlineData(IsolationLevel.RepeatableRead)]
    public async Task C28_sql_cannot_attach_unregistered_loss_with_zero_percent_to_frozen_calculation(IsolationLevel isolation)
    {
        var (client, user) = await Session();
        using (client)
        {
            var scenario = await CostScenario(client, user.UserId, "10", "20", "10");
            var order = await CostConfirmed(client, user.UserId, scenario, "10", "10");
            var execution = order.Execucao!.Id;
            var before = (await CostExecution(client, execution))!;
            var originalDestination = Assert.Single(before.Destinos);

            await RejectMaterialCostInsertAtCommit(isolation, """
                INSERT INTO "DestinosCustoExecucoes"
                    ("Id", "ApuracaoId", "DestinoId", "Tipo", "Quantidade", "Unidade", "Percentual",
                     "ValorConhecido", "Cobertura", "ComponentesJson", "LacunasJson", "OrdemRateio",
                     "RestoRateio", "Motivo", "PerdaId")
                VALUES (@id, @calculation, @loss, 'Perda', 1, 'kg', 0, 0, 'Desconhecido',
                        '[]'::jsonb, '[]'::jsonb, 1, '[]'::jsonb, 'Perda sintética inexistente', @loss)
                """, 1, ("id", Guid.NewGuid()), ("calculation", before.Id), ("loss", Guid.NewGuid()));

            var after = (await CostExecution(client, execution))!;
            Assert.Equal(before.Id, after.Id);
            Assert.Equal(before.Custo.ValorTotal, after.Custo.ValorTotal);
            Assert.Equal(originalDestination.Id, Assert.Single(after.Destinos).Id);
        }
    }

    [Theory]
    [InlineData(IsolationLevel.ReadCommitted)]
    [InlineData(IsolationLevel.RepeatableRead)]
    public async Task C28_sql_cannot_attach_offsetting_unlinked_parcels_to_physical_receipt(IsolationLevel isolation)
    {
        var (client, user) = await Session();
        using (client)
        {
            var stock = await CostStock(client, user.UserId, "3", "0.01", "un");
            var before = (await CostLot(client, stock.Lot.Id))!;
            var layer = Assert.Single(before.Camadas);
            var beforeCount = await Factory.ExecuteDbContextAsync(db => db.Set<ParcelaCustoMaterial>()
                .CountAsync(part => part.CamadaId == layer.Id));
            Assert.Equal(1, beforeCount);

            // Insert the debit before the offsetting credit. Both share the real receipt,
            // but neither names a physical movement. The final net quantity and cents match.
            await RejectMaterialCostInsertAtCommit(isolation, """
                INSERT INTO "ParcelasCustoMateriais"
                    ("Id", "EventoCustoId", "CamadaId", "LocalId", "Sentido", "Quantidade",
                     "ValorConhecido", "ComponentesJson", "LacunasJson")
                SELECT @debit, "EventoCustoId", "CamadaId", "LocalId", 'Saida', "Quantidade",
                       "ValorConhecido", "ComponentesJson", "LacunasJson"
                  FROM "ParcelasCustoMateriais"
                 WHERE "CamadaId" = @layer AND "MovimentoEstoqueId" IS NOT NULL;
                INSERT INTO "ParcelasCustoMateriais"
                    ("Id", "EventoCustoId", "CamadaId", "LocalId", "Sentido", "Quantidade",
                     "ValorConhecido", "ComponentesJson", "LacunasJson")
                SELECT @credit, "EventoCustoId", "CamadaId", "LocalId", 'Entrada', "Quantidade",
                       "ValorConhecido", "ComponentesJson", "LacunasJson"
                  FROM "ParcelasCustoMateriais"
                 WHERE "CamadaId" = @layer AND "MovimentoEstoqueId" IS NOT NULL
                """, 2, ("debit", Guid.NewGuid()), ("credit", Guid.NewGuid()), ("layer", layer.Id));

            var afterCount = await Factory.ExecuteDbContextAsync(db => db.Set<ParcelaCustoMaterial>()
                .CountAsync(part => part.CamadaId == layer.Id));
            Assert.Equal(beforeCount, afterCount);
            Assert.Equal(before.Custo.ValorTotal, (await CostLot(client, stock.Lot.Id))!.Custo.ValorTotal);
        }
    }

    [Theory]
    [InlineData(IsolationLevel.ReadCommitted)]
    [InlineData(IsolationLevel.RepeatableRead)]
    public async Task C28_sql_cannot_resolve_pending_calculation_without_allocating_destinations(IsolationLevel isolation)
    {
        var (client, user) = await Session(true);
        using (client)
        {
            var scenario = await CostScenario(client, user.UserId, "10", "20", "10");
            var order = await Order(client, user.UserId, scenario);
            order = await Success<OrderView>(await Post(client, $"{order.Id}/inicio", Start(order, scenario, user.UserId)));
            var confirmation = Confirm(order, scenario, user.UserId, "10", "10", "0");
            order = await Success<OrderView>(await Post(client, $"{order.Id}/confirmacao", confirmation));
            var execution = order.Execucao!.Id;
            var pending = (await CostExecution(client, execution))!;
            Assert.Equal("PendenteRateio", pending.Estado);
            Assert.Equal("20.00", pending.Custo.ValorConhecido);
            Assert.Empty(pending.Destinos);

            var invalidResolution = Guid.NewGuid();
            await RejectMaterialCostInsertAtCommit(isolation, """
                INSERT INTO "ApuracoesCustoExecucoes"
                    ("Id", "ExecucaoId", "Estado", "ValorConhecido", "ValorTotal", "Cobertura",
                     "ComponentesJson", "LacunasJson", "CorteFisico", "CorteMonetario", "SnapshotJson",
                     "AutorId", "ResponsavelId", "CreatedAtUtc", "DataOperacional", "Motivo",
                     "Correlacao", "Algoritmo", "ApuracaoOriginalId")
                SELECT @id, "ExecucaoId", 'Apurada', "ValorConhecido", "ValorConhecido", 'Completo',
                       "ComponentesJson", '[]'::jsonb, "CorteFisico", "CorteMonetario",
                       "SnapshotJson", "AutorId", "ResponsavelId", "CreatedAtUtc", "DataOperacional",
                       'Resolução sintética sem destinos', @id::text, "Algoritmo", "Id"
                  FROM "ApuracoesCustoExecucoes"
                 WHERE "Id" = @original
                """, 1, ("id", invalidResolution), ("original", pending.Id));

            var after = (await CostExecution(client, execution))!;
            Assert.Equal(pending.Id, after.Id);
            Assert.Equal("PendenteRateio", after.Estado);
            Assert.Null(after.Custo.ValorTotal);
            Assert.Empty(after.Destinos);
            Assert.False(await Factory.ExecuteDbContextAsync(db => db.Set<ApuracaoCustoExecucao>()
                .AnyAsync(calculation => calculation.Id == invalidResolution)));
        }
    }

    private async Task RejectMaterialCostInsertAtCommit(
        IsolationLevel isolation,
        string sql,
        int expectedRows,
        params (string Name, Guid Value)[] parameters)
    {
        var connectionString = await Factory.ExecuteDbContextAsync(db =>
            Task.FromResult(db.Database.GetConnectionString()!));
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync(isolation);
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        foreach (var parameter in parameters)
            command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        Assert.Equal(expectedRows, await command.ExecuteNonQueryAsync());

        var error = await Assert.ThrowsAsync<PostgresException>(() => transaction.CommitAsync());
        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
    }
}
