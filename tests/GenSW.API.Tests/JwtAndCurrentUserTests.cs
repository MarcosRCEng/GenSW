using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace GenSW.API.Tests;

[Collection(AuthApiCollection.Name)]
public sealed class JwtAndCurrentUserTests(AuthWebApplicationFactory factory)
{
    [Fact]
    public async Task Login_emits_required_HS256_claims_subject_and_roles()
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
        Assert.Contains(AuthWebApplicationFactory.JwtAudience, token.Audiences);
        Assert.Equal(user.UserId.ToString("D"), token.Claims.Single(claim => claim.Type == "sub").Value);
        Assert.False(string.IsNullOrWhiteSpace(token.Claims.Single(claim => claim.Type == "jti").Value));

        var issuedAt = long.Parse(
            token.Claims.Single(claim => claim.Type == "iat").Value,
            CultureInfo.InvariantCulture);
        var expiresAt = long.Parse(
            token.Claims.Single(claim => claim.Type == "exp").Value,
            CultureInfo.InvariantCulture);
        Assert.Equal(600, expiresAt - issuedAt);
        Assert.Equal(expiresAtUtc.ToUnixTimeSeconds(), expiresAt);
        Assert.Equal(
            ["Operator", "Reader"],
            token.Claims.Where(claim => claim.Type == "role").Select(claim => claim.Value).ToArray());
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
        Assert.Equal(user.UserId, AuthTestHttp.GetProperty(root, "userId").GetGuid());
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
        Assert.Equal(["nome", "pessoaid", "roles", "userid", "username"], propertyNames);
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

    [Theory]
    [InlineData("signature")]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("expired")]
    public async Task Me_rejects_invalid_JWT_validation_dimension(string invalidDimension)
    {
        var user = await factory.SeedUserAsync(UniqueUserName($"jwt_{invalidDimension}"));
        var token = CreateToken(user.UserId, invalidDimension);
        using var client = factory.CreateHttpsClient();

        using var response = await client.GetWithBearerAsync("/api/v1/auth/me", token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static string CreateToken(Guid userId, string invalidDimension)
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
            : AuthWebApplicationFactory.JwtAudience;
        var expiresAt = invalidDimension == "expired"
            ? now.AddMinutes(-1)
            : now.AddMinutes(10);
        var token = new JwtSecurityToken(
            issuer,
            audience,
            [
                new Claim("sub", userId.ToString("D")),
                new Claim("jti", Guid.NewGuid().ToString("N")),
                new Claim(
                    "iat",
                    now.AddMinutes(-20).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
                    ClaimValueTypes.Integer64),
            ],
            notBefore: now.AddMinutes(-20).UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string UniqueUserName(string prefix) => $"{prefix}_{Guid.NewGuid():N}";
}
