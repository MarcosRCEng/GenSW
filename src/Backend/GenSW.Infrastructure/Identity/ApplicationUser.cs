using GenSW.Domain.People;
using Microsoft.AspNetCore.Identity;

namespace GenSW.Infrastructure.Identity;

/// <summary>
/// Human identity persisted by ASP.NET Core Identity. Future identity use cases are responsible for
/// assigning and updating the UTC timestamps.
/// </summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public Guid PessoaId { get; set; }

    public Pessoa Pessoa { get; set; } = null!;

    public bool IsActive { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
