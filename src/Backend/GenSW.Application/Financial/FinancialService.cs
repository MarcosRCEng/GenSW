using GenSW.Domain.Financial;

namespace GenSW.Application.Financial;

// The application validates request invariants. The specific repository rechecks
// mutable state inside the PostgreSQL transaction before publishing the operation.
public sealed class FinancialService(IFinancialRepository repository)
{
    public static void ValidateEntry(LancamentoCommand c)
    {
        ArgumentNullException.ThrowIfNull(c);
        CaixaRules.Dinheiro(c.Valor, true); CaixaRules.Texto(c.Descricao,200,"Descrição"); CaixaRules.Opcional(c.Observacao,2000);
        if (!Enum.IsDefined(c.Tipo) || !Enum.IsDefined(c.FormaPagamento) || c.CategoriaId == Guid.Empty) throw new ArgumentException("Tipo, categoria ou forma de pagamento inválidos.");
    }
    public Task<ConfiguracaoCaixa?> ConfiguracaoAsync(CancellationToken ct) => repository.ConfiguracaoAsync(ct);
    public Task<ConfiguracaoCaixa> ConfigureAsync(ConfiguracaoCommand c, Guid actor, CancellationToken ct) { CaixaRules.Dinheiro(c.SaldoInicial); return repository.ConfigureAsync(c,actor,ct); }
    public Task<IReadOnlyList<CategoriaFinanceira>> CategoriasAsync(CancellationToken ct) => repository.CategoriasAsync(ct);
    public Task<CategoriaFinanceira> CreateCategoriaAsync(CategoriaCommand c,CancellationToken ct) => repository.CreateCategoriaAsync(c,ct);
    public Task<CategoriaFinanceira> UpdateCategoriaAsync(Guid id,CategoriaUpdateCommand c,CancellationToken ct) => repository.UpdateCategoriaAsync(id,c,ct);
    public Task<LancamentosPage> ListAsync(LancamentoQuery q,CancellationToken ct) => repository.ListAsync(q,ct);
    public Task<LancamentoView> GetAsync(Guid id,CancellationToken ct) => repository.GetAsync(id,ct);
    public Task<LancamentoCaixa> CreateAsync(LancamentoCommand c,Guid actor,string key,CancellationToken ct) { ValidateEntry(c); return repository.CreateAsync(c,actor,key,ct); }
    public Task<LancamentoCaixa> UpdateAsync(Guid id,CorrecaoCommand c,Guid actor,CancellationToken ct) { ValidateEntry(c.Lancamento); CaixaRules.Texto(c.Motivo,2000,"Motivo"); return repository.UpdateAsync(id,c,actor,ct); }
    public Task<LancamentoCaixa> CancelAsync(Guid id,CancelamentoCommand c,Guid actor,CancellationToken ct) { CaixaRules.Texto(c.Motivo,2000,"Motivo"); return repository.CancelAsync(id,c,actor,ct); }
    public Task<IReadOnlyList<AuditoriaLancamento>> HistoricoAsync(Guid id,CancellationToken ct) => repository.HistoricoAsync(id,ct);
    public Task<ResumoCaixa> ResumoAsync(DateOnly month,CancellationToken ct) => repository.ResumoAsync(month,ct);
    public Task<FechamentoCaixa?> FechamentoAsync(DateOnly month,CancellationToken ct) => repository.FechamentoAsync(month,ct);
    public Task<IReadOnlyList<FechamentoCaixa>> FechamentosAsync(CancellationToken ct) => repository.FechamentosAsync(ct);
    public Task<FechamentoCaixa> CloseAsync(DateOnly month,FechamentoCommand c,Guid actor,CancellationToken ct) { CaixaRules.Texto(c.Observacao,2000,"Observação"); if(c.SaldoConferido.HasValue) CaixaRules.Dinheiro(c.SaldoConferido.Value); return repository.CloseAsync(month,c,actor,ct); }
    public Task<AjusteResult> AdjustAsync(Guid id,AjusteCommand c,Guid actor,string key,CancellationToken ct) { CaixaRules.Texto(c.Motivo,2000,"Motivo"); if(c.Substituto is not null) ValidateEntry(c.Substituto); return repository.AdjustAsync(id,c,actor,key,ct); }
}
