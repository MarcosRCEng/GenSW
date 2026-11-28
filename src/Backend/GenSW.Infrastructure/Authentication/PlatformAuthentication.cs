using System.Data.Common;
using System.Security.Claims;
using GenSW.Application.Accounts;
using GenSW.Application.Authentication;
using GenSW.Infrastructure.Catalog;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;

namespace GenSW.Infrastructure.Authentication;

public static class PlatformAuthentication
{
    public const string Scheme = "GenSW.Platform";
    public const string ManageAccountsPolicy = "GenSW.Platform.ManageAccounts";
    public const string UserPolicy = "GenSW.Platform.User";
    public const string ManageAccountsRole = "PlatformAccountManager";
    public const string AccessRole = "PlatformOperator";

    internal static IServiceCollection AddPlatformAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication().AddJwtBearer(Scheme, _ => { });
        services.AddScoped<PlatformJwtEvents>();
        services.AddScoped<IPlatformAuthenticationSessionService, AuthenticationSessionService>();
        services.AddAuthorization(options =>
        {
            options.AddPolicy(UserPolicy, policy => policy.AddAuthenticationSchemes(Scheme).RequireAuthenticatedUser());
            options.AddPolicy(ManageAccountsPolicy, policy => policy.AddAuthenticationSchemes(Scheme)
                .RequireAuthenticatedUser().RequireRole(ManageAccountsRole));
        });
        return services;
    }
}

internal sealed class PlatformJwtEvents(IPlatformAuthenticationSessionService sessions) : JwtBearerEvents
{
    public override async Task TokenValidated(TokenValidatedContext context)
    {
        try
        {
            if (context.Principal is not { } principal ||
                !CatalogSessionIdentity.TryReadSubjectAndSession(principal, out var subjectId, out var sessionId) ||
                await sessions.GetSessionUserAsync(subjectId, sessionId, context.HttpContext.RequestAborted) is not { } user)
            {
                context.Fail("Invalid session.");
                return;
            }

            var identity = (ClaimsIdentity)principal.Identity!;
            foreach (var claim in identity.FindAll(identity.RoleClaimType).ToArray())
                identity.RemoveClaim(claim);
            foreach (var role in user.Roles)
                identity.AddClaim(new Claim(identity.RoleClaimType, role));
        }
        catch (Exception ex) when (ex is DbException or TimeoutException)
        {
            throw new AccountUnavailableException();
        }
    }
}
