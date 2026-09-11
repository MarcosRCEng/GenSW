using System.Text.RegularExpressions;

namespace GenSW.Domain.Animals;

public sealed class Animal
{
    private const int CodigoInternoMaxLength = 64;
    private const int NomeMaxLength = 200;
    private static readonly Regex WhitespacePattern = new(
        @"\s+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private Animal()
    {
        CodigoInterno = null!;
    }

    private Animal(
        string codigoInterno, string? nome, Guid especieId, Guid? racaId, Guid? variedadeId,
        SexoAnimal sexo, DateOnly? dataNascimento, EscopoAnimal escopo, DateTimeOffset nowUtc)
    {
        Id = Guid.NewGuid();
        CodigoInterno = codigoInterno;
        Nome = nome;
        EspecieId = especieId;
        RacaId = racaId;
        VariedadeId = variedadeId;
        Sexo = sexo;
        DataNascimento = dataNascimento;
        Escopo = escopo;
        Ativo = true;
        CreatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public Guid Id { get; private set; }
    public string CodigoInterno { get; private set; }
    public string? Nome { get; private set; }
    public Guid EspecieId { get; private set; }
    public Guid? RacaId { get; private set; }
    public Guid? VariedadeId { get; private set; }
    public SexoAnimal Sexo { get; private set; }
    public DateOnly? DataNascimento { get; private set; }
    public EscopoAnimal Escopo { get; private set; }
    public bool Ativo { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static Animal Criar(
        string codigoInterno, string? nome, Guid especieId, Guid? racaId, Guid? variedadeId,
        SexoAnimal sexo, DateOnly? dataNascimento, EscopoAnimal escopo,
        DateOnly utcToday, DateTimeOffset nowUtc)
    {
        ValidateCadastro(
            codigoInterno, nome, especieId, racaId, variedadeId, sexo, dataNascimento, escopo, utcToday,
            out var normalizedCodigoInterno, out var normalizedNome);

        return new Animal(
            normalizedCodigoInterno, normalizedNome, especieId, racaId, variedadeId,
            sexo, dataNascimento, escopo, nowUtc);
    }

    public void AlterarCadastro(
        string codigoInterno, string? nome, Guid especieId, Guid? racaId, Guid? variedadeId,
        SexoAnimal sexo, DateOnly? dataNascimento, EscopoAnimal escopo,
        DateOnly utcToday, DateTimeOffset nowUtc)
    {
        ValidateCadastro(
            codigoInterno, nome, especieId, racaId, variedadeId, sexo, dataNascimento, escopo, utcToday,
            out var normalizedCodigoInterno, out var normalizedNome);

        if (CodigoInterno == normalizedCodigoInterno && Nome == normalizedNome && EspecieId == especieId &&
            RacaId == racaId && VariedadeId == variedadeId && Sexo == sexo &&
            DataNascimento == dataNascimento && Escopo == escopo)
        {
            return;
        }

        CodigoInterno = normalizedCodigoInterno;
        Nome = normalizedNome;
        EspecieId = especieId;
        RacaId = racaId;
        VariedadeId = variedadeId;
        Sexo = sexo;
        DataNascimento = dataNascimento;
        Escopo = escopo;
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

    private static void ValidateCadastro(
        string codigoInterno, string? nome, Guid especieId, Guid? racaId, Guid? variedadeId,
        SexoAnimal sexo, DateOnly? dataNascimento, EscopoAnimal escopo, DateOnly utcToday,
        out string normalizedCodigoInterno, out string? normalizedNome)
    {
        normalizedCodigoInterno = NormalizeRequired(codigoInterno, CodigoInternoMaxLength, nameof(codigoInterno));
        normalizedNome = NormalizeOptional(nome, NomeMaxLength, nameof(nome));

        if (especieId == Guid.Empty)
        {
            throw new ArgumentException("Species is required.", nameof(especieId));
        }

        if (racaId == Guid.Empty)
        {
            throw new ArgumentException("Breed cannot be empty when supplied.", nameof(racaId));
        }

        if (variedadeId == Guid.Empty)
        {
            throw new ArgumentException("Variety cannot be empty when supplied.", nameof(variedadeId));
        }

        if (!Enum.IsDefined(sexo))
        {
            throw new ArgumentException("Sexo is invalid.", nameof(sexo));
        }

        if (!Enum.IsDefined(escopo))
        {
            throw new ArgumentException("Escopo is invalid.", nameof(escopo));
        }

        if (dataNascimento > utcToday)
        {
            throw new ArgumentException("Birth date cannot be in the future.", nameof(dataNascimento));
        }
    }

    private static string NormalizeRequired(string value, int maxLength, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);
        var normalized = WhitespacePattern.Replace(value.Trim(), " ");
        if (normalized.Length < 1 || normalized.Length > maxLength)
        {
            throw new ArgumentException($"Value must have between 1 and {maxLength} characters.", parameterName);
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = WhitespacePattern.Replace(value.Trim(), " ");
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"Value cannot exceed {maxLength} characters.", parameterName);
        }

        return normalized;
    }
}
