using GenSW.Infrastructure.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GenSW.API.Deployment;

internal sealed class CatalogReadinessCheck(IServiceScopeFactory scopes) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var catalog = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            var actual = await catalog.Database.GetAppliedMigrationsAsync(ct);
            var expected = catalog.Database.GetMigrations().ToArray();
            if (expected.Length == 0 || !expected.SequenceEqual(actual))
                return HealthCheckResult.Unhealthy();

            await catalog.Accounts.AnyAsync(ct);
            return HealthCheckResult.Healthy();
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy();
        }
    }
}
