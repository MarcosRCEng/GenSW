using GenSW.Application.Animals;
using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace GenSW.Infrastructure.Animals;

internal sealed class AnimalMutationScope(IDbContextTransaction transaction) : IAnimalMutationScope
{
    public Task CommitAsync(CancellationToken ct = default) => transaction.CommitAsync(ct);
    public ValueTask DisposeAsync() => transaction.DisposeAsync();

    internal static async Task<AnimalMutationScope> BeginAsync(GenSWDbContext context, Guid? animalId, bool genealogy, CancellationToken ct)
    {
        var tx = await context.Database.BeginTransactionAsync(ct);
        try
        {
            await context.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '5s'", ct);
            if (genealogy)
                await context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(364, 368)", ct);
            if (animalId is { } id)
                await context.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Animais\" WHERE \"Id\" = {id} FOR UPDATE", ct);
            return new(tx);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.LockNotAvailable)
        {
            await tx.DisposeAsync();
            throw new AnimalEvolutionException(409, "conflito_transitorio", "Outra operação está em andamento. Atualize e tente novamente.");
        }
        catch { await tx.DisposeAsync(); throw; }
    }
}
