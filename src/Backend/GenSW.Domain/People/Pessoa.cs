namespace GenSW.Domain.People;

/// <summary>
/// Represents a person in the ERP business domain. Future people use cases are responsible for
/// assigning and updating the UTC timestamps; the identity persistence foundation does not automate them.
/// </summary>
public sealed class Pessoa
{
    public Guid Id { get; set; }

    public string Nome { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
