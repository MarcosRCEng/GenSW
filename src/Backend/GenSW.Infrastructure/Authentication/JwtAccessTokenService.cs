using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GenSW.Application.Authentication;
using GenSW.Infrastructure.Catalog;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace GenSW.Infrastructure.Authentication;

internal sealed class JwtAccessTokenService : IAccessTokenService
{
    private readonly JwtOptions options;
    private readonly TimeProvider timeProvider;
    private readonly SigningCredentials signingCredentials;

    public JwtAccessTokenService(IOptions<JwtOptions> options, TimeProvider timeProvider)
    {
        this.options = options.Value;
        this.timeProvider = timeProvider;
        signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(this.options.SigningKey)),
            SecurityAlgorithms.HmacSha256);
    }

    public AccessTokenResult Create(Guid subjectId, Guid sessionId, DateTimeOffset sessionExpiry, ErpAccessClaims? erp)
    {
        var now = timeProvider.GetUtcNow();
        var expiresAtUtc = now.AddMinutes(options.AccessTokenMinutes);
        if (sessionExpiry < expiresAtUtc)
            expiresAtUtc = sessionExpiry;
        var claims = new List<Claim>
        {
            new(CatalogSessionIdentity.SubjectClaim, subjectId.ToString("D", CultureInfo.InvariantCulture)),
            new(CatalogSessionIdentity.SessionClaim, sessionId.ToString("D", CultureInfo.InvariantCulture)),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)),
            new(
                JwtRegisteredClaimNames.Iat,
                EpochTime.GetIntDate(now.UtcDateTime).ToString(CultureInfo.InvariantCulture),
                ClaimValueTypes.Integer64),
        };

        if (erp is not null)
        {
            claims.Add(new Claim(CatalogSessionIdentity.AccountClaim, erp.AccountId.ToString("D")));
            claims.Add(new Claim(CatalogSessionIdentity.BindingClaim, erp.BindingId.ToString("D")));
            claims.Add(new Claim(CatalogSessionIdentity.BindingRevisionClaim,
                erp.BindingRevision.ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64));
            claims.Add(new Claim(CatalogSessionIdentity.AccessGenerationClaim,
                erp.AccessGeneration.ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64));
        }

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.GetAudience(erp is null),
            claims: claims,
            expires: expiresAtUtc.UtcDateTime,
            signingCredentials: signingCredentials);

        return new AccessTokenResult
        {
            AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
            ExpiresAtUtc = expiresAtUtc,
            SessionId = sessionId,
        };
    }
}
