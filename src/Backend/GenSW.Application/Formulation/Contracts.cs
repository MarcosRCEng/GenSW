using GenSW.Domain.Catalog;
using GenSW.Domain.Formulation;

namespace GenSW.Application.Formulation;

public sealed class FormulationException(int status, string code, string message) : Exception(message)
{ public int Status { get; } = status; public string Code { get; } = code; }
public sealed record CatalogQuery(int Page = 1, int PageSize = 25, string? Search = null, bool? Ativo = null,
    string SortBy = "nome", string SortDirection = "asc", string? Classe = null, Guid? CategoriaId = null,
    string? Capacidade = null, string? Estado = null)
{
    public void Validate()
    {
        if (Page < 1 || PageSize is < 1 or > 100 || (long)(Page - 1) * PageSize > int.MaxValue || Search?.Length > 200)
            throw new ArgumentException("Paginação/busca inválida.");
        if (SortBy is not ("nome" or "codigo" or "createdAtUtc") || SortDirection is not ("asc" or "desc")) throw new ArgumentException("Ordenação inválida.");
        if (Capacidade is not (null or "entrada" or "producao" or "interno" or "venda")) throw new ArgumentException("Capacidade inválida.");
        if (Estado is not (null or "Rascunho" or "Publicado" or "Inativo")) throw new ArgumentException("Estado inválido.");
    }
}
public sealed record CatalogPage<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalItems, int TotalPages);
public sealed record ProfileView(Guid Id, Guid ItemId, int Numero, int Revisao, string Estado, Guid AutorId,
    DateTimeOffset CreatedAtUtc, DateTimeOffset? PublishedAtUtc, ProfileData Conteudo);
public sealed record RecipeVersionView(Guid Id, Guid ReceitaId, int Numero, int Revisao, string Estado, Guid AutorId,
    DateTimeOffset CreatedAtUtc, DateTimeOffset? PublishedAtUtc, RecipeData Conteudo);
public sealed record ConversionView(Guid Id, Guid ItemId, int Numero, string Origem, string Destino, string Fator,
    string Fonte, string Metodo, DateOnly DataFonte, string Contexto, string ReferenciaAmostra, string Proveniencia);
public sealed record ItemCommand(ItemData Conteudo, int VersaoEsperada = 0);
public sealed record ProfileCommand(ProfileData Conteudo, int VersaoEsperada = 0);
public sealed record RecipeCommand(RecipeHeader Conteudo, int VersaoEsperada = 0);
public sealed record RecipeVersionCommand(RecipeData Conteudo, int VersaoEsperada = 0);
public sealed record StatusCommand(bool Ativo, int VersaoEsperada);
public sealed record VersionCommand(int VersaoEsperada);
public sealed record CategoryCommand(string Nome, bool Ativo = true, int VersaoEsperada = 0);
public sealed record SimulationCommand(Guid ReceitaVersaoId, string Tamanho, string Unidade, Guid? EspecieId,
    string? Fase, IReadOnlyList<NutritionGoal> Metas, RecipeData? Variacao = null, Guid? AnteriorId = null);
public sealed record SimulationData(SimulationCommand Pedido, RecipeVersionView Receita, RecipeHeader Cabecalho,
    RecipeData Escalonada, IReadOnlyList<ResolvedIngredient> Linhas, IReadOnlyList<RecipeVersionView> Dependencias,
    IReadOnlyList<ConversionView> Conversoes, NutritionResult Resultado, string? RendimentoPercentual,
    string? BalancoKg, IReadOnlyList<string> Problemas);
public sealed record SimulationView(Guid Id, Guid AutorId, DateTimeOffset CreatedAtUtc, SimulationData Conteudo);
public sealed record ComparisonCommand(IReadOnlyList<Guid> SimulacaoIds, string Base = "BN");
public sealed record ComparisonCell(Guid SimulacaoId, string? Valor, string Estado, bool Estimado);
public sealed record ComparisonRow(string Componente, string Metodo, string Contexto, Guid? EspecieId, string? Fase,
    string Unidade, bool Comparavel, IReadOnlyList<ComparisonCell> Valores);
public sealed record ComparisonData(ComparisonCommand Pedido, IReadOnlyList<SimulationView> Simulacoes,
    IReadOnlyList<ComparisonRow> Linhas);
public sealed record ComparisonView(Guid Id, DateTimeOffset CreatedAtUtc, ComparisonData Conteudo);
public sealed record SnapshotSummary(Guid Id, string Tipo, DateTimeOffset CreatedAtUtc);

public interface IFormulationMutation : IAsyncDisposable { Task CommitAsync(CancellationToken ct); }
public interface IFormulationRepository
{
    Task<IFormulationMutation> BeginAsync(CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
    void Add(Item value); void Add(CategoriaItem value); void Add(ConversaoItem value); void Add(CatalogAudit value);
    void Add(NutritionProfile value); void Add(Recipe value); void Add(RecipeVersion value);
    void Add(RecipeReference value); void Add(FormulationSnapshot value);
    Task<Item?> ItemAsync(Guid id, bool tracking, CancellationToken ct);
    Task<CategoriaItem?> CategoryAsync(Guid id, bool tracking, CancellationToken ct);
    Task<ConversaoItem?> ConversionAsync(Guid id, CancellationToken ct);
    Task<NutritionProfile?> ProfileAsync(Guid id, bool tracking, CancellationToken ct);
    Task<Recipe?> RecipeAsync(Guid id, bool tracking, CancellationToken ct);
    Task<RecipeVersion?> VersionAsync(Guid id, bool tracking, CancellationToken ct);
    Task<bool> SpeciesExistsAsync(Guid id, CancellationToken ct);
    Task<int> NextConversionAsync(Guid item, CancellationToken ct);
    Task<int> NextProfileAsync(Guid item, CancellationToken ct);
    Task<int> NextVersionAsync(Guid recipe, CancellationToken ct);
    Task<CatalogPage<Item>> ItemsAsync(CatalogQuery q, CancellationToken ct);
    Task<CatalogPage<CategoriaItem>> CategoriesAsync(CatalogQuery q, CancellationToken ct);
    Task<CatalogPage<ConversaoItem>> ConversionsAsync(Guid item, CatalogQuery q, CancellationToken ct);
    Task<CatalogPage<NutritionProfile>> ProfilesAsync(Guid item, CatalogQuery q, CancellationToken ct);
    Task<CatalogPage<Recipe>> RecipesAsync(CatalogQuery q, CancellationToken ct);
    Task<CatalogPage<RecipeVersion>> VersionsAsync(Guid? recipe, CatalogQuery q, CancellationToken ct);
    Task<CatalogPage<CatalogAudit>> HistoryAsync(Guid id, CatalogQuery q, CancellationToken ct);
    Task<FormulationSnapshot?> SnapshotAsync(Guid id, CancellationToken ct);
    Task<FormulationSnapshot?> ReplayAsync(Guid actor, string type, string key, CancellationToken ct);
    Task<CatalogPage<SnapshotSummary>> SnapshotsAsync(string type, CatalogQuery q, CancellationToken ct);
}
