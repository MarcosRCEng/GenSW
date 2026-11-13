using GenSW.Application.Optimization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GenSW.Infrastructure.Optimization;

internal sealed class OptimizationRecovery(IServiceScopeFactory scopes, ILogger<OptimizationRecovery> logger) : BackgroundService
{
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
                await scope.ServiceProvider.GetRequiredService<OptimizationService>().RecoverExpiredAsync(stoppingToken);
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
                    logger.LogWarning("Recuperação de otimizações adiada. Categoria: {FailureType}", exception.GetType().Name);
                    reportedFailure = true;
                }
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
