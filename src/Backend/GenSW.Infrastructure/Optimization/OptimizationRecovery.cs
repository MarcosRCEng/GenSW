using System.Collections.Concurrent;
using GenSW.Application.Accounts;
using GenSW.Application.Optimization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GenSW.Infrastructure.Optimization;

internal sealed class OptimizationRecovery(IServiceScopeFactory scopes, TimeProvider clock,
    ILogger<OptimizationRecovery> logger) : BackgroundService
{
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> retryAt = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        var reportedFailure = false;

        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var accounts = await scope.ServiceProvider.GetRequiredService<IErpAccountScope>().ListActiveAsync(stoppingToken);
                var activeIds = accounts.Select(x => x.AccountId).ToHashSet();
                foreach (var id in retryAt.Keys)
                {
                    if (!activeIds.Contains(id)) retryAt.TryRemove(id, out _);
                }
                await Parallel.ForEachAsync(accounts, new ParallelOptions
                {
                    CancellationToken = stoppingToken,
                    MaxDegreeOfParallelism = 8
                }, RecoverAccountAsync);
                reportedFailure = false;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                if (!reportedFailure)
                {
                    logger.LogWarning("Recuperação de otimizações adiada pelo catálogo. Categoria {FailureType}", exception.GetType().Name);
                    reportedFailure = true;
                }
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async ValueTask RecoverAccountAsync(ErpAccountKey account, CancellationToken stoppingToken)
    {
        if (retryAt.TryGetValue(account.AccountId, out var next) && next > clock.GetUtcNow()) return;

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8), clock);
        using var recovery = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, timeout.Token);
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var binding = scope.ServiceProvider.GetRequiredService<IErpAccountScope>();
            await binding.BindAsync(account.AccountId, account.BindingId, account.BindingRevision, account.AccessGeneration,
                account.ActorId, recovery.Token);
            await scope.ServiceProvider.GetRequiredService<OptimizationService>().RecoverExpiredAsync(recovery.Token);
            retryAt.TryRemove(account.AccountId, out _);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            retryAt[account.AccountId] = clock.GetUtcNow().AddSeconds(30);
            logger.LogWarning("Recuperação de otimizações adiada para a conta {AccountId}, revisão {BindingRevision}. Categoria {FailureType}",
                account.AccountId, account.BindingRevision, exception.GetType().Name);
        }
    }
}
