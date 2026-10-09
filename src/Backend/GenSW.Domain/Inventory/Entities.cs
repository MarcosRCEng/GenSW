using GenSW.Domain.Catalog;

namespace GenSW.Domain.Inventory;

public sealed class LocalEstoque
{
    private LocalEstoque() { }
    public Guid Id { get; private set; }
    public string Codigo { get; private set; } = "";
    public string CodigoNormalizado { get; private set; } = "";
    public string Nome { get; private set; } = "";
    public string? Descricao { get; private set; }
    public Guid? PropriedadeId { get; private set; }
    public string Finalidade { get; private set; } = "Ordinario";
    public bool Ativo { get; private set; } = true;
    public int Revisao { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public static LocalEstoque Create(string code, string name, string? description, Guid? property, string purpose, bool active, DateTimeOffset now)
    {
        var value = new LocalEstoque { Id = Guid.NewGuid(), CreatedAtUtc = now, Finalidade = Purpose(purpose), Ativo = active };
        value.Update(code, name, description, property, 0, now); return value;
    }
    private static string Purpose(string value) => value is "Ordinario" or "Segregacao" ? value : throw new ArgumentException("Finalidade de local inválida.");
    public void Update(string code, string name, string? description, Guid? property, int expected, DateTimeOffset now)
    {
        InventoryRules.Expected(Revisao, expected); Codigo = CatalogRules.Text(code, 50, "Código"); CodigoNormalizado = Codigo.ToUpperInvariant();
        Nome = CatalogRules.Text(name, 200, "Nome"); Descricao = CatalogRules.Optional(description); PropriedadeId = property; Touch(now);
    }
    public void SetActive(bool active, int expected, DateTimeOffset now) { InventoryRules.Expected(Revisao, expected); Ativo = active; Touch(now); }
    public void SetPurpose(string purpose, int expected, DateTimeOffset now) { InventoryRules.Expected(Revisao, expected); Finalidade = Purpose(purpose); Touch(now); }
    private void Touch(DateTimeOffset now) { Revisao = checked(Revisao + 1); UpdatedAtUtc = now; }
}
public sealed record LoteData(string CodigoExterno, string Origem, string Fonte, Guid ResponsavelId,
    DateOnly? DataOrigem, DateOnly? Fabricacao, DateOnly? Coleta);
public sealed class LoteMaterial
{
    private LoteMaterial() { }
    public Guid Id { get; private set; }
    public Guid ItemId { get; private set; }
    public string Codigo { get; private set; } = "";
    public string CodigoNormalizado { get; private set; } = "";
    public string? CodigoExterno { get; private set; }
    public string Origem { get; private set; } = "";
    public string Fonte { get; private set; } = "";
    public Guid ResponsavelId { get; private set; }
    public DateOnly? DataOrigem { get; private set; }
    public DateOnly? Fabricacao { get; private set; }
    public DateOnly? Coleta { get; private set; }
    public DateOnly? Validade { get; private set; }
    public string? FonteValidade { get; private set; }
    public Guid? ResponsavelValidadeId { get; private set; }
    public string Situacao { get; private set; } = "Liberado";
    public bool Ativo { get; private set; } = true;
    public string Unidade { get; private set; } = "";
    public Guid? PerfilNutricionalId { get; private set; }
    public Guid? ConversaoItemId { get; private set; }
    public string? Aplicabilidade { get; private set; }
    public int Revisao { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public static LoteMaterial Create(Guid item, string code, string unit, LoteData data, DateOnly? expiry, string? expirySource, Guid? expiryResponsible,
        Guid? profile, Guid? conversion, string? applicability, bool blocked, DateTimeOffset now, DateOnly today)
    {
        InventoryRules.Id(item); var value = new LoteMaterial { Id = Guid.NewGuid(), ItemId = item, Codigo = CatalogRules.Text(code, 50, "Código"), Unidade = unit,
            Validade = expiry, FonteValidade = expirySource, ResponsavelValidadeId = expiryResponsible, PerfilNutricionalId = profile, ConversaoItemId = conversion,
            Aplicabilidade = CatalogRules.Optional(applicability), Situacao = blocked || InventoryRules.Expired(expiry, today) ? "Bloqueado" : "Liberado", CreatedAtUtc = now };
        if (unit is not ("kg" or "L" or "un")) throw new ArgumentException("Unidade inválida.");
        value.CodigoNormalizado = value.Codigo.ToUpperInvariant(); value.Update(data, 0, now, today); value.ValidateExpiry(); return value;
    }
    public void Update(LoteData data, int expected, DateTimeOffset now, DateOnly today)
    {
        InventoryRules.Expected(Revisao, expected); InventoryRules.Id(data.ResponsavelId);
        InventoryRules.Dates(data.DataOrigem, data.Fabricacao, data.Coleta, Validade, today);
        CodigoExterno = CatalogRules.Optional(data.CodigoExterno, 100); Origem = CatalogRules.Text(data.Origem, 1000, "Origem"); Fonte = CatalogRules.Text(data.Fonte, 1000, "Fonte");
        ResponsavelId = data.ResponsavelId; DataOrigem = data.DataOrigem; Fabricacao = data.Fabricacao; Coleta = data.Coleta; Touch(now);
    }
    public void SetActive(bool active, int expected, DateTimeOffset now) { InventoryRules.Expected(Revisao, expected); Ativo = active; Touch(now); }
    public void SetState(string state, int expected, DateTimeOffset now, DateOnly today)
    {
        InventoryRules.Expected(Revisao, expected);
        if (Situacao == "Encerrado" || state is not ("Liberado" or "Bloqueado" or "Encerrado")) throw new InventoryConflictException("situacao_invalida", "Lote encerrado é terminal.");
        if (state == "Liberado" && InventoryRules.Expired(Validade, today)) throw new InventoryConflictException("lote_vencido", "Lote vencido não pode ser liberado.");
        Situacao = state; Touch(now);
    }
    public void SetExpiry(DateOnly? expiry, string? source, Guid? responsible, int expected, DateTimeOffset now, DateOnly today)
    {
        InventoryRules.Expected(Revisao, expected); InventoryRules.Dates(DataOrigem, Fabricacao, Coleta, expiry, today);
        Validade = expiry; FonteValidade = source; ResponsavelValidadeId = responsible; ValidateExpiry(); Touch(now);
    }
    public void SetReferences(Guid? profile, Guid? conversion, string? applicability, int expected, DateTimeOffset now)
    { InventoryRules.Expected(Revisao, expected); PerfilNutricionalId = profile; ConversaoItemId = conversion; Aplicabilidade = CatalogRules.Optional(applicability); Touch(now); }
    private void ValidateExpiry()
    { if (Validade.HasValue) { FonteValidade = CatalogRules.Text(FonteValidade, 1000, "Fonte da validade"); InventoryRules.Id(ResponsavelValidadeId ?? Guid.Empty); } }
    private void Touch(DateTimeOffset now) { Revisao = checked(Revisao + 1); UpdatedAtUtc = now; }
}
public sealed class PosicaoEstoque
{
    private PosicaoEstoque() { }
    public Guid LoteId { get; private set; }
    public Guid LocalId { get; private set; }
    public string Unidade { get; private set; } = "";
    public decimal Quantidade { get; private set; }
    public int Revisao { get; private set; }
    public static PosicaoEstoque Create(Guid lot, Guid local, string unit) => new() { LoteId = lot, LocalId = local, Unidade = unit };
    public void SetBalance(decimal balance, int expected)
    { InventoryRules.Expected(Revisao, expected); InventoryRules.Balance(balance, Unidade); Quantidade = balance; Revisao = checked(Revisao + 1); }
}
public sealed record EventoEstoque(Guid Id, long Sequencia, string Tipo, Guid AutorId, Guid ResponsavelId,
    DateTimeOffset CreatedAtUtc, DateOnly DataOperacional, DateOnly? DataObservada, string Motivo,
    string? Documento, Guid? EventoReferenciaId, string Algoritmo, string SnapshotJson);
public sealed record MovimentoEstoque(Guid Id, Guid EventoId, int Ordinal, Guid LoteId, Guid LocalId,
    Guid ItemId, string Unidade, string Sentido, decimal Quantidade, string QuantidadeDeclarada,
    string UnidadeDeclarada, string Residuo, decimal SaldoAnterior, decimal SaldoPosterior,
    Guid? ConversaoItemId, Guid? PerfilNutricionalId, string SnapshotJson, string TipoEvento);
public sealed record HistoricoEstoque(Guid Id, Guid RegistroId, string Tipo, string Operacao, long Sequencia,
    Guid AutorId, DateTimeOffset CreatedAtUtc, string Motivo, string AntesJson, string DepoisJson);
public sealed record ComandoEstoque(Guid Id, Guid AutorId, string Operacao, Guid RecursoId, string Chave,
    string HashPayload, int StatusHttp, string? Location, string RespostaJson, Guid? EventoId, Guid? RegistroId, DateTimeOffset CreatedAtUtc, bool ExigeAdmin = false);
