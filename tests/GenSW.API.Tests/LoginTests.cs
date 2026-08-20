using System.Net;
using System.Text.Json;
using GenSW.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GenSW.API.Tests;

[Collection(AuthApiCollection.Name)]
public sealed class LoginTests(AuthWebApplicationFactory factory)
{
    [Fact]
    public async Task Login_with_active_user_and_correct_password_returns_access_token_and_secure_refresh_cookie()
    {
        var user = await factory.SeedUserAsync(UniqueUserName("login_ok"));
        using var client = factory.CreateHttpsClient();

        using var response = await client.LoginAsync(user.UserName, user.Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var (accessToken, expiresAtUtc) = await response.ReadAccessTokenAsync();
        Assert.NotEmpty(accessToken);
        Assert.True(expiresAtUtc > DateTimeOffset.UtcNow);

        using var responseJson = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var propertyNames = responseJson.RootElement
            .EnumerateObject()
            .Select(property => property.Name.ToLowerInvariant())
            .OrderBy(name => name)
            .ToArray();
        Assert.Equal(["accesstoken", "expiresatutc"], propertyNames);

        var cookie = response.GetIssuedCookie();
        Assert.StartsWith("GenSW", cookie.Name, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie.Header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie.Header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie.Header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/v1/auth", cookie.Header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("expires=", cookie.Header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("max-age=", cookie.Header, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", cookie.Header, StringComparison.OrdinalIgnoreCase);

        var maxAge = GetCookieMaxAge(cookie.Header);
        Assert.InRange(maxAge, 1, checked((int)TimeSpan.FromDays(7).TotalSeconds));
    }

    [Fact]
    public async Task Login_rejects_wrong_password_unknown_user_and_inactive_user_uniformly()
    {
        var active = await factory.SeedUserAsync(UniqueUserName("login_wrong"));
        var inactive = await factory.SeedUserAsync(UniqueUserName("login_inactive"), isActive: false);
        using var client = factory.CreateHttpsClient();

        using var wrongPassword = await client.LoginAsync(active.UserName, "GENSW_TEST_ONLY_wrong_password");
        using var unknownUser = await client.LoginAsync(
            UniqueUserName("login_unknown"),
            AuthWebApplicationFactory.ValidPassword);
        using var inactiveUser = await client.LoginAsync(inactive.UserName, inactive.Password);

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(wrongPassword.StatusCode, unknownUser.StatusCode);
        Assert.Equal(wrongPassword.StatusCode, inactiveUser.StatusCode);

        var wrongBody = await wrongPassword.Content.ReadAsStringAsync();
        var unknownBody = await unknownUser.Content.ReadAsStringAsync();
        var inactiveBody = await inactiveUser.Content.ReadAsStringAsync();
        Assert.Equal(GetPublicFailureShape(wrongBody), GetPublicFailureShape(unknownBody));
        Assert.Equal(GetPublicFailureShape(wrongBody), GetPublicFailureShape(inactiveBody));
        Assert.DoesNotContain("password", wrongBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("inactive", inactiveBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(active.UserName, wrongBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(inactive.UserName, inactiveBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_locks_user_after_five_failures()
    {
        var user = await factory.SeedUserAsync(UniqueUserName("login_lockout"));
        using var client = factory.CreateHttpsClient();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var failure = await client.LoginAsync(user.UserName, "GENSW_TEST_ONLY_wrong_password");
            Assert.Equal(HttpStatusCode.Unauthorized, failure.StatusCode);
        }

        var isLockedOut = await factory.ExecuteScopeAsync(async services =>
        {
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var storedUser = await userManager.FindByIdAsync(user.UserId.ToString("D"));
            return storedUser is not null && await userManager.IsLockedOutAsync(storedUser);
        });
        Assert.True(isLockedOut);

        using var correctPasswordWhileLocked = await client.LoginAsync(user.UserName, user.Password);
        Assert.Equal(HttpStatusCode.Unauthorized, correctPasswordWhileLocked.StatusCode);
    }

    [Fact]
    public async Task Successful_login_resets_previous_failed_access_count()
    {
        var user = await factory.SeedUserAsync(UniqueUserName("login_reset"));
        using var client = factory.CreateHttpsClient();

        using (var failure = await client.LoginAsync(user.UserName, "GENSW_TEST_ONLY_wrong_password"))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, failure.StatusCode);
        }

        var failedCount = await GetAccessFailedCountAsync(user.UserId);
        Assert.Equal(1, failedCount);

        using var success = await client.LoginAsync(user.UserName, user.Password);
        Assert.Equal(HttpStatusCode.OK, success.StatusCode);
        Assert.Equal(0, await GetAccessFailedCountAsync(user.UserId));
    }

    private Task<int> GetAccessFailedCountAsync(Guid userId)
    {
        return factory.ExecuteDbContextAsync(context => context.Users
            .Where(user => user.Id == userId)
            .Select(user => user.AccessFailedCount)
            .SingleAsync());
    }

    private static string GetPublicFailureShape(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return string.Empty;
        }

        using var document = JsonDocument.Parse(responseBody);
        var root = document.RootElement;
        return JsonSerializer.Serialize(new
        {
            Type = TryGetString(root, "type"),
            Title = TryGetString(root, "title"),
            Status = TryGetInt32(root, "status"),
            Detail = TryGetString(root, "detail"),
        });
    }

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static int? TryGetInt32(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) &&
            property.TryGetInt32(out var value)
            ? value
            : null;
    }

    private static int GetCookieMaxAge(string cookieHeader)
    {
        var maxAgePart = cookieHeader
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Single(part => part.StartsWith("max-age=", StringComparison.OrdinalIgnoreCase));
        return int.Parse(
            maxAgePart["max-age=".Length..],
            System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string UniqueUserName(string prefix) => $"{prefix}_{Guid.NewGuid():N}";
}
