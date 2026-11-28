namespace GenSW.Application.Accounts;

public sealed record ErpAccountKey(Guid AccountId, Guid BindingId, long BindingRevision, long AccessGeneration, Guid ActorId);

public sealed record ErpAccountContext(
    Guid AccountId, Guid BindingId, long BindingRevision, long AccessGeneration,
    Guid ActorId, Guid PessoaId, string ActorName, IReadOnlyList<string> Roles,
    CancellationToken RevocationToken)
{
    public ErpAccountKey Key => new(AccountId, BindingId, BindingRevision, AccessGeneration, ActorId);
}

public interface IErpAccountScope
{
    ErpAccountContext Current { get; }
    Task BindAsync(Guid accountId, Guid bindingId, long bindingRevision, long accessGeneration,
        Guid actorId, CancellationToken ct);
    Task<IReadOnlyList<ErpAccountKey>> ListActiveAsync(CancellationToken ct);
    Task RevalidateAsync(CancellationToken ct);
}

public sealed class AccountUnavailableException() : Exception("O vínculo da conta está indisponível.");
