using System.Text.RegularExpressions;

namespace GenSW.Domain.Species;

public sealed class Especie
{
    private const int NomeMaxLength = 200;
    private static readonly Regex WhitespacePattern = new(
        @"\s+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private Especie()
    {
        NomeComum = null!;
    }

    private Especie(string nomeComum, string? nomeCientifico, DateTimeOffset nowUtc)
    {
        Id = Guid.NewGuid();
        NomeComum = nomeComum;
        NomeCientifico = nomeCientifico;
        Ativo = true;
        CreatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public Guid Id { get; private set; }

    public string NomeComum { get; private set; }

    public string? NomeCientifico { get; private set; }

    public bool Ativo { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static Especie Criar(string nomeComum, string? nomeCientifico, DateTimeOffset nowUtc)
    {
        var nomeComumNormalizado = NormalizeRequiredName(nomeComum, nameof(nomeComum));
        var nomeCientificoNormalizado = NormalizeOptionalName(nomeCientifico, nameof(nomeCientifico));

        return new Especie(nomeComumNormalizado, nomeCientificoNormalizado, nowUtc);
    }

    public void AlterarCadastro(string nomeComum, string? nomeCientifico, DateTimeOffset nowUtc)
    {
        var nomeComumNormalizado = NormalizeRequiredName(nomeComum, nameof(nomeComum));
        var nomeCientificoNormalizado = NormalizeOptionalName(nomeCientifico, nameof(nomeCientifico));
        if (NomeComum == nomeComumNormalizado && NomeCientifico == nomeCientificoNormalizado)
        {
            return;
        }

        NomeComum = nomeComumNormalizado;
        NomeCientifico = nomeCientificoNormalizado;
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
                "The common name must have between 1 and 200 characters.",
                parameterName);
        }

        return normalized;
    }

    private static string? NormalizeOptionalName(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = WhitespacePattern.Replace(value.Trim(), " ");
        if (normalized.Length > NomeMaxLength)
        {
            throw new ArgumentException(
                "The scientific name cannot exceed 200 characters.",
                parameterName);
        }

        return normalized;
    }
}
