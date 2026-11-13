using GenSW.Application.Formulation;
using GenSW.Domain.Catalog;
using GenSW.Domain.Formulation;
using GenSW.Domain.Optimization;

namespace GenSW.Application.Optimization;

public sealed record OptimizationCommand(
    string VersaoModelo, string Modo, bool Disponibilidade, string MassaKg,
    IReadOnlyList<OptimizationCandidateCommand> Candidatos, IReadOnlyList<OptimizationGoalCommand> Metas,
    string? ReferenciaDescricao = null, Guid? ReceitaId = null, int? ReceitaRevisao = null,
    Guid? ReceitaVersaoId = null, int? ReceitaVersaoRevisao = null,
    Guid? AnteriorId = null, Guid? EspecieId = null, string? Fase = null, string? Preparo = null);

public sealed record OptimizationCandidateCommand(Guid Id, Guid ItemId, int ItemRevisao,
    Guid? PerfilId, int? PerfilRevisao, string ReferenciaKg,
    string? InclusaoMinima = null, string? InclusaoMaxima = null, string? Justificativa = null);

public sealed record OptimizationGoalCommand(Guid Id, string Componente, string Metodo, string Contexto,
    string Base, string Unidade, string? Minimo, string? Maximo, string Origem,
    string? Fonte = null, bool AceitarEstimados = false, string? Justificativa = null);

public sealed record OptimizationDiagnostic(OptimizationOutcome Desfecho, string Codigo, string Mensagem,
    Guid? CandidatoId = null, Guid? MetaId = null);

public sealed record OptimizationCoefficient(Guid MetaId, string? ValorNormalizado, string? Unidade, string Estado,
    string? Codigo = null, string? Fonte = null, string? Origem = null, string? Hipotese = null,
    NutritionValue? Observacao = null, NutritionValue? MateriaSeca = null, string? FracaoSeca = null,
    string? ValorExato = null);

public sealed record OptimizationCandidateSnapshot(Guid Id, Guid ItemId, string Nome, bool Excluido,
    int ItemRevisao, bool ItemAtivo, ItemData Item, ProfileView? Perfil,
    IReadOnlyList<OptimizationCoefficient> Coeficientes);

public sealed record OptimizationRecipeSnapshot(Guid Id, int Revisao, bool Ativo, RecipeHeader Cabecalho);
public sealed record OptimizationReferenceOutputSnapshot(Guid Id, int Revisao, bool Ativo, ItemData Item);

public sealed record OptimizationComposition(string? MassaKg, string? MateriaSecaKg, string? UmidadePercentual,
    IReadOnlyList<OptimizationComponentSummary> Componentes, IReadOnlyList<string> Avisos);

public sealed record OptimizationComponentSummary(string Componente, string Metodo, string Contexto,
    string Unidade, string Estado, string? ValorBN, string? ValorMS, string? ContribuicaoConhecida,
    string? CoberturaPercentual, bool Estimado, bool EstimadoMS);

public sealed record OptimizationInputs(DateTimeOffset CorteUtc,
    IReadOnlyList<OptimizationCandidateSnapshot> Candidatos, IReadOnlyList<OptimizationGoalCommand> Metas,
    IReadOnlyList<OptimizationDiagnostic> Diagnosticos, OptimizationRecipeSnapshot? Receita = null,
    RecipeVersionView? ReceitaVersao = null, RecipeData? ReceitaEscalonada = null,
    IReadOnlyList<ComponentDefinition>? Definicoes = null, OptimizationComposition? ComposicaoReferencia = null,
    OptimizationReferenceOutputSnapshot? SaidaReferencia = null);

public sealed record OptimizationResultContent(OptimizationOutcome Desfecho, string Codigo,
    string VersaoModelo, string VersaoAplicacao, string HashEntradas, string? HashModelo,
    OptimizationResult? Motor = null, string? Correlacao = null, string? HashModeloNativo = null,
    OptimizationComposition? Composicao = null, string AvaliacaoEconomica = "NaoAvaliado",
    string DisponibilidadeFisica = "NaoConsultada");

public sealed record OptimizationView(Guid Id, Guid AutorId, DateTimeOffset CreatedAtUtc,
    string Estado, int Revisao, OptimizationCommand Pedido, OptimizationInputs Entradas, OptimizationProblem? Modelo,
    OptimizationResultContent? Resultado, DateTimeOffset? FinalizadoEmUtc, string HashPedido, string? HashResultado,
    DateTimeOffset DeadlineUtc);

public sealed record OptimizationSummary(Guid Id, Guid AutorId, DateTimeOffset CreatedAtUtc, string Estado,
    string? Desfecho, Guid? EspecieId, string? Fase);

public sealed record OptimizationQuery(int Page = 1, int PageSize = 25, string SortDirection = "desc",
    string? Desfecho = null, Guid? EspecieId = null, string? Fase = null,
    DateTimeOffset? De = null, DateTimeOffset? Ate = null)
{
    public void Validate()
    {
        if (Page < 1 || PageSize is < 1 or > 100 || (long)(Page - 1) * PageSize > int.MaxValue)
            throw new ArgumentException("Paginação inválida.");
        if (SortDirection is not ("asc" or "desc") || Fase?.Length > 200 || De > Ate)
            throw new ArgumentException("Ordenação, fase ou intervalo de datas inválido.");
        if (Desfecho is not null && !Enum.GetNames<OptimizationOutcome>().Contains(Desfecho, StringComparer.Ordinal))
            throw new ArgumentException("Desfecho inválido.");
    }
}

public sealed record OptimizationDraftDestination(Guid SaidaItemId, int SaidaItemRevisao,
    Guid? ReceitaId = null, int? ReceitaRevisao = null, RecipeHeader? NovaReceita = null);

public sealed record OptimizationDraftPreview(string Hash, RecipeData Conteudo,
    IReadOnlyList<string> Avisos, bool ExigeAceiteResiduos);

public sealed record OptimizationDraftCommand(OptimizationDraftDestination Destino, string HashPrevia,
    bool AceitaResiduos, bool AceitaAvisos);

public sealed record OptimizationDraftReceipt(Guid Id, Guid OtimizacaoId, Guid ReceitaId, Guid VersaoId,
    DateTimeOffset CreatedAtUtc);

public sealed class OptimizationExecution
{
    public Guid Id { get; init; }
    public Guid AutorId { get; init; }
    public string Chave { get; init; } = "";
    public string Hash { get; init; } = "";
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset DeadlineUtc { get; init; }
    public Guid OwnerToken { get; init; }
    public string Estado { get; init; } = "EmCalculo";
    public int Revisao { get; init; } = 1;
    public Guid? EspecieId { get; init; }
    public string? Fase { get; init; }
    public string PedidoJson { get; init; } = "";
    public string EntradasJson { get; init; } = "";
    public string? ModeloJson { get; init; }
}

public sealed class OptimizationResultRow
{
    public Guid ExecutionId { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public string Desfecho { get; init; } = "";
    public string ConteudoJson { get; init; } = "";
}

public sealed class OptimizationDerivation
{
    public Guid Id { get; init; }
    public Guid ExecutionId { get; init; }
    public Guid AutorId { get; init; }
    public string Chave { get; init; } = "";
    public string Hash { get; init; } = "";
    public Guid ReceitaId { get; init; }
    public Guid VersaoId { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public string ConteudoJson { get; init; } = "";
}

public interface IOptimizationRepository
{
    Task<OptimizationExecution?> FindAsync(Guid id, CancellationToken ct);
    Task<OptimizationExecution?> FindByKeyAsync(Guid actor, string key, CancellationToken ct);
    Task<CatalogPage<OptimizationSummary>> ListAsync(OptimizationQuery query, CancellationToken ct);
    void Add(OptimizationExecution execution);
    Task<OptimizationResultRow?> FindResultAsync(Guid executionId, CancellationToken ct);
    Task<bool> TryCompleteAsync(Guid executionId, Guid ownerToken, OptimizationResultRow result, bool expired, CancellationToken ct);
    Task<IReadOnlyList<OptimizationExecution>> FindExpiredAsync(DateTimeOffset now, int limit, CancellationToken ct);
    Task<OptimizationDerivation?> FindDerivationAsync(Guid actor, string key, CancellationToken ct);
    Task<OptimizationDerivation?> FindDerivationByVersionAsync(Guid versionId, CancellationToken ct);
    void Add(OptimizationDerivation derivation);
}
