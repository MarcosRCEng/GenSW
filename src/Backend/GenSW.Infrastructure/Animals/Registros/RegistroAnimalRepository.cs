using GenSW.Application.Animals.Registros;
using GenSW.Domain.Animals;
using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace GenSW.Infrastructure.Animals.Registros;

public sealed class RegistroAnimalRepository(GenSWDbContext context) : IRegistroAnimalRepository
{
    private const string DuplicateIndex = "UX_RegistrosAnimal_TipoRegistro_NumeroRegistro_CaseInsensitive";
    private const string ActiveTypeIndex = "UX_RegistrosAnimal_Animal_TipoRegistro_Ativo";

    public async Task<IRegistroAnimalMutationScope> BeginMutationAsync(CancellationToken cancellationToken = default) =>
        new MutationScope(await context.Database.BeginTransactionAsync(cancellationToken));
    public Task<Animal?> LockAnimalAsync(Guid animalId, CancellationToken cancellationToken = default) =>
        context.Animais.FromSqlInterpolated($"SELECT * FROM \"Animais\" WHERE \"Id\" = {animalId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
    public Task<bool> AnimalExistsAsync(Guid animalId, CancellationToken cancellationToken = default) =>
        context.Animais.AsNoTracking().AnyAsync(item => item.Id == animalId, cancellationToken);
    public Task AddAsync(RegistroAnimal registro, CancellationToken cancellationToken = default) => context.RegistrosAnimal.AddAsync(registro, cancellationToken).AsTask();
    public Task<RegistroAnimal?> GetForUpdateAsync(Guid animalId, Guid registroId, CancellationToken cancellationToken = default) =>
        context.RegistrosAnimal.SingleOrDefaultAsync(item => item.AnimalId == animalId && item.Id == registroId, cancellationToken);
    public async Task<IReadOnlyList<RegistroAnimalResult>> ListByAnimalAsync(Guid animalId, CancellationToken cancellationToken = default) =>
        await context.RegistrosAnimal.AsNoTracking().Where(item => item.AnimalId == animalId).OrderByDescending(item => item.DataInicio).ThenByDescending(item => item.CreatedAtUtc)
            .Select(item => ToResult(item)).ToListAsync(cancellationToken);
    public Task<bool> HasDuplicateAsync(TipoRegistroAnimal tipoRegistro, string numeroRegistro, CancellationToken cancellationToken = default) =>
        context.RegistrosAnimal.AsNoTracking().AnyAsync(item => item.TipoRegistro == tipoRegistro && EF.Functions.ILike(item.NumeroRegistro, numeroRegistro, "\\"), cancellationToken);
    public Task<bool> HasActiveTypeAsync(Guid animalId, TipoRegistroAnimal tipoRegistro, CancellationToken cancellationToken = default) =>
        context.RegistrosAnimal.AsNoTracking().AnyAsync(item => item.AnimalId == animalId && item.TipoRegistro == tipoRegistro && item.Ativo, cancellationToken);
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try { await context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: var name })
        {
            var failed = exception.Entries.Select(entry => entry.Entity).OfType<RegistroAnimal>().FirstOrDefault();
            if (failed is not null && name == DuplicateIndex) throw new RegistroAnimalDuplicateException(failed.TipoRegistro, failed.NumeroRegistro);
            if (failed is not null && name == ActiveTypeIndex) throw new RegistroAnimalActiveTypeConflictException(failed.AnimalId, failed.TipoRegistro);
            throw;
        }
    }
    private static RegistroAnimalResult ToResult(RegistroAnimal item) => new(item.Id, item.AnimalId, item.TipoRegistro, item.NumeroRegistro, item.Ativo, item.DataInicio, item.DataFim, item.CreatedAtUtc, item.UpdatedAtUtc);
    private sealed class MutationScope(IDbContextTransaction transaction) : IRegistroAnimalMutationScope
    { private bool committed; public async Task CommitAsync(CancellationToken cancellationToken = default) { await transaction.CommitAsync(cancellationToken); committed = true; }
      public async ValueTask DisposeAsync() { if (!committed) await transaction.RollbackAsync(); await transaction.DisposeAsync(); } }
}
