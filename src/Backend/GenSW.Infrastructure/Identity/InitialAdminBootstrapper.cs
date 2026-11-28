using System.Data;
using System.Data.Common;
using GenSW.Infrastructure.Authentication;
using GenSW.Infrastructure.Catalog;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GenSW.Infrastructure.Identity;

/// <summary>Explicitly grants the first platform manager from a separately authorized command.</summary>
public sealed class InitialAdminBootstrapper(
    CatalogDbContext catalog,
    UserManager<CatalogUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager)
{
    public const string AdminRoleName = PlatformAuthentication.ManageAccountsRole;

    public async Task BootstrapAsync(
        string nome, string userName, string password, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nome);
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        try
        {
            await using var transaction = await catalog.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);

            var grant = await catalog.InitialOperators.SingleOrDefaultAsync(cancellationToken);
            var user = await userManager.FindByNameAsync(userName);
            if (grant is not null)
            {
                if (user is not { IsActive: true } || grant.SubjectId != user.Id ||
                    !await userManager.CheckPasswordAsync(user, password) ||
                    !await userManager.IsInRoleAsync(user, AdminRoleName))
                    throw new InitialAdminBootstrapRejectedException();

                await transaction.CommitAsync(cancellationToken);
                return;
            }

            if (user is null)
            {
                user = new CatalogUser
                {
                    Id = Guid.NewGuid(),
                    UserName = userName,
                    DisplayName = nome.Trim(),
                    IsActive = true,
                    LockoutEnabled = true,
                };
                EnsureSucceeded(await userManager.CreateAsync(user, password));
            }
            else if (!user.IsActive || !await userManager.CheckPasswordAsync(user, password))
            {
                throw new InitialAdminBootstrapRejectedException();
            }

            if (!await roleManager.RoleExistsAsync(AdminRoleName))
                EnsureSucceeded(await roleManager.CreateAsync(new IdentityRole<Guid> { Id = Guid.NewGuid(), Name = AdminRoleName }));

            if (!await userManager.IsInRoleAsync(user, AdminRoleName))
                EnsureSucceeded(await userManager.AddToRoleAsync(user, AdminRoleName));

            catalog.InitialOperators.Add(new CatalogInitialOperator
            {
                Id = 1,
                SubjectId = user.Id,
                GrantedAtUtc = DateTimeOffset.UtcNow,
            });
            await catalog.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is DbException or DbUpdateException or TimeoutException)
        {
            throw new InitialAdminBootstrapException();
        }
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InitialAdminBootstrapException();
    }
}

public sealed class InitialAdminBootstrapRejectedException : InvalidOperationException
{
    public InitialAdminBootstrapRejectedException()
        : base("Initial platform manager bootstrap was rejected.")
    {
    }
}

public sealed class InitialAdminBootstrapException : InvalidOperationException
{
    public InitialAdminBootstrapException()
        : base("Initial platform manager bootstrap failed.")
    {
    }
}
