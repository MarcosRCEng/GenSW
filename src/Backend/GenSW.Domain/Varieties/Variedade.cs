using System.Text.RegularExpressions;

namespace GenSW.Domain.Varieties;

public sealed class Variedade
{
    private const int NomeMaxLength = 200;
    private static readonly Regex WhitespacePattern = new(
        @"\s+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private Variedade()
    {
        Nome = null!;
    }

    private Variedade(Guid especieId, string nome, DateTimeOffset nowUtc)
    {
        Id = Guid.NewGuid();
        EspecieId = especieId;
        Nome = nome;
        Ativo = true;
        CreatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public Guid Id { get; private set; }

    public Guid EspecieId { get; private set; }

    public string Nome { get; private set; }

    public bool Ativo { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static Variedade Criar(Guid especieId, string nome, DateTimeOffset nowUtc)
        => new(especieId, NormalizeRequiredName(nome, nameof(nome)), nowUtc);

    public void AlterarCadastro(Guid especieId, string nome, DateTimeOffset nowUtc)
    {
        var nomeNormalizado = NormalizeRequiredName(nome, nameof(nome));
        if (EspecieId == especieId && Nome == nomeNormalizado)
        {
            return;
        }

        EspecieId = especieId;
        Nome = nomeNormalizado;
        UpdatedAtUtc = nowUtc;
    }

    public void Inativar(DateTimeOffset nowUtc)
    {
        if (!Ativo)
        {
            return;
        }

        Ativo = false;
        UpdatedAtUtc = nowUtc;
    }

    public void Reativar(DateTimeOffset nowUtc)
    {
        if (Ativo)
        {
            return;
        }

        Ativo = true;
        UpdatedAtUtc = nowUtc;
    }

    private static string NormalizeRequiredName(string value, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);
        var normalized = WhitespacePattern.Replace(value.Trim(), " ");
        if (normalized.Length is < 1 or > NomeMaxLength)
        {
            throw new ArgumentException(
                "The variety name must have between 1 and 200 characters.",
                parameterName);
        }

        return normalized;
    }
}
