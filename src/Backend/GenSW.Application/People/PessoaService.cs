using GenSW.Domain.People;

namespace GenSW.Application.People;

public sealed class PessoaService(IPessoaRepository repository, TimeProvider timeProvider) : IPessoaService
{
    public async Task<PessoaResult> CreateAsync(CreatePessoaCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var pessoa = Pessoa.Criar(command.TipoPessoa, command.Nome, command.NomeFantasia, timeProvider.GetUtcNow());
        await repository.AddAsync(pessoa, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return ToResult(pessoa);
    }

    public async Task<PessoaResult?> GetByIdAsync(Guid pessoaId, CancellationToken cancellationToken = default)
    {
        var pessoa = await repository.GetByIdAsync(pessoaId, tracking: false, cancellationToken);
        return pessoa is null ? null : ToResult(pessoa);
    }

    public async Task<PagedPessoaResult> ListAsync(PessoaListQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ValidateListQuery(query);
        var normalizedQuery = query with { Search = NormalizeSearch(query.Search) };
        var page = await repository.ListAsync(normalizedQuery, cancellationToken);
        var totalPages = page.TotalItems == 0 ? 0 : (int)Math.Ceiling(page.TotalItems / (double)normalizedQuery.PageSize);

        return new PagedPessoaResult(
            page.Items.Select(ToResult).ToArray(),
            normalizedQuery.Page,
            normalizedQuery.PageSize,
            page.TotalItems,
            totalPages);
    }

    public async Task<PessoaResult> UpdateAsync(Guid pessoaId, UpdatePessoaCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var pessoa = await GetTrackedAsync(pessoaId, cancellationToken);
        if (!pessoa.Ativo)
        {
            throw new PessoaInactiveException(pessoaId);
        }

        pessoa.AlterarCadastro(command.Nome, command.NomeFantasia, timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);
        return ToResult(pessoa);
    }

    public async Task<PessoaResult> SetActiveAsync(Guid pessoaId, bool ativo, CancellationToken cancellationToken = default)
    {
        var pessoa = await GetTrackedAsync(pessoaId, cancellationToken);
        var nowUtc = timeProvider.GetUtcNow();
        if (ativo)
        {
            pessoa.Reativar(nowUtc);
        }
        else
        {
            pessoa.Inativar(nowUtc);
        }

        await repository.SaveChangesAsync(cancellationToken);
        return ToResult(pessoa);
    }

    private async Task<Pessoa> GetTrackedAsync(Guid pessoaId, CancellationToken cancellationToken)
        => await repository.GetByIdAsync(pessoaId, tracking: true, cancellationToken)
            ?? throw new PessoaNotFoundException(pessoaId);

    private static void ValidateListQuery(PessoaListQuery query)
    {
        if (query.Page < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(query.Page));
        }

        if (query.PageSize is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(query.PageSize));
        }

        if (!Enum.IsDefined(query.SortBy))
        {
            throw new ArgumentOutOfRangeException(nameof(query.SortBy));
        }
    }

    private static string? NormalizeSearch(string? search) => string.IsNullOrWhiteSpace(search) ? null : search.Trim();

    private static PessoaResult ToResult(Pessoa pessoa) => new(
        pessoa.Id,
        pessoa.TipoPessoa,
        pessoa.Nome,
        pessoa.NomeFantasia,
        pessoa.Ativo,
        pessoa.CreatedAtUtc,
        pessoa.UpdatedAtUtc);
}
