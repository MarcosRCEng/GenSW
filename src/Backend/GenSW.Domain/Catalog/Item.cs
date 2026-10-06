namespace GenSW.Domain.Catalog;

public sealed record ItemData(string Codigo, string Nome, string? Descricao, Guid? CategoriaId,
    string Classe, string Unidade, bool PodeEntrar, bool PodeProduzir, bool UsoInterno, bool Venda);

public sealed class Item
{
    private Item() { }
    public Guid Id { get; private set; }
    public string Codigo { get; private set; } = "";
    public string CodigoNormalizado { get; private set; } = "";
    public string Nome { get; private set; } = "";
    public string? Descricao { get; private set; }
    public Guid? CategoriaId { get; private set; }
    public string Classe { get; private set; } = "";
    public string Unidade { get; private set; } = "";
    public bool PodeEntrar { get; private set; }
    public bool PodeProduzir { get; private set; }
    public bool UsoInterno { get; private set; }
    public bool Venda { get; private set; }
    public bool Ativo { get; private set; } = true;
    public bool UnidadeFixada { get; private set; }
    public int Revisao { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public ItemData Data() => new(Codigo, Nome, Descricao, CategoriaId, Classe, Unidade, PodeEntrar, PodeProduzir, UsoInterno, Venda);
    public static Item Create(ItemData data, DateTimeOffset now)
    {
        var item = new Item { Id = Guid.NewGuid(), CreatedAtUtc = now };
        item.Update(data, 0, now);
        return item;
    }
    public void Update(ItemData data, int expected, DateTimeOffset now)
    {
        CatalogRules.Expected(Revisao, expected);
        if (data.Classe is not ("Alimentar" or "OutroMaterialIncorporado" or "Embalagem" or "Consumivel")) throw new ArgumentException("Classe material inválida.");
        if (data.Unidade is not ("kg" or "L" or "un")) throw new ArgumentException("Unidade canônica deve ser kg, L ou un.");
        if (UnidadeFixada && Unidade != data.Unidade) throw new CatalogConflictException("unidade_fixada", "Unidade utilizada em publicação é imutável. Crie outro item.");
        Codigo = CatalogRules.Text(data.Codigo, 50, "Código"); CodigoNormalizado = Codigo.ToUpperInvariant();
        Nome = CatalogRules.Text(data.Nome, 200, "Nome"); Descricao = CatalogRules.Optional(data.Descricao);
        CategoriaId = data.CategoriaId; Classe = data.Classe; Unidade = data.Unidade;
        PodeEntrar = data.PodeEntrar; PodeProduzir = data.PodeProduzir; UsoInterno = data.UsoInterno; Venda = data.Venda;
        Revisao++; UpdatedAtUtc = now;
    }
    public void SetActive(bool active, int expected, DateTimeOffset now)
    { CatalogRules.Expected(Revisao, expected); Ativo = active; Revisao++; UpdatedAtUtc = now; }
    public void FixUnit() => UnidadeFixada = true;
}

public sealed class CategoriaItem
{
    private CategoriaItem() { }
    public Guid Id { get; private set; }
    public string Nome { get; private set; } = "";
    public string NomeNormalizado { get; private set; } = "";
    public bool Ativo { get; private set; } = true;
    public int Revisao { get; private set; }
    public static CategoriaItem Create(string name)
    {
        var item = new CategoriaItem { Id = Guid.NewGuid() };
        item.Update(name, true, 0); return item;
    }
    public void Update(string name, bool active, int expected)
    { CatalogRules.Expected(Revisao, expected); Nome = CatalogRules.Text(name, 200, "Categoria"); NomeNormalizado = Nome.ToUpperInvariant(); Ativo = active; Revisao++; }
}

public sealed record ConversionData(string Origem, string Destino, string Fator, string Fonte, string Metodo,
    DateOnly DataFonte, string Contexto, string ReferenciaAmostra, string Proveniencia);

public sealed class ConversaoItem
{
    private ConversaoItem() { }
    public Guid Id { get; private set; }
    public Guid ItemId { get; private set; }
    public int Numero { get; private set; }
    public string Origem { get; private set; } = "";
    public string Destino { get; private set; } = "";
    [System.Text.Json.Serialization.JsonConverter(typeof(InvariantDecimalJsonConverter))]
    public decimal Fator { get; private set; }
    public string Fonte { get; private set; } = "";
    public string Metodo { get; private set; } = "";
    public DateOnly DataFonte { get; private set; }
    public string Contexto { get; private set; } = "";
    public string ReferenciaAmostra { get; private set; } = "";
    public string Proveniencia { get; private set; } = "";
    public Guid AutorId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public static ConversaoItem Create(Guid itemId, int number, ConversionData data, Guid author, DateTimeOffset now)
    {
        CatalogRules.Unit(data.Origem); CatalogRules.Unit(data.Destino);
        if (CatalogRules.Dimension(data.Origem) == CatalogRules.Dimension(data.Destino)) throw new ArgumentException("Use o fator exato da dimensão; conversão contextual exige dimensões diferentes.");
        if (data.Proveniencia is not ("Medido" or "Declarado" or "Estimado")) throw new ArgumentException("Origem da conversão inválida.");
        return new()
        {
            Id = Guid.NewGuid(),
            ItemId = itemId,
            Numero = number,
            Origem = data.Origem,
            Destino = data.Destino,
            Fator = CatalogRules.Decimal(data.Fator, true),
            Fonte = CatalogRules.Text(data.Fonte, 1000, "Fonte"),
            Metodo = CatalogRules.Text(data.Metodo, 500, "Método"),
            Contexto = CatalogRules.Text(data.Contexto, 2000, "Contexto"),
            ReferenciaAmostra = CatalogRules.Text(data.ReferenciaAmostra, 200, "Amostra"),
            Proveniencia = data.Proveniencia,
            DataFonte = data.DataFonte,
            AutorId = author,
            CreatedAtUtc = now
        };
    }
    public decimal? ToKg(decimal value, string unit)
    {
        if (unit == Origem && CatalogRules.Dimension(Destino) == "massa") return CatalogRules.Canonical(checked(value * Fator), Destino);
        if (unit == Destino && CatalogRules.Dimension(Origem) == "massa") return CatalogRules.Canonical(value / Fator, Origem);
        return null;
    }
}

public sealed record CatalogAudit(Guid Id, Guid RegistroId, string Tipo, string Operacao, Guid AutorId,
    DateTimeOffset CreatedAtUtc, string AntesJson, string DepoisJson);
