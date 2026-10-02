using GenSW.Application.Images;
using GenSW.Domain.Animals;
namespace GenSW.Application.Animals.Tree;

public sealed record TreeNode(Guid AnimalId, string CodigoInterno, string? Nome, SexoAnimal Sexo, bool Ativo,
    DateOnly? DataNascimento, ImageSummary? Imagem, string OrigemImagem, bool PaiConhecido, bool MaeConhecida,
    bool TemAscendentesAdicionais, bool TemDescendentesAdicionais);
public sealed record TreeEdge(Guid FiliacaoId, Guid ProgenitorId, Guid DescendenteId, TipoFiliacaoAnimal TipoFiliacao);
public sealed record TreeLimits(int Ascendentes, int Descendentes, int MaxNos = 100, int MaxArestas = 200);
public sealed record TreeResult(Guid RaizId, IReadOnlyList<TreeNode> Nos, IReadOnlyList<TreeEdge> Arestas,
    TreeLimits Limites, bool Truncada, IReadOnlyList<string> Avisos);
public sealed record TreeRelationsResult(Guid RaizId, IReadOnlyList<TreeNode> Nos, IReadOnlyList<TreeEdge> Arestas,
    int Page, int PageSize, int TotalItems, int TotalPages, IReadOnlyList<string> Avisos);
public interface IAnimalTreeQuery
{
    Task<TreeResult> GetAsync(Guid root, int ancestors, int descendants, CancellationToken ct);
    Task<TreeRelationsResult> RelationsAsync(Guid root, string direction, int page, int pageSize, CancellationToken ct);
}
