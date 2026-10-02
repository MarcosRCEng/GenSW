using System.Text.Json.Serialization;

namespace GenSW.Domain.Financial;

public enum NaturezaFinanceira { Receita = 1, Despesa = 2 }
public enum FormaPagamento { Dinheiro = 1, Pix = 2, Transferencia = 3, Cartao = 4, Outro = 5 }
public enum OrigemLancamento { Ordinario = 1, AjusteReversao = 2, AjusteSubstituicao = 3 }

public static class CaixaRules
{
    public const decimal Limite = 9999999999999999.99m;
    public static decimal Dinheiro(decimal valor, bool positivo = false)
    {
        var escala = (decimal.GetBits(valor)[3] >> 16) & 0xff;
        if (valor < -Limite || valor > Limite || escala > 2 || (positivo && valor <= 0))
            throw new ArgumentException("Informe um valor com até duas casas decimais dentro do limite do caixa.");
        return valor;
    }
    public static string Texto(string? valor, int limite, string campo)
    {
        if (string.IsNullOrWhiteSpace(valor) || valor.Trim().Length > limite)
            throw new ArgumentException($"{campo} é obrigatório e admite até {limite} caracteres.");
        return valor.Trim();
    }
    public static string? Opcional(string? valor, int limite) => string.IsNullOrWhiteSpace(valor) ? null : Texto(valor, limite, "Texto");
    public static DateOnly Mes(DateOnly data) => new(data.Year, data.Month, 1);
    public static DateOnly Hoje(TimeProvider clock) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")).DateTime);
    public static void Data(DateOnly data, DateOnly inicio, DateOnly hoje)
    {
        if (data < inicio || data > hoje) throw new ArgumentException("Data fora do período iniciado ou posterior à data operacional de São Paulo.");
    }
    public static void Versao(int atual, int esperada)
    {
        if (esperada < 1) throw new ArgumentException("Versão esperada obrigatória.");
        if (atual != esperada) throw new CaixaConflictException("Registro alterado por outro usuário. Recarregue e confira os dados.");
    }
}
public sealed class CaixaConflictException(string message) : InvalidOperationException(message);
public sealed class CaixaNotFoundException(string message) : InvalidOperationException(message);

public sealed record ConfiguracaoCaixa(int Id, DateOnly DataInicio,
    [property: JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)] decimal SaldoInicial,
    int Versao, Guid AutorId, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
public sealed record CategoriaFinanceira(Guid Id, string Nome, string NomeNormalizado, NaturezaFinanceira Natureza,
    string? Codigo, bool Ativa, int Versao, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
public sealed record LancamentoCaixa(Guid Id, NaturezaFinanceira Tipo, DateOnly DataMovimento,
    [property: JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)] decimal Valor,
    string Descricao, Guid CategoriaId, FormaPagamento FormaPagamento, Guid? PessoaId, Guid? AnimalId,
    string? Observacao, bool Cancelado, int Versao, Guid AutorId, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc,
    OrigemLancamento Origem, Guid? LancamentoOriginalId, string? MotivoAjuste);
public sealed record AuditoriaLancamento(Guid Id, Guid LancamentoId, string Operacao, string? AntesJson,
    string DepoisJson, string? Motivo, Guid AutorId, DateTimeOffset CreatedAtUtc);
public sealed record MesCaixa(DateOnly Mes, bool Fechado, int Versao);
public sealed record FechamentoCaixa(Guid Id, DateOnly Mes,
    [property: JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)] decimal SaldoAbertura,
    [property: JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)] decimal Receitas,
    [property: JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)] decimal Despesas,
    [property: JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)] decimal SaldoFinal,
    int QuantidadeReceitas, int QuantidadeDespesas,
    [property: JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)] decimal? SaldoConferido,
    string Observacao, Guid AutorId, DateTimeOffset CreatedAtUtc);
public sealed record IdempotenciaFinanceira(Guid AutorId, string Operacao, string Chave, string PayloadHash, string RespostaJson, DateTimeOffset CreatedAtUtc);
