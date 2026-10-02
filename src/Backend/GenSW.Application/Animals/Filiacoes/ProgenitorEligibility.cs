using System.Linq.Expressions;
using GenSW.Domain.Animals;
namespace GenSW.Application.Animals.Filiacoes;

public static class ProgenitorEligibility
{
    public static Expression<Func<Animal, bool>> Predicate(Animal child, TipoFiliacaoAnimal type)
    {
        if (!Enum.IsDefined(type)) throw new ArgumentException("Tipo de filiação inválido.");
        var sex = type == TipoFiliacaoAnimal.Pai ? SexoAnimal.Macho : SexoAnimal.Femea;
        return parent => parent.Id != child.Id && parent.Ativo && parent.EspecieId == child.EspecieId &&
            (!child.RacaId.HasValue || parent.RacaId == child.RacaId) && parent.Sexo == sex;
    }
}

public sealed record ProgenitorSummary(Guid Id, string CodigoInterno, string? Nome, bool Ativo);
public sealed record ProgenitorCandidate(Guid Id, string CodigoInterno, string? Nome, SexoAnimal Sexo, Guid EspecieId, Guid? RacaId, bool Ativo);
public interface IProgenitorQuery
{
    Task<AnimalEvolutionPage<ProgenitorCandidate>> SearchAsync(Guid animalId, TipoFiliacaoAnimal type, string? search, int page, int pageSize, CancellationToken ct);
}
