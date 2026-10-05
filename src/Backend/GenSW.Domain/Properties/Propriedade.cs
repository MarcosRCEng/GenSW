using System.Text.RegularExpressions;

namespace GenSW.Domain.Properties;

public sealed class Propriedade
{
    private Propriedade() { }

    public Guid Id { get; private set; }
    public string Nome { get; private set; } = null!;
    public string NomeNormalizado { get; private set; } = null!;
    public string? Localizacao { get; private set; }
    public string? Observacao { get; private set; }
    public bool Ativo { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static Propriedade Criar(string nome, string? localizacao, string? observacao, DateTimeOffset now)
    {
        var item = new Propriedade { Id = Guid.NewGuid(), Ativo = true, CreatedAtUtc = now };
        item.AlterarCadastro(nome, localizacao, observacao, now);
        return item;
    }

    public void AlterarCadastro(string nome, string? localizacao, string? observacao, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(nome);
        var normalized = Regex.Replace(nome.Trim(), @"\s+", " ");
        if (normalized.Length is < 1 or > 200)
            throw new ArgumentException("O nome deve conter entre 1 e 200 caracteres.", nameof(nome));
        var location = NormalizeOptional(localizacao, 500, nameof(localizacao));
        var note = NormalizeOptional(observacao, 2000, nameof(observacao));
        if (Nome == normalized && Localizacao == location && Observacao == note) return;
        Nome = normalized;
        NomeNormalizado = normalized.ToUpperInvariant();
        Localizacao = location;
        Observacao = note;
        UpdatedAtUtc = now;
    }

    public void AlterarStatus(bool ativo, DateTimeOffset now)
    {
        if (Ativo == ativo) return;
        Ativo = ativo;
        UpdatedAtUtc = now;
    }

    internal static string? NormalizeOptional(string? value, int maxLength, string parameter)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (normalized?.Length > maxLength)
            throw new ArgumentException($"O campo não pode exceder {maxLength} caracteres.", parameter);
        return normalized;
    }
}
