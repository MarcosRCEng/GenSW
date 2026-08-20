namespace GenSW.Application.Authentication;

public sealed class CurrentUserResult
{
    public Guid UserId { get; init; }

    public Guid PessoaId { get; init; }

    public string Nome { get; init; } = string.Empty;

    public string UserName { get; init; } = string.Empty;

    public IReadOnlyCollection<string> Roles { get; init; } = Array.Empty<string>();
}
