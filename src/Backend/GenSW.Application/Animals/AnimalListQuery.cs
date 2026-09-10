using GenSW.Domain.Animals;

namespace GenSW.Application.Animals;

public sealed record AnimalListQuery(
    int Page = 1, int PageSize = 25, string? Search = null,
    Guid? EspecieId = null, Guid? RacaId = null, Guid? VariedadeId = null,
    SexoAnimal? Sexo = null, EscopoAnimal? Escopo = null, bool? Ativo = null,
    AnimalSortField SortBy = AnimalSortField.CodigoInterno, bool SortDescending = false);
