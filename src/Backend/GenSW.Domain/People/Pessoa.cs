namespace GenSW.Domain.People;

public sealed class Pessoa
{
    private const int NomeMaxLength = 200;

    private Pessoa()
    {
        Nome = null!;
    }

    private Pessoa(TipoPessoa tipoPessoa, string nome, string? nomeFantasia, DateTimeOffset nowUtc)
    {
        Id = Guid.NewGuid();
        TipoPessoa = tipoPessoa;
        Nome = nome;
        NomeFantasia = nomeFantasia;
        Ativo = true;
        CreatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public Guid Id { get; private set; }

    public TipoPessoa TipoPessoa { get; private set; }

    public string Nome { get; private set; }

    public string? NomeFantasia { get; private set; }

    public bool Ativo { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static Pessoa Criar(TipoPessoa tipoPessoa, string nome, string? nomeFantasia, DateTimeOffset nowUtc)
    {
        ValidarTipoPessoa(tipoPessoa);
        var nomeNormalizado = NormalizarNome(nome);
        var nomeFantasiaNormalizado = NormalizarNomeFantasia(tipoPessoa, nomeFantasia);

        return new Pessoa(tipoPessoa, nomeNormalizado, nomeFantasiaNormalizado, nowUtc);
    }

    public void AlterarCadastro(string nome, string? nomeFantasia, DateTimeOffset nowUtc)
    {
        if (!Ativo)
        {
            throw new InvalidOperationException("Inactive people cannot have their registration changed.");
        }

        var nomeNormalizado = NormalizarNome(nome);
        var nomeFantasiaNormalizado = NormalizarNomeFantasia(TipoPessoa, nomeFantasia);
        if (Nome == nomeNormalizado && NomeFantasia == nomeFantasiaNormalizado)
        {
            return;
        }

        Nome = nomeNormalizado;
        NomeFantasia = nomeFantasiaNormalizado;
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

    private static void ValidarTipoPessoa(TipoPessoa tipoPessoa)
    {
        if (tipoPessoa is not TipoPessoa.Fisica and not TipoPessoa.Juridica)
        {
            throw new ArgumentOutOfRangeException(nameof(tipoPessoa), tipoPessoa, "The person type is invalid.");
        }
    }

    private static string NormalizarNome(string nome)
    {
        ArgumentNullException.ThrowIfNull(nome);

        var nomeNormalizado = nome.Trim();
        if (nomeNormalizado.Length is < 2 or > NomeMaxLength)
        {
            throw new ArgumentException("The name must have between 2 and 200 characters.", nameof(nome));
        }

        return nomeNormalizado;
    }

    private static string? NormalizarNomeFantasia(TipoPessoa tipoPessoa, string? nomeFantasia)
    {
        if (string.IsNullOrWhiteSpace(nomeFantasia))
        {
            return null;
        }

        if (tipoPessoa != TipoPessoa.Juridica)
        {
            throw new ArgumentException("Only legal persons can have a trade name.", nameof(nomeFantasia));
        }

        var nomeFantasiaNormalizado = nomeFantasia.Trim();
        if (nomeFantasiaNormalizado.Length > NomeMaxLength)
        {
            throw new ArgumentException("The trade name cannot exceed 200 characters.", nameof(nomeFantasia));
        }

        return nomeFantasiaNormalizado;
    }
}
