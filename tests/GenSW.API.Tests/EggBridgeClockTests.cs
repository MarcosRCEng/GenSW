using System.Net;
using GenSW.Application.Authentication;
using GenSW.Application.EggBridge;
using GenSW.Application.Inventory;
using GenSW.Domain.Inventory;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace GenSW.API.Tests;

public sealed partial class EggBridgeApiTests
{
    [Fact]
    public async Task F03_11_midnight_while_waiting_invalidates_still_unexpired_preview_without_stock_effect()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        var nextMidnight = TimeZoneInfo.ConvertTimeToUtc(InventoryRules.Today(TimeProvider.System).AddDays(1).ToDateTime(TimeOnly.MinValue), zone);
        var clock = new EggClock(new DateTimeOffset(nextMidnight).AddSeconds(-30));
        // Only the F03 service clock changes: JWT, the host and other modules
        // keep their actual clock. The preview expires 30 minutes later.
        using var configured = Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<EggBridgeService>();
            services.AddScoped(provider => new EggBridgeService(provider.GetRequiredService<IEggBridgeRepository>(),
                provider.GetRequiredService<IInventoryRepository>(), provider.GetRequiredService<IAuthenticationSessionService>(),
                provider.GetRequiredService<IEggBridgePreviewProtector>(), clock));
        }));
        var (session, user) = await Session(true); using (session)
        using (var client = configured.CreateClient(new() { BaseAddress = new Uri("https://localhost") }))
        {
            client.DefaultRequestHeaders.Authorization = session.DefaultRequestHeaders.Authorization;
            var m = await Stock(client, user.UserId); var eggs = await Eggs(1); var scope = await Scope(client, m, user.UserId, eggs);
            var preview = await Preview(client, Entry(m, scope.Id, user.UserId, eggs)); var before = await Snapshot();
            var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var holder = Factory.ExecuteDbContextAsync(async db =>
            {
                await using var tx = await db.Database.BeginTransactionAsync();
                await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(413,421)"); held.SetResult();
                await release.Task.WaitAsync(TimeSpan.FromSeconds(15)); await tx.CommitAsync();
            });
            Task<HttpResponseMessage>? confirm = null;
            try
            {
                await held.Task.WaitAsync(TimeSpan.FromSeconds(5)); confirm = Post(client, "confirmacoes", new EggConfirmCommand(preview.Token));
                Assert.True(await WaitForLock("wait_event='advisory' AND query LIKE '%413%421%'"));
                clock.Advance(TimeSpan.FromMinutes(1)); Assert.True(clock.GetUtcNow() < preview.ExpiraEmUtc);
            }
            finally { release.TrySetResult(); await holder; }
            await Error(await confirm!, HttpStatusCode.Conflict); Assert.Equal(before, await Snapshot());
        }
    }

    private sealed class EggClock(DateTimeOffset initial) : TimeProvider
    {
        private long ticks = initial.UtcTicks;
        public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref ticks), TimeSpan.Zero);
        public void Advance(TimeSpan duration) => Interlocked.Add(ref ticks, duration.Ticks);
    }
}
