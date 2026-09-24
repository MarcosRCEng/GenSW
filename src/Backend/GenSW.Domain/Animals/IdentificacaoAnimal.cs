namespace GenSW.Domain.Animals;

public sealed class IdentificacaoAnimal
{
    private IdentificacaoAnimal() { Valor = null!; }

    private IdentificacaoAnimal(Guid animalId, TipoIdentificacaoAnimal tipo, string? descricaoTipo,
        string valor, bool principal, DateOnly? dataAplicacao, string? observacao, DateTimeOffset nowUtc)
    {
        Id = Guid.NewGuid(); AnimalId = animalId; Tipo = tipo; DescricaoTipo = descricaoTipo; Valor = valor;
        Principal = principal; DataAplicacao = dataAplicacao; Observacao = observacao; Ativo = true;
        CreatedAtUtc = nowUtc; UpdatedAtUtc = nowUtc;
    }

    public Guid Id { get; private set; }
    public Guid AnimalId { get; private set; }
    public TipoIdentificacaoAnimal Tipo { get; private set; }
    public string? DescricaoTipo { get; private set; }
    public string Valor { get; private set; }
    public bool Principal { get; private set; }
    public DateOnly? DataAplicacao { get; private set; }
    public string? Observacao { get; private set; }
    public bool Ativo { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static IdentificacaoAnimal Criar(Guid animalId, TipoIdentificacaoAnimal tipo, string? descricaoTipo,
        string valor, bool principal, DateOnly? dataAplicacao, string? observacao, DateTimeOffset nowUtc)
    {
        if (animalId == Guid.Empty) throw new ArgumentException("Animal is required.", nameof(animalId));
        if (!Enum.IsDefined(tipo)) throw new ArgumentException("Identification type is invalid.", nameof(tipo));
        var normalizedValor = NormalizeRequired(valor, 128, nameof(valor));
        var normalizedDescricao = NormalizeOptional(descricaoTipo, 100, nameof(descricaoTipo));
        if (tipo == TipoIdentificacaoAnimal.Outro && normalizedDescricao is null)
            throw new ArgumentException("Description is required for Outro.", nameof(descricaoTipo));
        if (tipo != TipoIdentificacaoAnimal.Outro && normalizedDescricao is not null)
            throw new ArgumentException("Description is only allowed for Outro.", nameof(descricaoTipo));
        var normalizedObservacao = NormalizeOptional(observacao, 1000, nameof(observacao));
        return new IdentificacaoAnimal(animalId, tipo, normalizedDescricao, normalizedValor, principal,
            dataAplicacao, normalizedObservacao, nowUtc);
    }

    public void AlterarMetadados(bool hasDataAplicacao, DateOnly? dataAplicacao, bool hasObservacao,
        string? observacao, DateTimeOffset nowUtc)
    {
        var novo = hasObservacao ? NormalizeOptional(observacao, 1000, nameof(observacao)) : Observacao;
        if ((!hasDataAplicacao || DataAplicacao == dataAplicacao) && (!hasObservacao || Observacao == novo)) return;
        if (hasDataAplicacao) DataAplicacao = dataAplicacao;
        if (hasObservacao) Observacao = novo;
        UpdatedAtUtc = nowUtc;
    }

    public void DefinirPrincipal(DateTimeOffset nowUtc)
    {
        if (!Ativo) throw new InvalidOperationException("Inactive identification cannot be principal.");
        if (Principal) return;
        Principal = true; UpdatedAtUtc = nowUtc;
    }
    public void RemoverPrincipal(DateTimeOffset nowUtc) { if (!Principal) return; Principal = false; UpdatedAtUtc = nowUtc; }
    public void Inativar(DateTimeOffset nowUtc)
    {
        if (!Ativo) return;
        Ativo = false; Principal = false; UpdatedAtUtc = nowUtc;
    }
    public void Reativar(DateTimeOffset nowUtc) { if (Ativo) return; Ativo = true; UpdatedAtUtc = nowUtc; }

    private static string NormalizeRequired(string value, int max, string name)
    {
        ArgumentNullException.ThrowIfNull(value, name); var v = value.Trim();
        if (v.Length < 1 || v.Length > max) throw new ArgumentException($"Value must have between 1 and {max} characters.", name);
        return v;
    }
    private static string? NormalizeOptional(string? value, int max, string name)
    {
        if (value is null) return null; var v = value.Trim();
        if (v.Length == 0) return null;
        if (v.Length > max) throw new ArgumentException($"Value cannot exceed {max} characters.", name);
        return v;
    }
}
