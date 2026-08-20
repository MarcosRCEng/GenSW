using System.Net;
using GenSW.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GenSW.API.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PostgreSqlAuthCollection
{
    public const string Name = "PostgreSQL authentication integration";
}

[Collection(PostgreSqlAuthCollection.Name)]
public sealed class PostgreSqlRefreshConcurrencyTests
{
    [Fact]
    public async Task Sequential_refresh_uses_the_migrated_PostgreSQL_schema()
    {
        await using var postgreSql = await EphemeralPostgreSql.StartAsync();
        var factory = new AuthWebApplicationFactory(postgreSql.ConnectionString);
        await factory.InitializeAsync();

        try
        {
            var appliedMigrations = await factory.ExecuteDbContextAsync(context =>
                context.Database.GetAppliedMigrationsAsync());
            Assert.Contains("20260820002015_AddIdentityFoundation", appliedMigrations);

            var user = await factory.SeedUserAsync($"postgres_sequential_{Guid.NewGuid():N}");
            using var client = factory.CreateHttpsClient();
            using var login = await client.LoginAsync(user.UserName, user.Password);
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);

            using var refresh = await client.PostWithCookieAsync(
                "/api/v1/auth/refresh",
                login.GetIssuedCookie());

            Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
            Assert.Equal(2, (await LoadSessionsAsync(factory, user.UserId)).Count);
        }
        finally
        {
            await ((IAsyncLifetime)factory).DisposeAsync();
        }
    }

    [Fact]
    public async Task Concurrent_refresh_is_serialized_by_PostgreSQL_family_row_lock()
    {
        await using var postgreSql = await EphemeralPostgreSql.StartAsync();
        var factory = new AuthWebApplicationFactory(postgreSql.ConnectionString);
        await factory.InitializeAsync();

        try
        {
            var providerName = await factory.ExecuteDbContextAsync(context =>
                Task.FromResult(context.Database.ProviderName));
            Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", providerName);

            var user = await factory.SeedUserAsync($"postgres_concurrency_{Guid.NewGuid():N}");
            using var loginClient = factory.CreateHttpsClient();
            using var login = await loginClient.LoginAsync(user.UserName, user.Password);
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            var originalCookie = login.GetIssuedCookie();
            var original = Assert.Single(await LoadSessionsAsync(factory, user.UserId));
            using var firstClient = factory.CreateHttpsClient();
            using var secondClient = factory.CreateHttpsClient();
            using var requestTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            var responses = await Task.WhenAll(
                firstClient.PostWithCookieAsync(
                    "/api/v1/auth/refresh",
                    originalCookie,
                    requestTimeout.Token),
                secondClient.PostWithCookieAsync(
                    "/api/v1/auth/refresh",
                    originalCookie,
                    requestTimeout.Token));

            try
            {
                Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
                Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.Unauthorized));

                var family = (await LoadSessionsAsync(factory, user.UserId))
                    .Where(session => session.FamilyId == original.FamilyId)
                    .ToArray();
                var successors = family.Where(session => session.Id != original.Id).ToArray();
                Assert.Single(successors);
                Assert.DoesNotContain(family, session => session.RevokedAtUtc is null);
            }
            finally
            {
                foreach (var response in responses)
                {
                    response.Dispose();
                }
            }
        }
        finally
        {
            await ((IAsyncLifetime)factory).DisposeAsync();
        }
    }

    private static async Task<IReadOnlyList<RefreshSession>> LoadSessionsAsync(
        AuthWebApplicationFactory factory,
        Guid userId)
    {
        return await factory.ExecuteDbContextAsync(async context =>
            (IReadOnlyList<RefreshSession>)await context.RefreshSessions
                .AsNoTracking()
                .Where(session => session.UserId == userId)
                .ToListAsync());
    }
}
