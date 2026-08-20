namespace GenSW.Infrastructure.Authentication;

public sealed class JwtOptions
{
    public const string SectionName = "Authentication:Jwt";

    public string Issuer { get; init; } = string.Empty;

    public string Audience { get; init; } = string.Empty;

    public int AccessTokenMinutes { get; init; } = 10;

    /// <summary>
    /// High-entropy external secret interpreted as UTF-8 bytes. It must never be committed to configuration files.
    /// </summary>
    public string SigningKey { get; init; } = string.Empty;
}
