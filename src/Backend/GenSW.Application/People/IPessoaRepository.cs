using GenSW.Domain.People;

namespace GenSW.Application.People;

public interface IPessoaRepository
{
    Task AddAsync(Pessoa pessoa, CancellationToken cancellationToken = default);

    Task<Pessoa?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Pessoa?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PessoaListPage> ListAsync(PessoaListQuery query, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public sealed record PessoaListPage(IReadOnlyList<Pessoa> Items, int TotalItems);
