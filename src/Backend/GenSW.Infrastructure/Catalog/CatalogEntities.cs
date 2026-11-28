using Microsoft.AspNetCore.Identity;

namespace GenSW.Infrastructure.Catalog;

public sealed class CatalogUser : IdentityUser<Guid>
{
    public bool IsActive { get; set; }
    public string DisplayName { get; set; } = string.Empty;
}

public enum CatalogAccountState
{
    Pending = 1,
    Active = 2,
    Deactivating = 3,
    Inactive = 4,
}

public sealed class CatalogAccount
{
    public Guid Id { get; set; }
    public Guid SubjectId { get; set; }
    public bool Confirmed { get; set; }
    public CatalogAccountState State { get; set; } = CatalogAccountState.Pending;
    public long AccessGeneration { get; set; } = 1;
}

public sealed class CatalogBinding
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public long Revision { get; set; }
    public Guid ErpActorId { get; set; }
    public Guid DatabaseId { get; set; }
    public string DatabaseName { get; set; } = string.Empty;
    public Guid VolumeId { get; set; }
    public string DatabaseSecretRef { get; set; } = string.Empty;
    public string MaintenanceSecretRef { get; set; } = string.Empty;
    public string VolumeRef { get; set; } = string.Empty;
    public string ExpectedMigration { get; set; } = string.Empty;
    public string? VerifiedMigration { get; set; }
    public string? LastMaintenanceFailure { get; set; }
    public bool Published { get; set; }
    public bool Ready { get; set; }
    public bool InMaintenance { get; set; }
}

public enum CatalogSessionAudience
{
    Erp = 1,
    Platform = 2,
}

public sealed class CatalogSession
{
    public Guid Id { get; set; }
    public Guid SubjectId { get; set; }
    public CatalogSessionAudience Audience { get; set; }
    public Guid? AccountId { get; set; }
    public Guid? BindingId { get; set; }
    public long? BindingRevision { get; set; }
    public long? AccessGeneration { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
}

public sealed class CatalogRefreshToken
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public byte[] Digest { get; set; } = [];
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? ConsumedAtUtc { get; set; }
}

public sealed class CatalogInitialOperator
{
    public int Id { get; set; }
    public Guid SubjectId { get; set; }
    public DateTimeOffset GrantedAtUtc { get; set; }
}
