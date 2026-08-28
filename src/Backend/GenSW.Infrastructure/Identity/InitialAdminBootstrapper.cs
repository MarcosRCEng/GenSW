using System.Data;
using GenSW.Domain.People;
using GenSW.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GenSW.Infrastructure.Identity;

/// <summary>
/// Explicitly provisions the sole initial administrative identity for an empty installation.
/// </summary>
public sealed class InitialAdminBootstrapper(
    GenSWDbContext context,
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager)
{
    public const string AdminRoleName = "Admin";

    public async Task BootstrapAsync(
        string nome,
        string userName,
        string password,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nome);
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        try
        {
            if (await context.Users.AnyAsync(cancellationToken))
            {
                throw new InitialAdminBootstrapRejectedException();
            }

            var now = DateTimeOffset.UtcNow;
            var pessoa = Pessoa.Criar(TipoPessoa.Fisica, nome, null, now);

            context.Pessoas.Add(pessoa);
            await context.SaveChangesAsync(cancellationToken);

            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                PessoaId = pessoa.Id,
                UserName = userName,
                IsActive = true,
                LockoutEnabled = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            };

            EnsureSucceeded(await userManager.CreateAsync(user, password));

            if (!await roleManager.RoleExistsAsync(AdminRoleName))
            {
                var role = new IdentityRole<Guid>
                {
                    Id = Guid.NewGuid(),
                    Name = AdminRoleName,
                };
                EnsureSucceeded(await roleManager.CreateAsync(role));
            }

            EnsureSucceeded(await userManager.AddToRoleAsync(user, AdminRoleName));

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InitialAdminBootstrapException();
        }
    }
}

public sealed class InitialAdminBootstrapRejectedException : InvalidOperationException
{
    public InitialAdminBootstrapRejectedException()
        : base("Initial administrator bootstrap is allowed only when no application users exist.")
    {
    }
}

public sealed class InitialAdminBootstrapException : InvalidOperationException
{
    public InitialAdminBootstrapException()
        : base("Initial administrator bootstrap failed.")
    {
    }
}
