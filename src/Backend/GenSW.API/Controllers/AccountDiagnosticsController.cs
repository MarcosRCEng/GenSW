using GenSW.Application.Accounts;
using GenSW.Infrastructure.Accounts;
using GenSW.Infrastructure.Authentication;
using GenSW.Infrastructure.Catalog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GenSW.API.Controllers;

[ApiController, Route("api/v1/platform/accounts"),
 Authorize(Policy = PlatformAuthentication.ManageAccountsPolicy)]
public sealed class AccountDiagnosticsController(CatalogDbContext catalog, IAccountDestinationVerifier verifier) : ControllerBase
{
    [HttpGet("{id:guid}/diagnostics")]
    public async Task<IActionResult> Diagnose(Guid id, CancellationToken ct)
    {
        Response.Headers.CacheControl = "private, no-store";
        var account = await catalog.Accounts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (account is null)
            return NotFound();

        var bindings = await catalog.Bindings.AsNoTracking().Where(x => x.AccountId == id && x.Published)
            .Take(2).ToListAsync(ct);
        var binding = bindings.Count == 1 ? bindings[0] : null;
        var availability = "unavailable";
        if (binding is { InMaintenance: false, Ready: true } && account.State == CatalogAccountState.Active && account.Confirmed)
        {
            try
            {
                await verifier.VerifyAsync(account, binding, ct);
                availability = "ready";
            }
            catch (AccountUnavailableException) { }
        }
        return Ok(new
        {
            accountId = account.Id,
            state = account.State.ToString(),
            bindingRevision = binding?.Revision,
            inMaintenance = binding?.InMaintenance ?? false,
            availability,
            expectedMigration = binding?.ExpectedMigration,
            verifiedMigration = binding?.VerifiedMigration,
            maintenanceFailure = binding?.LastMaintenanceFailure,
        });
    }
}
