namespace GenSW.Application.People;

public interface IPessoaService
{
    Task<PessoaResult> CreateAsync(CreatePessoaCommand command, CancellationToken cancellationToken = default);

    Task<PessoaResult?> GetByIdAsync(Guid pessoaId, CancellationToken cancellationToken = default);

    Task<PagedPessoaResult> ListAsync(PessoaListQuery query, CancellationToken cancellationToken = default);

    Task<PessoaResult> UpdateAsync(Guid pessoaId, UpdatePessoaCommand command, CancellationToken cancellationToken = default);

    Task<PessoaResult> SetActiveAsync(Guid pessoaId, bool ativo, CancellationToken cancellationToken = default);
}
