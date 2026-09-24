using GenSW.Domain.Animals;

namespace GenSW.Application.Animals.Identificacoes;

public sealed record IdentificacaoAnimalListQuery(
    int Page = 1, int PageSize = 25, TipoIdentificacaoAnimal? Tipo = null,
    string? Valor = null, bool? Ativo = null, bool? Principal = null);
