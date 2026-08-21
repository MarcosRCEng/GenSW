using GenSW.Infrastructure.Identity;
using GenSW.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GenSW.API.Tests;

public sealed class InitialAdminBootstrapperTests
{
    [Fact]
    public async Task Bootstrap_on_an_empty_database_creates_a_person_user_admin_role_and_valid_identity_password()
    {
        await using var testHost = await InitialAdminBootstrapTestHost.CreateAsync();
        var password = CreateTestPassword();

        await testHost.BootstrapAsync("Bootstrap Test", "bootstrap_admin", password);

        await testHost.VerifyAsync(async services =>
        {
            var context = services.GetRequiredService<GenSWDbContext>();
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            var pessoa = await context.Pessoas.SingleAsync();
            var user = await context.Users.SingleAsync();

            Assert.Equal("Bootstrap Test", pessoa.Nome);
            Assert.Equal(pessoa.Id, user.PessoaId);
            Assert.True(await roleManager.RoleExistsAsync(InitialAdminBootstrapper.AdminRoleName));
            Assert.True(await userManager.IsInRoleAsync(user, InitialAdminBootstrapper.AdminRoleName));
            Assert.True(await userManager.CheckPasswordAsync(user, password));
        });
    }

    [Fact]
    public async Task Bootstrap_rejects_a_second_attempt_when_any_user_exists()
    {
        await using var testHost = await InitialAdminBootstrapTestHost.CreateAsync();
        var password = CreateTestPassword();
        await testHost.BootstrapAsync("First Admin", "first_admin", password);

        await Assert.ThrowsAsync<InitialAdminBootstrapRejectedException>(
            () => testHost.BootstrapAsync("Second Admin", "second_admin", password));

        await testHost.VerifyAsync(async services =>
        {
            var context = services.GetRequiredService<GenSWDbContext>();
            Assert.Equal(1, await context.Pessoas.CountAsync());
            Assert.Equal(1, await context.Users.CountAsync());
        });
    }

    [Fact]
    public async Task Bootstrap_reuses_an_existing_admin_role_when_there_are_no_users()
    {
        await using var testHost = await InitialAdminBootstrapTestHost.CreateAsync();
        var existingRoleId = await testHost.CreateRoleAsync(InitialAdminBootstrapper.AdminRoleName);
        var password = CreateTestPassword();

        await testHost.BootstrapAsync("Reused Role Admin", "reused_role_admin", password);

        await testHost.VerifyAsync(async services =>
        {
            var context = services.GetRequiredService<GenSWDbContext>();
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var role = await context.Roles.SingleAsync();
            var user = await context.Users.SingleAsync();

            Assert.Equal(existingRoleId, role.Id);
            Assert.True(await userManager.IsInRoleAsync(user, InitialAdminBootstrapper.AdminRoleName));
        });
    }

    [Fact]
    public async Task Bootstrap_rolls_back_the_person_when_identity_user_creation_fails()
    {
        await using var testHost = await InitialAdminBootstrapTestHost.CreateAsync();
        var password = CreateTestPassword();

        await Assert.ThrowsAsync<InitialAdminBootstrapException>(
            () => testHost.BootstrapAsync("Rollback Test", "invalid user name", password));

        await testHost.VerifyAsync(async services =>
        {
            var context = services.GetRequiredService<GenSWDbContext>();

            Assert.Equal(0, await context.Pessoas.CountAsync());
            Assert.Equal(0, await context.Users.CountAsync());
            Assert.Equal(0, await context.Roles.CountAsync());
            Assert.Equal(0, await context.UserRoles.CountAsync());
        });
    }

    private static string CreateTestPassword() => $"A5_{Guid.NewGuid():N}";

    private sealed class InitialAdminBootstrapTestHost : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly ServiceProvider services;

        private InitialAdminBootstrapTestHost(SqliteConnection connection, ServiceProvider services)
        {
            this.connection = connection;
            this.services = services;
        }

        public static async Task<InitialAdminBootstrapTestHost> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddDbContext<GenSWDbContext>(options => options.UseSqlite(connection));
            serviceCollection
                .AddIdentityCore<ApplicationUser>(options =>
                {
                    options.Password.RequiredLength = 15;
                    options.Password.RequireDigit = false;
                    options.Password.RequireLowercase = false;
                    options.Password.RequireUppercase = false;
                    options.Password.RequireNonAlphanumeric = false;
                })
                .AddRoles<IdentityRole<Guid>>()
                .AddEntityFrameworkStores<GenSWDbContext>();
            serviceCollection.AddScoped<InitialAdminBootstrapper>();

            var services = serviceCollection.BuildServiceProvider();
            await using (var scope = services.CreateAsyncScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GenSWDbContext>();
                await context.Database.EnsureCreatedAsync();
            }

            return new InitialAdminBootstrapTestHost(connection, services);
        }

        public async Task BootstrapAsync(string nome, string userName, string password)
        {
            await using var scope = services.CreateAsyncScope();
            var bootstrapper = scope.ServiceProvider.GetRequiredService<InitialAdminBootstrapper>();
            await bootstrapper.BootstrapAsync(nome, userName, password);
        }

        public async Task<Guid> CreateRoleAsync(string roleName)
        {
            await using var scope = services.CreateAsyncScope();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            var role = new IdentityRole<Guid>
            {
                Id = Guid.NewGuid(),
                Name = roleName,
            };
            var result = await roleManager.CreateAsync(role);

            Assert.True(result.Succeeded);
            return role.Id;
        }

        public async Task VerifyAsync(Func<IServiceProvider, Task> verification)
        {
            await using var scope = services.CreateAsyncScope();
            await verification(scope.ServiceProvider);
        }

        public async ValueTask DisposeAsync()
        {
            await services.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
