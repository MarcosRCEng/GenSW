using GenSW.Domain.Animals;
using GenSW.Application.Animals.Cruzamentos;

namespace GenSW.Application.Animals.Proles;

public sealed record ProleCommand(TipoRegistroProle TipoRegistro, int Quantidade, TipoOrigemProle Origem, DateOnly Data, decimal? PesoGramas, SexoAnimal Sexo, string Condicao, string? Observacao);
public sealed record ProleUpdateCommand(TipoOrigemProle Origem, DateOnly Data, decimal? PesoGramas, SexoAnimal Sexo, string Condicao, string? Observacao);
public sealed record ProleListQuery(int Page = 1, int PageSize = 25, Guid? CicloReprodutivoId = null, TipoRegistroProle? TipoRegistro = null, TipoOrigemProle? Origem = null, bool? Convertida = null);
public sealed record ProleResult(Guid Id, Guid CicloReprodutivoId, Guid? LoteOrigemId, TipoRegistroProle TipoRegistro, int Quantidade, int QuantidadeDesdobrada, TipoOrigemProle Origem, DateOnly Data, decimal? PesoGramas, SexoAnimal Sexo, string Condicao, string? Observacao, Guid? AnimalId, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
public sealed record PagedProleResult(IReadOnlyList<ProleResult> Items, int Page, int PageSize, int TotalItems, int TotalPages);
public sealed record ProleConversaoCommand(string? CodigoInterno, string? Nome, Guid EspecieId, Guid? RacaId, Guid? VariedadeId, EscopoAnimal Escopo);
public sealed record ProleConversaoResult(ProleResult Prole, AnimalResult Animal, CruzamentoAnimalResumo PaiSugerido, CruzamentoAnimalResumo MaeSugerida);
public sealed class ProleNotFoundException(Guid id) : KeyNotFoundException($"Offspring record {id} was not found.");
public sealed class ProleConflictException(string message) : InvalidOperationException(message);
