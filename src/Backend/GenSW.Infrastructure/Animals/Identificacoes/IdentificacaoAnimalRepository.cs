using GenSW.Application.Animals.Identificacoes;
using GenSW.Domain.Animals;
using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace GenSW.Infrastructure.Animals.Identificacoes;

public sealed class IdentificacaoAnimalRepository(GenSWDbContext context) : IIdentificacaoAnimalRepository
{
    private const string TipoValorIndex = "UX_IdentificacoesAnimal_Tipo_Valor_CaseInsensitive";
    // PostgreSQL identifiers are limited to 63 bytes; this is the persisted name of the migration index.
    private const string OutroDescricaoTipoValorIndex = "UX_IdentificacoesAnimal_Outro_DescricaoTipo_Valor_CaseInsensiti";
    private const string PrincipalAtivaIndex = "UX_IdentificacoesAnimal_Animal_PrincipalAtiva";

    public async Task<IIdentificacaoAnimalMutationScope> BeginMutationAsync(CancellationToken cancellationToken = default)
    {
        var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        return new MutationScope(transaction);
    }

    public Task<Animal?> LockAnimalAsync(Guid animalId, CancellationToken cancellationToken = default) =>
        context.Animais.FromSqlInterpolated($"SELECT * FROM \"Animais\" WHERE \"Id\" = {animalId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

    public Task<bool> AnimalExistsAsync(Guid animalId, CancellationToken cancellationToken = default) =>
        context.Animais.AsNoTracking().AnyAsync(animal => animal.Id == animalId, cancellationToken);

    public Task AddAsync(IdentificacaoAnimal identificacao, CancellationToken cancellationToken = default) =>
        context.IdentificacoesAnimal.AddAsync(identificacao, cancellationToken).AsTask();

    public Task<IdentificacaoAnimal?> GetByAnimalForUpdateAsync(Guid animalId, Guid identificacaoId,
        CancellationToken cancellationToken = default) =>
        context.IdentificacoesAnimal.SingleOrDefaultAsync(item => item.AnimalId == animalId && item.Id == identificacaoId,
            cancellationToken);

    public Task<IdentificacaoAnimalResult?> GetByAnimalReadOnlyAsync(Guid animalId, Guid identificacaoId,
        CancellationToken cancellationToken = default) =>
        ReadModels(context.IdentificacoesAnimal.AsNoTracking().Where(item => item.AnimalId == animalId && item.Id == identificacaoId))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<PagedIdentificacaoAnimalResult> ListByAnimalAsync(Guid animalId, IdentificacaoAnimalListQuery query,
        CancellationToken cancellationToken = default)
    {
        var filtered = ApplyFilters(context.IdentificacoesAnimal.AsNoTracking().Where(item => item.AnimalId == animalId), query);
        var totalItems = await filtered.CountAsync(cancellationToken);
        var items = await ReadModels(ApplyOrdering(filtered)).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .ToListAsync(cancellationToken);
        return new PagedIdentificacaoAnimalResult(items, query.Page, query.PageSize, totalItems, TotalPages(totalItems, query.PageSize));
    }

    public async Task<PagedIdentificacaoAnimalGlobalResult> ListGlobalAsync(IdentificacaoAnimalListQuery query,
        CancellationToken cancellationToken = default)
    {
        var filtered = ApplyFilters(context.IdentificacoesAnimal.AsNoTracking(), query);
        var totalItems = await filtered.CountAsync(cancellationToken);
        var items = await (from identification in ApplyOrdering(filtered).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
                           join animal in context.Animais.AsNoTracking() on identification.AnimalId equals animal.Id
                           select new
                           {
                               Identification = ToResult(identification),
                               Animal = new IdentificacaoAnimalAnimalResumo(animal.Id, animal.CodigoInterno, animal.Nome),
                               identification.CreatedAtUtc,
                               identification.Id
                           })
            .OrderByDescending(item => item.CreatedAtUtc).ThenByDescending(item => item.Id)
            .Select(item => new IdentificacaoAnimalGlobalResult(item.Identification, item.Animal))
            .ToListAsync(cancellationToken);
        return new PagedIdentificacaoAnimalGlobalResult(items, query.Page, query.PageSize, totalItems, TotalPages(totalItems, query.PageSize));
    }

    public Task<bool> HasDuplicateAsync(TipoIdentificacaoAnimal tipo, string? descricaoTipo, string valor,
        CancellationToken cancellationToken = default)
    {
        var escapedValue = EscapeLikePattern(valor);
        var items = context.IdentificacoesAnimal.AsNoTracking().Where(item => item.Tipo == tipo &&
            EF.Functions.ILike(item.Valor, escapedValue, "\\"));
        if (tipo == TipoIdentificacaoAnimal.Outro)
        {
            var escapedDescricao = EscapeLikePattern(descricaoTipo!);
            items = items.Where(item => EF.Functions.ILike(item.DescricaoTipo!, escapedDescricao, "\\"));
        }
        return items.AnyAsync(cancellationToken);
    }

    public Task<IdentificacaoAnimal?> GetCurrentPrincipalForUpdateAsync(Guid animalId,
        CancellationToken cancellationToken = default) =>
        context.IdentificacoesAnimal.SingleOrDefaultAsync(item => item.AnimalId == animalId && item.Ativo && item.Principal,
            cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: TipoValorIndex or OutroDescricaoTipoValorIndex or PrincipalAtivaIndex
        } postgres)
        {
            var failed = exception.Entries.Select(entry => entry.Entity).OfType<IdentificacaoAnimal>().FirstOrDefault();
            if (postgres.ConstraintName == PrincipalAtivaIndex)
                throw new IdentificacaoAnimalPrincipalConflictException(
                    IdentificacaoAnimalPrincipalConflictSource.PersistedNamedPrincipalAtivaUniqueConstraint, exception);
            if (failed is not null)
                throw new IdentificacaoAnimalDuplicateException(failed.Tipo, failed.DescricaoTipo, failed.Valor,
                    postgres.ConstraintName == TipoValorIndex
                        ? IdentificacaoAnimalDuplicateConflictSource.PersistedNamedTipoValorUniqueConstraint
                        : IdentificacaoAnimalDuplicateConflictSource.PersistedNamedOutroDescricaoTipoValorUniqueConstraint,
                    exception);
            throw;
        }
    }

    private static IQueryable<IdentificacaoAnimal> ApplyFilters(IQueryable<IdentificacaoAnimal> items,
        IdentificacaoAnimalListQuery query)
    {
        if (query.Tipo is { } tipo) items = items.Where(item => item.Tipo == tipo);
        if (query.Ativo is { } ativo) items = items.Where(item => item.Ativo == ativo);
        if (query.Principal is { } principal) items = items.Where(item => item.Principal == principal);
        if (!string.IsNullOrWhiteSpace(query.Valor))
        {
            var pattern = $"%{EscapeLikePattern(query.Valor.Trim())}%";
            items = items.Where(item => EF.Functions.ILike(item.Valor, pattern, "\\"));
        }
        return items;
    }

    private static IOrderedQueryable<IdentificacaoAnimal> ApplyOrdering(IQueryable<IdentificacaoAnimal> items) =>
        items.OrderByDescending(item => item.CreatedAtUtc).ThenByDescending(item => item.Id);

    private static IQueryable<IdentificacaoAnimalResult> ReadModels(IQueryable<IdentificacaoAnimal> items) =>
        items.Select(item => ToResult(item));

    private static IdentificacaoAnimalResult ToResult(IdentificacaoAnimal item) => new(item.Id, item.AnimalId, item.Tipo,
        item.DescricaoTipo, item.Valor, item.Principal, item.DataAplicacao, item.Observacao, item.Ativo,
        item.CreatedAtUtc, item.UpdatedAtUtc);

    private static int TotalPages(int totalItems, int pageSize) => totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);

    private static string EscapeLikePattern(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal)
        .Replace("_", "\\_", StringComparison.Ordinal);

    private sealed class MutationScope(IDbContextTransaction transaction) : IIdentificacaoAnimalMutationScope
    {
        private bool _committed;

        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            await transaction.CommitAsync(cancellationToken);
            _committed = true;
        }

        public async ValueTask DisposeAsync()
        {
            if (!_committed) await transaction.RollbackAsync();
            await transaction.DisposeAsync();
        }
    }
}
