using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using GenSW.Infrastructure.Authentication;
using GenSW.Infrastructure.Catalog;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace GenSW.API.Tests;

[Collection(AuthApiCollection.Name)]
public sealed class JwtAndCurrentUserTests(AuthWebApplicationFactory factory)
{
    [Fact]
    public async Task Login_emits_HS256_claims_with_central_subject_and_session()
    {
        var user = await factory.SeedUserAsync(
            UniqueUserName("jwt_claims"),
            roles: ["Operator", "Reader"]);
        using var client = factory.CreateHttpsClient();
        using var response = await client.LoginAsync(user.UserName, user.Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var (encodedToken, expiresAtUtc) = await response.ReadAccessTokenAsync();
        var token = new JwtSecurityTokenHandler().ReadJwtToken(encodedToken);

        Assert.Equal(SecurityAlgorithms.HmacSha256, token.Header.Alg);
        Assert.Equal(AuthWebApplicationFactory.JwtIssuer, token.Issuer);
        Assert.Contains(AuthWebApplicationFactory.JwtAudience + ":erp-v2", token.Audiences);
        Assert.Equal(user.SubjectId.ToString("D"), token.Claims.Single(claim => claim.Type == "sub").Value);
        Assert.NotEqual(Guid.Empty, Guid.Parse(token.Claims.Single(claim => claim.Type == "sid").Value));
        Assert.NotEqual(Guid.Empty, Guid.Parse(token.Claims.Single(claim => claim.Type == "aid").Value));
        Assert.NotEqual(Guid.Empty, Guid.Parse(token.Claims.Single(claim => claim.Type == "bid").Value));
        Assert.Equal("1", token.Claims.Single(claim => claim.Type == "brv").Value);
        Assert.Equal("1", token.Claims.Single(claim => claim.Type == "gen").Value);
        Assert.False(string.IsNullOrWhiteSpace(token.Claims.Single(claim => claim.Type == "jti").Value));

        var issuedAt = long.Parse(
            token.Claims.Single(claim => claim.Type == "iat").Value,
            CultureInfo.InvariantCulture);
        var expiresAt = long.Parse(
            token.Claims.Single(claim => claim.Type == "exp").Value,
            CultureInfo.InvariantCulture);
        Assert.Equal(600, expiresAt - issuedAt);
        Assert.Equal(expiresAtUtc.ToUnixTimeSeconds(), expiresAt);
        Assert.DoesNotContain(token.Claims, claim => claim.Type == "role");
    }

    [Fact]
    public async Task Me_with_valid_token_returns_only_minimum_current_user_data()
    {
        var user = await factory.SeedUserAsync(
            UniqueUserName("me_valid"),
            roles: ["Operator"]);
        using var client = factory.CreateHttpsClient();
        using var login = await client.LoginAsync(user.UserName, user.Password);
        var (accessToken, _) = await login.ReadAccessTokenAsync();

        using var response = await client.GetWithBearerAsync("/api/v1/auth/me", accessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var responseBody = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(responseBody);
        var root = document.RootElement;
        Assert.Equal(user.SubjectId, AuthTestHttp.GetProperty(root, "userId").GetGuid());
        Assert.Equal(user.SubjectId, AuthTestHttp.GetProperty(root, "subjectId").GetGuid());
        Assert.Equal(user.UserId, AuthTestHttp.GetProperty(root, "erpActorId").GetGuid());
        Assert.Equal(user.PessoaId, AuthTestHttp.GetProperty(root, "pessoaId").GetGuid());
        Assert.Equal(user.Nome, AuthTestHttp.GetProperty(root, "nome").GetString());
        Assert.Equal(user.UserName, AuthTestHttp.GetProperty(root, "userName").GetString());
        Assert.Equal(
            ["Operator"],
            AuthTestHttp.GetProperty(root, "roles")
                .EnumerateArray()
                .Select(role => role.GetString()
                    ?? throw new InvalidOperationException("Role value was null."))
                .ToArray());

        var propertyNames = root
            .EnumerateObject()
            .Select(property => property.Name.ToLowerInvariant())
            .OrderBy(name => name)
            .ToArray();
        Assert.Equal(["erpactorid", "nome", "pessoaid", "roles", "subjectid", "userid", "username"], propertyNames);
        Assert.DoesNotContain("passwordhash", responseBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("securitystamp", responseBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("refreshsession", responseBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Me_without_token_returns_unauthorized()
    {
        using var client = factory.CreateHttpsClient();

        using var response = await client.GetAsync("/api/v1/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Platform_and_erp_sessions_are_valid_only_for_their_audience()
    {
        var erpAdmin = await factory.SeedUserAsync(UniqueUserName("erp_admin"), roles: ["Admin"]);
        var platformName = UniqueUserName("central_operator");
        await factory.ExecuteScopeAsync(async services =>
        {
            var users = services.GetRequiredService<UserManager<CatalogUser>>();
            var roles = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            var platformUser = new CatalogUser { Id = Guid.NewGuid(), UserName = platformName, IsActive = true };
            Assert.True((await users.CreateAsync(platformUser, AuthWebApplicationFactory.ValidPassword)).Succeeded);
            if (!await roles.RoleExistsAsync(PlatformAuthentication.ManageAccountsRole))
                Assert.True((await roles.CreateAsync(new IdentityRole<Guid>(PlatformAuthentication.ManageAccountsRole))).Succeeded);
            Assert.True((await users.AddToRoleAsync(platformUser, PlatformAuthentication.ManageAccountsRole)).Succeeded);
            return true;
        });

        using var client = factory.CreateHttpsClient();
        const string platformLogin = "/api/v1/platform/auth/login";
        const string platformMe = "/api/v1/platform/auth/me";
        const string erpMe = "/api/v1/auth/me";
        using var platform = await client.PostAsJsonAsync(platformLogin,
            new { userName = platformName, password = AuthWebApplicationFactory.ValidPassword });
        Assert.Equal(HttpStatusCode.OK, platform.StatusCode);
        var (platformToken, _) = await platform.ReadAccessTokenAsync();
        Assert.Contains("path=/api/v1/platform/auth", platform.GetIssuedCookie().Header);
        Assert.Equal(HttpStatusCode.OK, (await client.GetWithBearerAsync(platformMe, platformToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetWithBearerAsync(erpMe, platformToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.LoginAsync(platformName, AuthWebApplicationFactory.ValidPassword)).StatusCode);

        using var erp = await client.LoginAsync(erpAdmin.UserName, erpAdmin.Password);
        var (erpToken, _) = await erp.ReadAccessTokenAsync();
        Assert.Equal(HttpStatusCode.OK, (await client.GetWithBearerAsync(erpMe, erpToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetWithBearerAsync(platformMe, erpToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(platformLogin,
            new { userName = erpAdmin.UserName, password = erpAdmin.Password })).StatusCode);
    }

    [Theory]
    [InlineData("signature")]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("expired")]
    public async Task Me_rejects_invalid_JWT_validation_dimension(string invalidDimension)
    {
        var user = await factory.SeedUserAsync(UniqueUserName($"jwt_{invalidDimension}"));
        using var client = factory.CreateHttpsClient();
        using var login = await client.LoginAsync(user.UserName, user.Password);
        var issued = new JwtSecurityTokenHandler().ReadJwtToken((await login.ReadAccessTokenAsync()).AccessToken);
        var token = CreateToken(issued, invalidDimension);

        using var response = await client.GetWithBearerAsync("/api/v1/auth/me", token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static string CreateToken(JwtSecurityToken validToken, string invalidDimension)
    {
        var now = DateTimeOffset.UtcNow;
        var signingKey = invalidDimension == "signature"
            ? "GENSW_TEST_ONLY_DIFFERENT_KEY_0123456789_ABCDEFGHIJKLMNOPQRSTUVWXYZ"
            : AuthWebApplicationFactory.JwtSigningKey;
        var issuer = invalidDimension == "issuer"
            ? "GenSW.Tests.InvalidIssuer"
            : AuthWebApplicationFactory.JwtIssuer;
        var audience = invalidDimension == "audience"
            ? "GenSW.Tests.InvalidAudience"
            : AuthWebApplicationFactory.JwtAudience + ":erp-v2";
        var expiresAt = invalidDimension == "expired"
            ? now.AddMinutes(-1)
            : now.AddMinutes(10);
        var token = new JwtSecurityToken(
            issuer,
            audience,
            validToken.Claims.Where(claim => claim.Type is not ("aud" or "iss" or "exp" or "nbf")),
            notBefore: now.AddMinutes(-20).UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string UniqueUserName(string prefix) => $"{prefix}_{Guid.NewGuid():N}";
}
