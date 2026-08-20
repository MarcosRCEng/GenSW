namespace GenSW.Infrastructure.Identity;

/// <summary>
/// Persistence model reserved for future refresh-token rotation. Only canonical digest bytes are
/// persisted; the raw refresh token must never be stored.
/// </summary>
public sealed class RefreshSession
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public Guid FamilyId { get; set; }

    public byte[] TokenHash { get; set; } = [];

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public DateTimeOffset? RevokedAtUtc { get; set; }

    public Guid? ReplacedBySessionId { get; set; }

    public RefreshSession? ReplacedBySession { get; set; }
}
