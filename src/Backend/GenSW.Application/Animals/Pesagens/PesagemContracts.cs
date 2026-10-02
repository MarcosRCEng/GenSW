using GenSW.Domain.Animals;

namespace GenSW.Application.Animals.Pesagens;

public sealed record PesagemCommand(DateOnly DataMedicao, decimal PesoGramas, TipoMarcoPesagem TipoMarco,
    string? DescricaoMarco, int? IdadeReferenciaDias, string? Observacao);
public sealed record PesagemQuery(int Page = 1, int PageSize = 25, DateOnly? DataInicial = null,
    DateOnly? DataFinal = null, TipoMarcoPesagem? TipoMarco = null);
public sealed record PesagemResult(Guid Id, Guid AnimalId, DateOnly DataMedicao, decimal PesoGramas,
    TipoMarcoPesagem TipoMarco, string? DescricaoMarco, int? IdadeReferenciaDias, int? IdadeDiasNaMedicao,
    string? Observacao, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
public interface IPesagemRepository
{
    Task<IAnimalMutationScope> BeginAsync(Guid animalId, CancellationToken ct);
    Task<Animal?> AnimalAsync(Guid id, CancellationToken ct);
    Task<PesagemAnimal?> GetAsync(Guid animalId, Guid id, CancellationToken ct);
    Task<(IReadOnlyList<PesagemAnimal> Items, int Total)> ListAsync(Guid animalId, PesagemQuery query, CancellationToken ct);
    Task AddAsync(PesagemAnimal item, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
}
