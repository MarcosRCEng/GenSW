using System.Globalization;
using System.Security.Claims;

namespace GenSW.Infrastructure.Catalog;

public sealed record CatalogSessionIdentity(
    Guid SubjectId, Guid SessionId, Guid AccountId, Guid BindingId, long BindingRevision, long AccessGeneration)
{
    public const string SubjectClaim = "sub";
    public const string SessionClaim = "sid";
    public const string AccountClaim = "aid";
    public const string BindingClaim = "bid";
    public const string BindingRevisionClaim = "brv";
    public const string AccessGenerationClaim = "gen";

    public static bool TryRead(ClaimsPrincipal principal, out CatalogSessionIdentity? identity)
    {
        identity = null;
        if (!TryReadSubjectAndSession(principal, out var subjectId, out var sessionId) ||
            !TryGuid(principal, AccountClaim, out var accountId) ||
            !TryGuid(principal, BindingClaim, out var bindingId) ||
            !TryVersion(principal, BindingRevisionClaim, out var revision) ||
            !TryVersion(principal, AccessGenerationClaim, out var generation))
            return false;

        identity = new(subjectId, sessionId, accountId, bindingId, revision, generation);
        return true;
    }

    public static bool TryReadSubjectAndSession(ClaimsPrincipal principal, out Guid subjectId, out Guid sessionId)
    {
        subjectId = sessionId = Guid.Empty;
        return principal.Identity?.IsAuthenticated == true &&
            TryGuid(principal, SubjectClaim, out subjectId) && TryGuid(principal, SessionClaim, out sessionId);
    }

    internal bool Matches(CatalogErpBinding resolved) =>
        resolved.Account.Id == AccountId && resolved.Account.SubjectId == SubjectId &&
        resolved.Account.AccessGeneration == AccessGeneration &&
        resolved.Binding.Id == BindingId && resolved.Binding.Revision == BindingRevision;

    private static bool TryGuid(ClaimsPrincipal principal, string type, out Guid value) =>
        Guid.TryParseExact(ReadClaim(principal, type), "D", out value) && value != Guid.Empty;

    private static bool TryVersion(ClaimsPrincipal principal, string type, out long value) =>
        long.TryParse(ReadClaim(principal, type), NumberStyles.None, CultureInfo.InvariantCulture, out value) && value > 0;

    private static string? ReadClaim(ClaimsPrincipal principal, string type)
    {
        using var claims = principal.FindAll(type).GetEnumerator();
        if (!claims.MoveNext())
            return null;

        var value = claims.Current.Value;
        return claims.MoveNext() ? null : value;
    }
}
