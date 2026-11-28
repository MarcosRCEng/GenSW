using GenSW.Application.Accounts;
using GenSW.Domain.People;
using GenSW.Infrastructure.Accounts;
using GenSW.Infrastructure.Catalog;
using GenSW.Infrastructure.Identity;
using GenSW.Infrastructure.Optimization;
using GenSW.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Xunit;

namespace GenSW.API.Tests;

public sealed class AuthWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string JwtIssuer = "GenSW.Tests";
    public const string JwtAudience = "GenSW.Tests.Client";
    public const string ValidPassword = "GENSW_TEST_ONLY_password_12345";
    public const string JwtSigningKey = "GENSW_TEST_ONLY_SIGNING_KEY_0123456789_ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    public const string AllowedOrigin = "https://localhost:5173";
    private const string TestErpDatabaseName = "gensw_auth_tests";
    private const string TestCatalogDatabaseName = "gensw_catalog_tests";

    private readonly string testRoot = Path.Combine(Path.GetTempPath(), $"gensw-auth-tests-{Guid.NewGuid():N}");
    private readonly int loginPermitLimit;
    private readonly int loginWindowSeconds;
    private readonly string? postgreSqlConnectionString;
    private readonly string? postgresCatalogConnectionString;
    private readonly string? erpMigration;
    private readonly DbContextOptions<GenSWDbContext> erpDatabaseOptions;
    private CatalogErpBinding? serviceTestBinding;

    public AuthWebApplicationFactory() : this(1_000, 60, null) { }

    internal AuthWebApplicationFactory(int loginPermitLimit, int loginWindowSeconds)
        : this(loginPermitLimit, loginWindowSeconds, null) { }

    internal AuthWebApplicationFactory(string postgreSqlConnectionString, string? erpMigration = null)
        : this(1_000, 60, postgreSqlConnectionString, erpMigration) { }

    private AuthWebApplicationFactory(
        int loginPermitLimit, int loginWindowSeconds, string? postgreSqlConnectionString, string? erpMigration = null)
    {
        this.loginPermitLimit = loginPermitLimit;
        this.loginWindowSeconds = loginWindowSeconds;
        this.postgreSqlConnectionString = postgreSqlConnectionString;
        this.erpMigration = erpMigration;

        var erp = new DbContextOptionsBuilder<GenSWDbContext>();
        if (postgreSqlConnectionString is null)
        {
            erp.UseSqlite(SqliteConnectionString("erp.db"));
        }
        else
        {
            var target = new NpgsqlConnectionStringBuilder(postgreSqlConnectionString);
            if (target.Host != "127.0.0.1" || target.Port == 5432 || target.Database != TestErpDatabaseName ||
                target.Username != "gensw_test" || target.Pooling)
                throw new InvalidOperationException("The fixture requires an ephemeral PostgreSQL test instance.");

            erp.UseNpgsql(target.ConnectionString);
            target.Database = TestCatalogDatabaseName;
            postgresCatalogConnectionString = target.ConnectionString;
        }
        erpDatabaseOptions = erp.Options;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:GenSWCatalog"] = postgresCatalogConnectionString ??
                "Host=127.0.0.1;Port=1;Database=gensw_test_configuration_only",
            ["Authentication:Jwt:Issuer"] = JwtIssuer,
            ["Authentication:Jwt:Audience"] = JwtAudience,
            ["Authentication:Jwt:AccessTokenMinutes"] = "10",
            ["Authentication:Jwt:SigningKey"] = JwtSigningKey,
            ["Cors:AllowedOrigins:0"] = AllowedOrigin,
            ["RateLimiting:Login:PermitLimit"] = loginPermitLimit.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["RateLimiting:Login:WindowSeconds"] = loginWindowSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
        foreach (var pair in settings)
            builder.UseSetting(pair.Key, pair.Value);

        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(settings));
        builder.ConfigureTestServices(services =>
        {
            if (postgresCatalogConnectionString is null)
            {
                services.RemoveAll<CatalogDbContext>();
                services.RemoveAll<DbContextOptions<CatalogDbContext>>();
                foreach (var descriptor in services.Where(descriptor =>
                    descriptor.ServiceType.IsGenericType &&
                    descriptor.ServiceType.Name.StartsWith("IDbContextOptionsConfiguration", StringComparison.Ordinal) &&
                    descriptor.ServiceType.GenericTypeArguments.Contains(typeof(CatalogDbContext))).ToArray())
                    services.Remove(descriptor);
                services.AddDbContext<CatalogDbContext>(options => options.UseSqlite(SqliteConnectionString("catalog.db")));
            }

            // Business regression tests share fixture data. The account-isolation suite instead
            // uses the production verifier and separate databases, grants and private roots.
            services.RemoveAll<IAccountDestinationVerifier>();
            services.AddScoped<IAccountDestinationVerifier>(_ => new TestDestination(this));
            services.RemoveAll<GenSW.Domain.Optimization.IOptimizationSolver>();
            services.AddSingleton<GenSW.Domain.Optimization.IOptimizationSolver>(
                new HighsProcessSolver(TimeSpan.FromSeconds(10)));

        });
    }

    public HttpClient CreateHttpsClient(bool handleCookies = false) => CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        BaseAddress = new Uri("https://localhost"),
        HandleCookies = handleCookies,
    });

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(testRoot);
        if (postgresCatalogConnectionString is not null)
        {
            await using var connection = new NpgsqlConnection(postgreSqlConnectionString);
            await connection.OpenAsync();
            if (connection.Database != TestErpDatabaseName)
                throw new InvalidOperationException("Unexpected PostgreSQL fixture database.");
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT count(*) FROM pg_database WHERE datname = 'gensw_catalog_tests'";
            if ((long)(await command.ExecuteScalarAsync())! == 0)
            {
                command.CommandText = "CREATE DATABASE gensw_catalog_tests";
                await command.ExecuteNonQueryAsync();
            }
        }

        // Initialize the synthetic destinations before starting the hosted recovery service.
        var options = new DbContextOptionsBuilder<CatalogDbContext>();
        if (postgresCatalogConnectionString is not null)
            options.UseNpgsql(postgresCatalogConnectionString);
        else
            options.UseSqlite(SqliteConnectionString("catalog.db"));
        await using (var catalog = new CatalogDbContext(options.Options))
        {
            if (postgresCatalogConnectionString is not null)
                await catalog.Database.MigrateAsync();
            else
                await catalog.Database.EnsureCreatedAsync();
        }

        await ExecuteDbContextAsync(async db =>
        {
            if (postgreSqlConnectionString is not null)
                await db.GetService<IMigrator>().MigrateAsync(erpMigration);
            else
                await db.Database.EnsureCreatedAsync();
        });
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        for (var attempt = 0; attempt < 5 && Directory.Exists(testRoot); attempt++)
        {
            try { Directory.Delete(testRoot, recursive: true); }
            catch (IOException) when (attempt < 4) { await Task.Delay(50); }
        }
    }

    public async Task<SeededUser> SeedUserAsync(
        string userName, bool isActive = true, IReadOnlyCollection<string>? roles = null, string password = ValidPassword)
    {
        await using var scope = Services.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<CatalogUser>>();
        var now = DateTimeOffset.UtcNow;
        var pessoa = Pessoa.Criar(TipoPessoa.Fisica, $"Pessoa {userName}", null, now);
        var actor = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            PessoaId = pessoa.Id,
            UserName = userName,
            IsActive = isActive,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        await ExecuteDbContextAsync(async db =>
        {
            db.Pessoas.Add(pessoa);
            db.Users.Add(actor);
            foreach (var roleName in roles ?? [])
            {
                var normalized = roleName.ToUpperInvariant();
                var role = await db.Roles.SingleOrDefaultAsync(row => row.NormalizedName == normalized);
                if (role is null)
                {
                    role = new IdentityRole<Guid> { Id = Guid.NewGuid(), Name = roleName, NormalizedName = normalized };
                    db.Roles.Add(role);
                }
                db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = actor.Id, RoleId = role.Id });
            }
            await db.SaveChangesAsync();
        });

        var user = new CatalogUser
        {
            Id = Guid.NewGuid(), UserName = userName, DisplayName = pessoa.Nome, IsActive = isActive, LockoutEnabled = true,
        };
        EnsureSucceeded(await users.CreateAsync(user, password));

        var account = new CatalogAccount
        {
            Id = Guid.NewGuid(), SubjectId = user.Id, Confirmed = true, State = CatalogAccountState.Active,
        };
        await using var erpModel = new GenSWDbContext(erpDatabaseOptions);
        var binding = new CatalogBinding
        {
            Id = Guid.NewGuid(), AccountId = account.Id, Revision = 1, ErpActorId = actor.Id,
            DatabaseId = Guid.NewGuid(), VolumeId = Guid.NewGuid(), DatabaseName = TestErpDatabaseName,
            DatabaseSecretRef = "test-database", VolumeRef = Guid.NewGuid().ToString("D"), Published = true, Ready = true,
            ExpectedMigration = erpMigration ?? erpModel.Database.GetMigrations().Last(),
        };
        catalog.AddRange(account, binding);
        await catalog.SaveChangesAsync();

        var volume = TestVolume(binding);
        Directory.CreateDirectory(volume);
        await File.WriteAllTextAsync(Path.Combine(volume, PrivateVolume.MarkerName), binding.VolumeId.ToString("D"));

        if (postgreSqlConnectionString is not null && erpMigration is null)
        {
            await ExecuteDbContextAsync(db => db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO gensw_scope.destination (database_id, account_id, binding_id, binding_revision, generation, enabled)
                VALUES ({binding.DatabaseId}, {account.Id}, {binding.Id}, {binding.Revision}, {account.AccessGeneration}, true)
                ON CONFLICT DO NOTHING
                """));
        }

        if (isActive)
            serviceTestBinding = new CatalogErpBinding(account, binding);
        return new SeededUser(actor.Id, pessoa.Id, pessoa.Nome, userName, password, user.Id);
    }

    public async Task RemoveLocalRoleAsync(Guid actorId, string roleName)
    {
        await ExecuteDbContextAsync(async db =>
        {
            var role = await db.Roles.Where(row => row.Name == roleName).Select(row => row.Id).SingleAsync();
            var removed = await db.UserRoles.Where(row => row.RoleId == role && row.UserId == actorId).ExecuteDeleteAsync();
            Assert.Equal(1, removed);
        });
    }

    public async Task SetUserActiveAsync(Guid actorId, bool isActive)
    {
        await ExecuteDbContextAsync(async db =>
        {
            var actor = await db.Users.SingleAsync(row => row.Id == actorId);
            actor.IsActive = isActive;
            actor.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        });
        await ExecuteCatalogAsync(async catalog =>
        {
            var user = await (from binding in catalog.Bindings
                              join account in catalog.Accounts on binding.AccountId equals account.Id
                              join candidate in catalog.Users on account.SubjectId equals candidate.Id
                              where binding.ErpActorId == actorId
                              select candidate).SingleAsync();
            user.IsActive = isActive;
            await catalog.SaveChangesAsync();
            return true;
        });
    }

    public async Task<T> ExecuteDbContextAsync<T>(Func<GenSWDbContext, Task<T>> action)
    {
        await using var db = new GenSWDbContext(erpDatabaseOptions);
        return await action(db);
    }

    public async Task ExecuteDbContextAsync(Func<GenSWDbContext, Task> action)
    {
        await using var db = new GenSWDbContext(erpDatabaseOptions);
        await action(db);
    }

    public async Task<T> ExecuteCatalogAsync<T>(Func<CatalogDbContext, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<CatalogDbContext>());
    }

    public async Task<T> ExecuteScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();
        if (serviceTestBinding is { } resolved)
            await scope.ServiceProvider.GetRequiredService<AccountScope>()
                .EstablishAsync(resolved.Account, resolved.Binding, default);
        return await action(scope.ServiceProvider);
    }

    private string TestVolume(CatalogBinding binding) => Path.Combine(testRoot, binding.VolumeRef);

    private string SqliteConnectionString(string file) =>
        $"Data Source={Path.Combine(testRoot, file)};Foreign Keys=True;Default Timeout=30;Pooling=False";

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join(", ", result.Errors.Select(error => error.Code)));
    }

    private sealed class TestDestination(AuthWebApplicationFactory factory) : IAccountDestinationVerifier
    {
        public async Task<VerifiedAccountDestination> VerifyAsync(CatalogAccount account, CatalogBinding binding, CancellationToken ct)
        {
            var options = factory.erpDatabaseOptions;
            if (factory.postgreSqlConnectionString is not null)
            {
                var connection = new NpgsqlConnectionStringBuilder(factory.postgreSqlConnectionString)
                {
                    Options = $"-c gensw.binding={binding.Id:D} -c gensw.revision={binding.Revision} -c gensw.generation={account.AccessGeneration}",
                };
                options = new DbContextOptionsBuilder<GenSWDbContext>().UseNpgsql(connection.ConnectionString).Options;
            }

            await using var db = new GenSWDbContext(options);
            var actor = await db.Users.AsNoTracking().Where(row => row.Id == binding.ErpActorId && row.IsActive)
                .Select(row => new { row.PessoaId, row.Pessoa.Nome }).SingleOrDefaultAsync(ct)
                ?? throw new AccountUnavailableException();
            var roles = await (from membership in db.UserRoles.AsNoTracking()
                               join role in db.Roles.AsNoTracking() on membership.RoleId equals role.Id
                               where membership.UserId == binding.ErpActorId && role.Name != null
                               orderby role.Name
                               select role.Name!).ToArrayAsync(ct);
            return new VerifiedAccountDestination(options, factory.TestVolume(binding), actor.PessoaId, actor.Nome, roles);
        }
    }
}

public sealed record SeededUser(Guid UserId, Guid PessoaId, string Nome, string UserName, string Password, Guid SubjectId);
