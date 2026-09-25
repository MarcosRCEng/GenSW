using GenSW.Domain.Animals;

namespace GenSW.Application.Animals.Identificacoes;

public sealed record CreateIdentificacaoAnimalCommand(
    TipoIdentificacaoAnimal Tipo, string? DescricaoTipo, string Valor,
    bool Principal, DateOnly? DataAplicacao, string? Observacao);

public sealed record UpdateIdentificacaoAnimalMetadataCommand(
    bool HasDataAplicacao, DateOnly? DataAplicacao,
    bool HasObservacao, string? Observacao);

public sealed record SetIdentificacaoAnimalAtivoCommand(bool Ativo);

public sealed record SetIdentificacaoAnimalPrincipalCommand(bool Principal);
