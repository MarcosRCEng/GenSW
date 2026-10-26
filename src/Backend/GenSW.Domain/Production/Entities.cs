using GenSW.Domain.Catalog;
using GenSW.Domain.Inventory;

namespace GenSW.Domain.Production;

public sealed class OrdemProducao
{
    private OrdemProducao() { }
    public Guid Id { get; private set; }
    public string Codigo { get; private set; } = "";
    public string CodigoNormalizado { get; private set; } = "";
    public Guid ReceitaVersaoId { get; private set; }
    public string Tamanho { get; private set; } = "";
    public string Unidade { get; private set; } = "";
    public Guid ResponsavelId { get; private set; }
    public Guid? PropriedadeId { get; private set; }
    public string? PropriedadeNome { get; private set; }
    public string Estado { get; private set; } = "Rascunho";
    public int Revisao { get; private set; }
    public string? SnapshotPlanejadoJson { get; private set; }
    public string? PlanoHash { get; private set; }
    public Guid AutorId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? PlanejadaEmUtc { get; private set; }
    public DateTimeOffset? IniciadaEmUtc { get; private set; }
    public DateTimeOffset? ConfirmadaEmUtc { get; private set; }
    public static OrdemProducao Create(Guid id, string code, Guid recipe, string size, string unit, Guid responsible,
        Guid? property, string? propertyName, Guid actor, DateTimeOffset now)
    {
        InventoryRules.Id(id); var order = new OrdemProducao { Id=id, AutorId=actor, CreatedAtUtc=now };
        order.Update(code,recipe,size,unit,responsible,property,propertyName,0,now); return order;
    }
    public void Update(string code, Guid recipe, string size, string unit, Guid responsible, Guid? property, string? propertyName, int expected, DateTimeOffset now)
    {
        Require("Rascunho"); Expect(expected); InventoryRules.Id(recipe); InventoryRules.Id(responsible); CatalogRules.Quantity(size,unit);
        Codigo=CatalogRules.Text(code,50,"Código"); CodigoNormalizado=Codigo.ToUpperInvariant(); ReceitaVersaoId=recipe;
        Tamanho=size; Unidade=unit; ResponsavelId=responsible; PropriedadeId=property; PropriedadeNome=propertyName; Touch(now);
    }
    public void Expect(int expected) => InventoryRules.Expected(Revisao,expected);
    public void Require(params string[] states)
    { if(!states.Contains(Estado,StringComparer.Ordinal)) throw new InventoryConflictException("estado_invalido","A ação não é permitida no estado atual da ordem."); }
    public void Plan(string json,string hash,int expected,DateTimeOffset now)
    { Require("Rascunho"); Expect(expected); SnapshotPlanejadoJson=json; PlanoHash=hash; Estado="Planejada"; PlanejadaEmUtc=now; Touch(now); }
    public void Transition(string target,int expected,DateTimeOffset now)
    {
        var allowed = (Estado,target) switch {
            ("Planejada","Rascunho") or ("Planejada","Iniciada") or ("Planejada","Cancelada") or ("Rascunho","Cancelada") or
            ("Iniciada","Confirmada") or ("Iniciada","AbortadaSemConsumo") or ("Confirmada","Revertida") => true, _ => false };
        if(!allowed) throw new InventoryConflictException("estado_invalido","Transição inválida para a ordem.");
        Expect(expected);
        Estado=target; if(target=="Rascunho"){SnapshotPlanejadoJson=null;PlanoHash=null;PlanejadaEmUtc=null;} if(target=="Iniciada") IniciadaEmUtc=now; if(target=="Confirmada") ConfirmadaEmUtc=now; Touch(now);
    }
    public void RecordOccurrence(int expected,DateTimeOffset now) { Expect(expected); Touch(now); }
    private void Touch(DateTimeOffset now) { Revisao=checked(Revisao+1); UpdatedAtUtc=now; }
}

public sealed class ReservaProducao
{
    private ReservaProducao() { }
    public Guid Id { get; private set; }
    public Guid OrdemId { get; private set; }
    public Guid LinhaId { get; private set; }
    public Guid LoteId { get; private set; }
    public Guid LocalId { get; private set; }
    public string Unidade { get; private set; } = "";
    public decimal QuantidadeOriginal { get; private set; }
    public decimal QuantidadeAtiva { get; private set; }
    public decimal QuantidadeConsumida { get; private set; }
    public decimal QuantidadeLiberada { get; private set; }
    public string SnapshotJson { get; private set; } = "";
    public int Revisao { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public static ReservaProducao Create(Guid id,Guid order,Guid line,Guid lot,Guid local,string unit,decimal quantity,string snapshot,DateTimeOffset now)
    {
        foreach(var value in new[]{id,order,line,lot,local}) InventoryRules.Id(value);
        InventoryRules.Balance(quantity,unit); if(quantity<=0) throw new ArgumentException("Reserva exige quantidade positiva.");
        return new(){Id=id,OrdemId=order,LinhaId=line,LoteId=lot,LocalId=local,Unidade=unit,QuantidadeOriginal=quantity,QuantidadeAtiva=quantity,SnapshotJson=snapshot,Revisao=1,CreatedAtUtc=now};
    }
    public void Settle(decimal consumed)
    {
        if(consumed<0||consumed>QuantidadeAtiva) throw new ArgumentException("Consumo da reserva inválido.");
        InventoryRules.Balance(consumed,Unidade); QuantidadeConsumida+=consumed; QuantidadeLiberada+=QuantidadeAtiva-consumed; QuantidadeAtiva=0; Revisao++;
    }
}
public sealed record ExecucaoProducao(Guid Id,Guid OrdemId,string Resultado,Guid AutorId,Guid ResponsavelId,
    DateTimeOffset CreatedAtUtc,DateOnly DataOperacional,DateOnly? DataObservada,string SnapshotJson,string Algoritmo);
public sealed record ExecucaoMovimento(Guid Id,Guid ExecucaoId,Guid LinhaId,Guid? AlocacaoId,Guid MovimentoEstoqueId,string Papel);
public sealed record SaidaProducao(Guid Id,Guid ExecucaoId,Guid LinhaId,Guid LoteId,Guid LocalId,decimal Quantidade,
    int RevisaoLoteOriginal,string SituacaoOriginal,DateOnly? ValidadeOriginal,Guid EventoId);
public sealed record GenealogiaMaterial(Guid Id,Guid ExecucaoId,Guid LoteEntradaId,Guid LoteSaidaId);
public sealed record HistoricoProducao(Guid Id,Guid OrdemId,string Operacao,Guid AutorId,DateTimeOffset CreatedAtUtc,
    string Motivo,string AntesJson,string DepoisJson);
public sealed record OcorrenciaProducao(Guid Id,Guid OrdemId,string Tipo,Guid AutorId,DateTimeOffset CreatedAtUtc,
    DateOnly? DataObservada,string Motivo,string Evidencia,bool HouveConsumoFisico);
public sealed record ComandoProducao(Guid Id,Guid OrdemId,string Operacao,string Chave,string HashPayload,Guid AutorId,
    int StatusHttp,string? Location,string RespostaJson,bool ExigeAdmin,DateTimeOffset CreatedAtUtc);
public sealed record ReversaoProducao(Guid Id,Guid ExecucaoId,Guid AutorId,DateTimeOffset CreatedAtUtc,string Motivo,string Evidencia);
