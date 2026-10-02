using System.Text.RegularExpressions;

namespace GenSW.Domain.Animals;

public sealed class Prole
{
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private Prole() { }
    public Guid Id { get; private set; }
    public Guid CicloReprodutivoId { get; private set; }
    public Guid? LoteOrigemId { get; private set; }
    public TipoRegistroProle TipoRegistro { get; private set; }
    public int Quantidade { get; private set; }
    public int QuantidadeDesdobrada { get; private set; }
    public TipoOrigemProle Origem { get; private set; }
    public DateOnly Data { get; private set; }
    public decimal? PesoGramas { get; private set; }
    public SexoAnimal Sexo { get; private set; }
    public string Condicao { get; private set; } = null!;
    public string? Observacao { get; private set; }
    public Guid? AnimalId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static Prole Criar(Guid cicloId, TipoRegistroProle tipo, int quantidade, TipoOrigemProle origem, DateOnly data, decimal? peso, SexoAnimal sexo, string condicao, string? observacao, Guid? loteOrigemId, DateTimeOffset now)
    {
        if (cicloId == Guid.Empty) throw new ArgumentException("Cycle is required.", nameof(cicloId));
        if (loteOrigemId == Guid.Empty) throw new ArgumentException("Source batch cannot be empty.", nameof(loteOrigemId));
        Validar(tipo, quantidade, origem, peso, sexo, condicao, observacao, out var normalizedCondicao, out var normalizedObservacao);
        if (loteOrigemId is not null && (tipo != TipoRegistroProle.Individual || quantidade != 1)) throw new ArgumentException("A batch split must create one individual record.");
        return new Prole { Id = Guid.NewGuid(), CicloReprodutivoId = cicloId, LoteOrigemId = loteOrigemId, TipoRegistro = tipo, Quantidade = quantidade, Origem = origem, Data = data, PesoGramas = peso, Sexo = sexo, Condicao = normalizedCondicao, Observacao = normalizedObservacao, CreatedAtUtc = now, UpdatedAtUtc = now };
    }

    public void Atualizar(TipoOrigemProle origem, DateOnly data, decimal? peso, SexoAnimal sexo, string condicao, string? observacao, DateTimeOffset now)
    {
        if (AnimalId is not null) throw new InvalidOperationException("A converted offspring record cannot be edited.");
        Validar(TipoRegistro, Quantidade, origem, peso, sexo, condicao, observacao, out var normalizedCondicao, out var normalizedObservacao);
        Origem = origem; Data = data; PesoGramas = peso; Sexo = sexo; Condicao = normalizedCondicao; Observacao = normalizedObservacao; UpdatedAtUtc = now;
    }

    public void RegistrarDesdobramento(DateTimeOffset now)
    {
        if (TipoRegistro != TipoRegistroProle.Lote || QuantidadeDesdobrada >= Quantidade) throw new InvalidOperationException("This batch has no units available to split.");
        QuantidadeDesdobrada++; UpdatedAtUtc = now;
    }

    public void VincularAnimal(Guid animalId, DateTimeOffset now)
    {
        if (TipoRegistro != TipoRegistroProle.Individual) throw new InvalidOperationException("A batch must be split before conversion.");
        if (animalId == Guid.Empty) throw new ArgumentException("Animal is required.", nameof(animalId));
        if (AnimalId is not null) throw new InvalidOperationException("This offspring record has already been converted to an animal.");
        AnimalId = animalId; UpdatedAtUtc = now;
    }

    private static void Validar(TipoRegistroProle tipo, int quantidade, TipoOrigemProle origem, decimal? peso, SexoAnimal sexo, string condicao, string? observacao, out string normalizedCondicao, out string? normalizedObservacao)
    {
        if (!Enum.IsDefined(tipo) || !Enum.IsDefined(origem) || !Enum.IsDefined(sexo)) throw new ArgumentException("Offspring values are invalid.");
        if (quantidade < 1 || (tipo == TipoRegistroProle.Individual && quantidade != 1)) throw new ArgumentException("An individual record has quantity one and a batch has a positive quantity.");
        if (peso is <= 0) throw new ArgumentException("Weight must be positive when informed.");
        normalizedCondicao = NormalizarObrigatorio(condicao, 200, nameof(condicao));
        normalizedObservacao = NormalizarOpcional(observacao, 2000, nameof(observacao));
    }
    private static string NormalizarObrigatorio(string value, int max, string name) { ArgumentNullException.ThrowIfNull(value, name); var result = Whitespace.Replace(value.Trim(), " "); if (result.Length is < 1 or > 200) throw new ArgumentException("Condition is required and must be valid.", name); return result; }
    private static string? NormalizarOpcional(string? value, int max, string name) { if (string.IsNullOrWhiteSpace(value)) return null; var result = Whitespace.Replace(value.Trim(), " "); if (result.Length > max) throw new ArgumentException("Observation is too long.", name); return result; }
}
