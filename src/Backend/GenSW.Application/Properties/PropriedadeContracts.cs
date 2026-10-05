using GenSW.Application.Animals;
using GenSW.Domain.Properties;

namespace GenSW.Application.Properties;

public sealed record PropriedadeCommand(string Nome, string? Localizacao = null, string? Observacao = null);
public sealed record PropriedadeResult(Guid Id, string Nome, string? Localizacao, string? Observacao, bool Ativo,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
public sealed record PropriedadeListQuery(int Page = 1, int PageSize = 25, string? Search = null, bool? Ativo = null,
    string SortBy = "nome", string SortDirection = "asc");
public sealed record PropriedadePage(IReadOnlyList<Propriedade> Items, int TotalItems);
public sealed record VinculoAnimalPropriedadeResult(Guid Id, Guid AnimalId, Guid PropriedadeId, string PropriedadeNome,
    bool PropriedadeAtiva, DateOnly DataInicio, DateOnly? DataFim, string? Observacao,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
public sealed record AnimalPropriedadesResult(VinculoAnimalPropriedadeResult? Atual, IReadOnlyList<VinculoAnimalPropriedadeResult> Historico);
public sealed record TransferirAnimalCommand(Guid PropriedadeId, DateOnly DataInicio, Guid? VinculoAtualIdEsperado,
    string? Observacao = null);
public sealed record DesvincularAnimalCommand(DateOnly DataFim, Guid VinculoAtualIdEsperado);

public interface IPropriedadeRepository
{
    Task AddAsync(Propriedade item, CancellationToken ct);
    Task<Propriedade?> GetAsync(Guid id, bool tracking, CancellationToken ct);
    Task<PropriedadePage> ListAsync(PropriedadeListQuery query, CancellationToken ct);
    Task<bool> NomeExistsAsync(string nome, Guid? excludingId, CancellationToken ct);
    Task<IAnimalMutationScope> BeginMutationAsync(Guid? animalId, CancellationToken ct);
    Task<Propriedade?> LockPropriedadeAsync(Guid id, CancellationToken ct);
    Task<bool> AnimalExistsAsync(Guid id, CancellationToken ct);
    Task<VinculoAnimalPropriedade?> GetCurrentAsync(Guid animalId, CancellationToken ct);
    Task<DateOnly?> GetLastEndAsync(Guid animalId, CancellationToken ct);
    Task<IReadOnlyList<VinculoAnimalPropriedadeResult>> HistoryAsync(Guid animalId, CancellationToken ct);
    Task AddVinculoAsync(VinculoAnimalPropriedade item, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
}
