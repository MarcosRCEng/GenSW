using System.Net;
using GenSW.Application.MaterialCosts;
using GenSW.Application.Production;
using GenSW.Domain.MaterialCosts;
using GenSW.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GenSW.API.Tests;

public sealed partial class ProductionApiTests
{
    [Fact]
    public async Task C31_admin_revoked_after_present_resolution_preview_cannot_confirm_or_persist_cost()
    {
        var (client, user) = await Session(true);
        using (client)
        {
            var scenario = await CostScenario(client, user.UserId, "10", "20", "10");
            var order = await Order(client, user.UserId, scenario);
            order = await Success<OrderView>(await Post(client, $"{order.Id}/inicio", Start(order, scenario, user.UserId)));
            var confirmation = Confirm(order, scenario, user.UserId, "10", "10", "0");
            order = await Success<OrderView>(await Post(client, $"{order.Id}/confirmacao", confirmation));

            var execution = order.Execucao!;
            var output = Assert.Single(execution.LotesSaida);
            var pending = (await CostExecution(client, execution.Id))!;
            Assert.Equal("PendenteRateio", pending.Estado);
            Assert.Equal("20.00", pending.Custo.ValorConhecido);
            Assert.Null((await CostLot(client, output.LoteId))!.Custo.ValorTotal);

            var command = new MaterialResolutionCommand(Guid.NewGuid(), 1,
                new MaterialAllocationCommand(1, false, Evidence, [new(output.Id, "Saida", "100")]),
                Evidence, Evidence);
            var preview = await Success<MaterialProductionPreview>(
                await CostPost(client, $"execucoes/{execution.Id}/previas-resolucao", command));
            Assert.Equal("20.00", preview.Custo.ValorTotal);
            command = command with
            {
                CustoMaterial = command.CustoMaterial with { SequenciaEsperada = preview.SequenciaEsperada }
            };
            var physicalBefore = await Snapshot();
            var costBefore = await Factory.ExecuteDbContextAsync(async db => new
            {
                Calculations = await db.Set<ApuracaoCustoExecucao>().CountAsync(),
                Destinations = await db.Set<DestinoCustoExecucao>().CountAsync(),
                Layers = await db.Set<CamadaCustoMaterial>().CountAsync(),
                Events = await db.Set<EventoCustoMaterial>().CountAsync(),
                Commands = await db.Set<ComandoCustoMaterial>().CountAsync()
            });

            await Factory.ExecuteScopeAsync(async services =>
            {
                var users = services.GetRequiredService<UserManager<ApplicationUser>>();
                var current = await users.FindByIdAsync(user.UserId.ToString("D"));
                Assert.NotNull(current);
                Assert.True((await users.RemoveFromRoleAsync(current, "Admin")).Succeeded);
                return 0;
            });

            // Reuse the authenticated client and its original bearer token after the role change.
            await Error(await CostPost(client, $"execucoes/{execution.Id}/resolucoes", command, Unique()),
                HttpStatusCode.Forbidden);
            var after = (await CostExecution(client, execution.Id))!;
            Assert.Equal(pending.Id, after.Id);
            Assert.Equal("PendenteRateio", after.Estado);
            Assert.Null(after.Custo.ValorTotal);
            Assert.Empty(after.Destinos);
            Assert.Null((await CostLot(client, output.LoteId))!.Custo.ValorTotal);
            Assert.Equal(physicalBefore, await Snapshot());
            var costAfter = await Factory.ExecuteDbContextAsync(async db => new
            {
                Calculations = await db.Set<ApuracaoCustoExecucao>().CountAsync(),
                Destinations = await db.Set<DestinoCustoExecucao>().CountAsync(),
                Layers = await db.Set<CamadaCustoMaterial>().CountAsync(),
                Events = await db.Set<EventoCustoMaterial>().CountAsync(),
                Commands = await db.Set<ComandoCustoMaterial>().CountAsync()
            });
            Assert.Equal(costBefore, costAfter);
        }
    }
}
