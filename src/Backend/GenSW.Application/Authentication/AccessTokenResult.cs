namespace GenSW.Application.Authentication;

public sealed class AccessTokenResult
{
    public string AccessToken { get; init; } = string.Empty;

    public DateTimeOffset ExpiresAtUtc { get; init; }

    public Guid SessionId { get; init; }
}
