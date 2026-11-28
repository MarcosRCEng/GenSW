using System.Data.Common;
using System.Security.Claims;
using GenSW.Application.Accounts;
using GenSW.Infrastructure.Accounts;
using GenSW.Infrastructure.Authentication;
using GenSW.Infrastructure.Catalog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GenSW.API.Authentication;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
internal sealed class AccountFileAccessAttribute : Attribute;

internal sealed class ErpAccountMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext http, CatalogBindings bindings, AccountScope account, TimeProvider clock)
    {
        try
        {
            var endpoint = http.GetEndpoint();
            if (endpoint is null || endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null ||
                endpoint.Metadata.GetMetadata<IAuthorizeData>() is null ||
                endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Any(a =>
                    a.AuthenticationSchemes?.Split(',').Contains(PlatformAuthentication.Scheme) == true ||
                    a.Policy == PlatformAuthentication.ManageAccountsPolicy || a.Policy == PlatformAuthentication.UserPolicy))
            {
                await next(http);
                return;
            }

            if (!CatalogSessionIdentity.TryRead(http.User, out var session) || session is null)
            {
                http.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            var resolved = await bindings.ResolveSessionAsync(session, clock.GetUtcNow(), http.RequestAborted);
            if (resolved is null)
            {
                http.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            await account.EstablishAsync(resolved.Account, resolved.Binding, http.RequestAborted);
            var local = account.Current;
            var claims = http.User.Claims.Where(claim => claim.Type != "role" && claim.Type != ErpActorClaim.Type)
                .Append(new Claim(ErpActorClaim.Type, local.ActorId.ToString("D")))
                .Concat(local.Roles.Select(role => new Claim("role", role)));
            http.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "ERP", "sub", "role"));
            http.Response.Headers.CacheControl = "private, no-store";

            if (endpoint.Metadata.GetMetadata<AccountFileAccessAttribute>() is not null)
            {
                await using var lease = await AccountFileLease.OpenAsync(account, http.RequestAborted);
                await next(http);
                return;
            }

            await next(http);
        }
        catch (Exception error) when (!http.RequestAborted.IsCancellationRequested && IsUnavailable(error))
        {
            if (http.Response.HasStarted)
            {
                http.Abort();
                return;
            }

            http.Response.Clear();
            http.Response.Headers.CacheControl = "private, no-store";
            await Results.Problem(statusCode: 503, title: "Vínculo da conta indisponível",
                extensions: new Dictionary<string, object?> { ["code"] = "vinculo_indisponivel" }).ExecuteAsync(http);
        }
    }

    private static bool IsUnavailable(Exception error) => error switch
    {
        AccountUnavailableException or TimeoutException => true,
        PostgresException pg => pg.SqlState is "GS001" or "40001" or "42501" or "3D000" or "55000" or "55P03" or "53300" or "57P01" or "57P02" or "57P03" ||
            pg.SqlState.StartsWith("08", StringComparison.Ordinal),
        DbException => true,
        DbUpdateException { InnerException: Exception inner } => IsUnavailable(inner),
        InvalidOperationException { InnerException: DbException provider } => IsUnavailable(provider),
        _ => false
    };
}

internal static class ErpActorClaim
{
    internal const string Type = "gensw_erp_actor";

    internal static Guid ErpActorId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(Type), out var id) && id != Guid.Empty
            ? id : throw new AccountUnavailableException();
}
