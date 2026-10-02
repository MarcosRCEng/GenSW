using GenSW.Application.Animals;
using GenSW.Application.Animals.Filiacoes;
using GenSW.Application.Animals.Pesagens;
using GenSW.Application.Animals.Tree;
using GenSW.Application.Images;
using GenSW.Domain.Animals;

namespace GenSW.API.Contracts.AnimalEvolution;

public sealed record EvolutionPageResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalItems, int TotalPages);
public sealed record PesagemResponse(Guid Id, Guid AnimalId, DateOnly DataMedicao, decimal PesoGramas,
    TipoMarcoPesagem TipoMarco, string? DescricaoMarco, int? IdadeReferenciaDias, int? IdadeDiasNaMedicao,
    string? Observacao, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
public sealed record ImageResponse(Guid Id, Guid? AnimalId, Guid? VariedadeId, string? Legenda, DateOnly? DataCaptura,
    int Ordem, bool Ativa, bool Representativa, string Mime, int Largura, int Altura, long TamanhoBytes,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, string ThumbnailPath, string ConteudoPath);
public sealed record ImageSummaryResponse(Guid Id, string? Legenda, string ThumbnailPath);
public sealed record PreferredImageResponse(string Origem, ImageSummaryResponse? Imagem);
public sealed record ProgenitorCandidateResponse(Guid Id, string CodigoInterno, string? Nome, SexoAnimal Sexo, Guid EspecieId, Guid? RacaId, bool Ativo);
public sealed record TreeNodeResponse(Guid AnimalId, string CodigoInterno, string? Nome, SexoAnimal Sexo, bool Ativo,
    DateOnly? DataNascimento, ImageSummaryResponse? Imagem, string OrigemImagem, bool PaiConhecido, bool MaeConhecida,
    bool TemAscendentesAdicionais, bool TemDescendentesAdicionais);
public sealed record TreeEdgeResponse(Guid FiliacaoId, Guid ProgenitorId, Guid DescendenteId, TipoFiliacaoAnimal TipoFiliacao);
public sealed record TreeLimitsResponse(int Ascendentes, int Descendentes, int MaxNos, int MaxArestas);
public sealed record TreeResponse(Guid RaizId, IReadOnlyList<TreeNodeResponse> Nos, IReadOnlyList<TreeEdgeResponse> Arestas,
    TreeLimitsResponse Limites, bool Truncada, IReadOnlyList<string> Avisos);
public sealed record TreeRelationsResponse(Guid RaizId, IReadOnlyList<TreeNodeResponse> Nos, IReadOnlyList<TreeEdgeResponse> Arestas,
    int Page, int PageSize, int TotalItems, int TotalPages, IReadOnlyList<string> Avisos);

internal static class EvolutionResponse
{
    internal static PesagemResponse Map(PesagemResult x) => new(x.Id,x.AnimalId,x.DataMedicao,x.PesoGramas,x.TipoMarco,x.DescricaoMarco,x.IdadeReferenciaDias,x.IdadeDiasNaMedicao,x.Observacao,x.CreatedAtUtc,x.UpdatedAtUtc);
    internal static ImageResponse Map(ImageResult x) => new(x.Id,x.AnimalId,x.VariedadeId,x.Legenda,x.DataCaptura,x.Ordem,x.Ativa,x.Representativa,x.Mime,x.Largura,x.Altura,x.TamanhoBytes,x.CreatedAtUtc,x.UpdatedAtUtc,x.ThumbnailPath,x.ConteudoPath);
    private static ImageSummaryResponse? Map(ImageSummary? x) => x is null ? null : new(x.Id,x.Legenda,x.ThumbnailPath);
    internal static PreferredImageResponse Map(PreferredImage x) => new(x.Origem,Map(x.Imagem));
    private static ProgenitorCandidateResponse Map(ProgenitorCandidate x) => new(x.Id,x.CodigoInterno,x.Nome,x.Sexo,x.EspecieId,x.RacaId,x.Ativo);
    private static TreeNodeResponse Map(TreeNode x) => new(x.AnimalId,x.CodigoInterno,x.Nome,x.Sexo,x.Ativo,x.DataNascimento,Map(x.Imagem),x.OrigemImagem,x.PaiConhecido,x.MaeConhecida,x.TemAscendentesAdicionais,x.TemDescendentesAdicionais);
    private static TreeEdgeResponse Map(TreeEdge x) => new(x.FiliacaoId,x.ProgenitorId,x.DescendenteId,x.TipoFiliacao);
    internal static TreeResponse Map(TreeResult x) => new(x.RaizId,x.Nos.Select(Map).ToArray(),x.Arestas.Select(Map).ToArray(),new(x.Limites.Ascendentes,x.Limites.Descendentes,x.Limites.MaxNos,x.Limites.MaxArestas),x.Truncada,x.Avisos);
    internal static TreeRelationsResponse Map(TreeRelationsResult x) => new(x.RaizId,x.Nos.Select(Map).ToArray(),x.Arestas.Select(Map).ToArray(),x.Page,x.PageSize,x.TotalItems,x.TotalPages,x.Avisos);
    private static EvolutionPageResponse<TResponse> Page<T,TResponse>(AnimalEvolutionPage<T> x, Func<T,TResponse> map) => new(x.Items.Select(map).ToArray(),x.Page,x.PageSize,x.TotalItems,x.TotalPages);
    internal static EvolutionPageResponse<PesagemResponse> Map(AnimalEvolutionPage<PesagemResult> x) => Page(x,Map);
    internal static EvolutionPageResponse<ImageResponse> Map(AnimalEvolutionPage<ImageResult> x) => Page(x,Map);
    internal static EvolutionPageResponse<ProgenitorCandidateResponse> Map(AnimalEvolutionPage<ProgenitorCandidate> x) => Page(x,Map);
}
