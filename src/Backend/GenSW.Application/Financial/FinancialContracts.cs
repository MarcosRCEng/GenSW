using System.Text.Json.Serialization;
using GenSW.Domain.Financial;

namespace GenSW.Application.Financial;

public sealed record ConfiguracaoCommand([property: JsonRequired] DateOnly DataInicio,
    [property: JsonRequired, JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] decimal SaldoInicial, int? VersaoEsperada = null);
public sealed record CategoriaCommand(string Nome, NaturezaFinanceira Natureza);
public sealed record CategoriaUpdateCommand(string Nome, bool Ativa, int VersaoEsperada);
public sealed record LancamentoCommand(NaturezaFinanceira Tipo, DateOnly DataMovimento,
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] decimal Valor,
    string Descricao, Guid CategoriaId, FormaPagamento FormaPagamento, Guid? PessoaId = null, Guid? AnimalId = null, string? Observacao = null);
public sealed record CorrecaoCommand(LancamentoCommand Lancamento, int VersaoEsperada, string Motivo);
public sealed record CancelamentoCommand(int VersaoEsperada, string Motivo);
public sealed record AjusteCommand(int VersaoEsperada, string Motivo, bool SomenteAnotacao, LancamentoCommand? Substituto = null);
public sealed record FechamentoCommand(int VersaoEsperada, string Observacao,
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] decimal? SaldoConferido = null);
public sealed record LancamentoQuery(int Page = 1, int PageSize = 25, int? Ano = null, int? Mes = null,
    DateOnly? De = null, DateOnly? Ate = null, NaturezaFinanceira? Tipo = null, Guid? CategoriaId = null,
    string? Search = null, bool? Cancelado = null);
public sealed record LancamentoView(LancamentoCaixa Lancamento, string CategoriaNome, string? PessoaNome, string? AnimalNome, bool Revertido);
public sealed record LancamentosPage(IReadOnlyList<LancamentoView> Items, int Page, int PageSize, int TotalItems, int TotalPages);
public sealed record AjusteResult(Guid OriginalId, LancamentoCaixa? Reversao, LancamentoCaixa? Substituto, bool SomenteAnotacao);
public sealed record ResumoCaixa(DateOnly Mes, bool Fechado, int Versao,
    [property: JsonNumberHandling(JsonNumberHandling.WriteAsString)] decimal SaldoAbertura,
    [property: JsonNumberHandling(JsonNumberHandling.WriteAsString)] decimal Receitas,
    [property: JsonNumberHandling(JsonNumberHandling.WriteAsString)] decimal Despesas,
    [property: JsonNumberHandling(JsonNumberHandling.WriteAsString)] decimal Resultado,
    [property: JsonNumberHandling(JsonNumberHandling.WriteAsString)] decimal SaldoFinal,
    int QuantidadeReceitas, int QuantidadeDespesas);

public interface IFinancialRepository
{
    Task<ConfiguracaoCaixa?> ConfiguracaoAsync(CancellationToken ct);
    Task<ConfiguracaoCaixa> ConfigureAsync(ConfiguracaoCommand command, Guid autor, CancellationToken ct);
    Task<IReadOnlyList<CategoriaFinanceira>> CategoriasAsync(CancellationToken ct);
    Task<CategoriaFinanceira> CreateCategoriaAsync(CategoriaCommand command, CancellationToken ct);
    Task<CategoriaFinanceira> UpdateCategoriaAsync(Guid id, CategoriaUpdateCommand command, CancellationToken ct);
    Task<LancamentosPage> ListAsync(LancamentoQuery query, CancellationToken ct);
    Task<LancamentoView> GetAsync(Guid id, CancellationToken ct);
    Task<LancamentoCaixa> CreateAsync(LancamentoCommand command, Guid autor, string chave, CancellationToken ct);
    Task<LancamentoCaixa> UpdateAsync(Guid id, CorrecaoCommand command, Guid autor, CancellationToken ct);
    Task<LancamentoCaixa> CancelAsync(Guid id, CancelamentoCommand command, Guid autor, CancellationToken ct);
    Task<IReadOnlyList<AuditoriaLancamento>> HistoricoAsync(Guid id, CancellationToken ct);
    Task<ResumoCaixa> ResumoAsync(DateOnly mes, CancellationToken ct);
    Task<FechamentoCaixa?> FechamentoAsync(DateOnly mes, CancellationToken ct);
    Task<IReadOnlyList<FechamentoCaixa>> FechamentosAsync(CancellationToken ct);
    Task<FechamentoCaixa> CloseAsync(DateOnly mes, FechamentoCommand command, Guid autor, CancellationToken ct);
    Task<AjusteResult> AdjustAsync(Guid id, AjusteCommand command, Guid autor, string chave, CancellationToken ct);
}
