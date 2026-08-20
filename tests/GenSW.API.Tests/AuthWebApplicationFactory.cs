using GenSW.Domain.People;
using GenSW.Infrastructure.Identity;
using GenSW.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace GenSW.API.Tests;

public sealed class AuthWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string JwtIssuer = "GenSW.Tests";
    public const string JwtAudience = "GenSW.Tests.Client";
    public const string JwtSigningKey = "GENSW_TEST_ONLY_SIGNING_KEY_0123456789_ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    public const string AllowedOrigin = "https://localhost:5173";
    public const string ValidPassword = "GENSW_TEST_ONLY_password_12345";

    private readonly string databasePath = Path.Combine(
        Path.GetTempPath(),
        $"gensw-auth-tests-{Guid.NewGuid():N}.db");
    private readonly int loginPermitLimit;
    private readonly int loginWindowSeconds;
    private readonly string? postgreSqlConnectionString;

    public AuthWebApplicationFactory()
        : this(1_000, 60, postgreSqlConnectionString: null)
    {
    }

    internal AuthWebApplicationFactory(int loginPermitLimit, int loginWindowSeconds)
        : this(loginPermitLimit, loginWindowSeconds, postgreSqlConnectionString: null)
    {
    }

    internal AuthWebApplicationFactory(string postgreSqlConnectionString)
        : this(1_000, 60, postgreSqlConnectionString)
    {
    }

    private AuthWebApplicationFactory(
        int loginPermitLimit,
        int loginWindowSeconds,
        string? postgreSqlConnectionString)
    {
        this.loginPermitLimit = loginPermitLimit;
        this.loginWindowSeconds = loginWindowSeconds;
        this.postgreSqlConnectionString = postgreSqlConnectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting(
            "ConnectionStrings:GenSW",
            postgreSqlConnectionString ??
                "Host=127.0.0.1;Database=gensw_test_configuration_only");
        builder.UseSetting("Authentication:Jwt:Issuer", JwtIssuer);
        builder.UseSetting("Authentication:Jwt:Audience", JwtAudience);
        builder.UseSetting("Authentication:Jwt:AccessTokenMinutes", "10");
        builder.UseSetting("Authentication:Jwt:SigningKey", JwtSigningKey);
        builder.UseSetting("Cors:AllowedOrigins:0", AllowedOrigin);
        builder.UseSetting(
            "RateLimiting:Login:PermitLimit",
            loginPermitLimit.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.UseSetting(
            "RateLimiting:Login:WindowSeconds",
            loginWindowSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:GenSW"] = postgreSqlConnectionString ??
                    "Host=127.0.0.1;Database=gensw_test_configuration_only",
                ["Authentication:Jwt:Issuer"] = JwtIssuer,
                ["Authentication:Jwt:Audience"] = JwtAudience,
                ["Authentication:Jwt:AccessTokenMinutes"] = "10",
                ["Authentication:Jwt:SigningKey"] = JwtSigningKey,
                ["Cors:AllowedOrigins:0"] = AllowedOrigin,
                ["RateLimiting:Login:PermitLimit"] = loginPermitLimit.ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
                ["RateLimiting:Login:WindowSeconds"] = loginWindowSeconds.ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
            });
        });

        builder.ConfigureTestServices(services =>
        {
            if (postgreSqlConnectionString is not null)
            {
                return;
            }

            services.RemoveAll<GenSWDbContext>();
            services.RemoveAll<DbContextOptions<GenSWDbContext>>();

            var staleOptionsConfigurations = services
                .Where(descriptor => IsOptionsConfigurationForGenSwDbContext(descriptor.ServiceType))
                .ToArray();

            foreach (var descriptor in staleOptionsConfigurations)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<GenSWDbContext>(options => options.UseSqlite(
                $"Data Source={databasePath};Foreign Keys=True;Default Timeout=30;Pooling=False"));
        });
    }

    public HttpClient CreateHttpsClient(bool handleCookies = false)
    {
        return CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = handleCookies,
        });
    }

    public async Task InitializeAsync()
    {
        using var client = CreateHttpsClient();
        await ExecuteDbContextAsync(async context =>
        {
            if (postgreSqlConnectionString is null)
            {
                await context.Database.EnsureDeletedAsync();
                await context.Database.EnsureCreatedAsync();
            }
            else
            {
                await context.Database.MigrateAsync();
            }
        });
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        Dispose();

        for (var attempt = 0;
             postgreSqlConnectionString is null && attempt < 5 && File.Exists(databasePath);
             attempt++)
        {
            try
            {
                File.Delete(databasePath);
            }
            catch (IOException) when (attempt < 4)
            {
                await Task.Delay(50);
            }
        }
    }

    public async Task<SeededUser> SeedUserAsync(
        string userName,
        bool isActive = true,
        IReadOnlyCollection<string>? roles = null,
        string password = ValidPassword)
    {
        await using var scope = Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<GenSWDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var now = DateTimeOffset.UtcNow;
        var pessoa = new Pessoa
        {
            Id = Guid.NewGuid(),
            Nome = $"Pessoa {userName}",
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        context.Pessoas.Add(pessoa);
        await context.SaveChangesAsync();

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            PessoaId = pessoa.Id,
            UserName = userName,
            IsActive = isActive,
            LockoutEnabled = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        EnsureSucceeded(await userManager.CreateAsync(user, password));

        foreach (var roleName in roles ?? [])
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                EnsureSucceeded(await roleManager.CreateAsync(new IdentityRole<Guid>
                {
                    Id = Guid.NewGuid(),
                    Name = roleName,
                }));
            }

            EnsureSucceeded(await userManager.AddToRoleAsync(user, roleName));
        }

        return new SeededUser(user.Id, pessoa.Id, pessoa.Nome, userName, password);
    }

    public async Task SetUserActiveAsync(Guid userId, bool isActive)
    {
        await using var scope = Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByIdAsync(userId.ToString("D"))
            ?? throw new InvalidOperationException($"Test user {userId:D} was not found.");

        user.IsActive = isActive;
        user.UpdatedAtUtc = DateTimeOffset.UtcNow;
        EnsureSucceeded(await userManager.UpdateAsync(user));
    }

    public async Task<T> ExecuteDbContextAsync<T>(Func<GenSWDbContext, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<GenSWDbContext>();
        return await action(context);
    }

    public async Task ExecuteDbContextAsync(Func<GenSWDbContext, Task> action)
    {
        await using var scope = Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<GenSWDbContext>();
        await action(context);
    }

    public async Task<T> ExecuteScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    private static bool IsOptionsConfigurationForGenSwDbContext(Type serviceType)
    {
        return serviceType.IsGenericType &&
            serviceType.Name.StartsWith("IDbContextOptionsConfiguration", StringComparison.Ordinal) &&
            serviceType.GenericTypeArguments.Contains(typeof(GenSWDbContext));
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(
                "; ",
                result.Errors.Select(error => $"{error.Code}: {error.Description}")));
        }
    }
}

public sealed record SeededUser(
    Guid UserId,
    Guid PessoaId,
    string Nome,
    string UserName,
    string Password);
