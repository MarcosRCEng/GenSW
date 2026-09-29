using GenSW.Domain.Animals;

namespace GenSW.Application.Animals.Registros;

public sealed record CreateRegistroAnimalCommand(TipoRegistroAnimal TipoRegistro, string NumeroRegistro, DateOnly DataInicio);
public sealed record InactivateRegistroAnimalCommand(DateOnly DataFim);
