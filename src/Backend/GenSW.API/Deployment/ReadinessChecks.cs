using GenSW.Infrastructure.Images;
using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GenSW.API.Deployment;

internal sealed class DatabaseReadinessCheck(IServiceScopeFactory scopes) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<GenSWDbContext>();
            var pending = await database.Database.GetPendingMigrationsAsync(cancellationToken);
            if (pending.Any())
            {
                return HealthCheckResult.Unhealthy("The database requires migrations.");
            }

            await database.Users.AnyAsync(cancellationToken);
            await database.Especies.AnyAsync(cancellationToken);
            await database.ImagensAnimal.AnyAsync(cancellationToken);
            await database.ImagensVariedade.AnyAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy("The database or catalog is unavailable.");
        }
    }
}

internal sealed class ImageStorageReadinessCheck(PrivateImageStorage storage) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            storage.VerifyWritable();
            return Task.FromResult(HealthCheckResult.Healthy());
        }
        catch (Exception)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Private image storage is unavailable."));
        }
    }
}
