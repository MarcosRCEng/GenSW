namespace GenSW.Application.Authentication;

public sealed class AuthenticationSessionResult
{
    public AccessTokenResult AccessToken { get; init; } = new();

    public string RefreshToken { get; init; } = string.Empty;

    public DateTimeOffset RefreshTokenExpiresAtUtc { get; init; }
}
