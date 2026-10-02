using GenSW.Domain.Financial;
using GenSW.Domain.People;
using GenSW.Domain.Animals;
using Microsoft.EntityFrameworkCore;

namespace GenSW.Infrastructure.Persistence;

internal static class FinancialModelConfiguration
{
    public static void Configure(ModelBuilder b, bool postgres)
    {
        b.Entity<ConfiguracaoCaixa>(e =>
        {
            e.ToTable("ConfiguracoesCaixa", t => t.HasCheckConstraint("CK_Caixa_Singleton", postgres ? "\"Id\" = 1 AND EXTRACT(DAY FROM \"DataInicio\") = 1" : "\"Id\" = 1 AND strftime('%d', \"DataInicio\") = '01'"));
            e.HasKey(x => x.Id); e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.SaldoInicial).HasPrecision(18, 2); e.Property(x => x.Versao).IsConcurrencyToken();
        });
        b.Entity<CategoriaFinanceira>(e =>
        {
            e.ToTable("CategoriasFinanceiras", t => t.HasCheckConstraint("CK_Categoria_Natureza", "\"Natureza\" IN (1,2)")); e.HasKey(x => x.Id);
            e.Property(x => x.Nome).HasMaxLength(100); e.Property(x => x.NomeNormalizado).HasMaxLength(100);
            e.Property(x => x.Codigo).HasMaxLength(50); e.Property(x => x.Versao).IsConcurrencyToken();
            e.HasIndex(x => new { x.Natureza, x.NomeNormalizado }).IsUnique(); e.HasIndex(x => x.Codigo).IsUnique();
            var names = new[] { "Insumos", "Alimentação", "Saúde animal", "Serviços", "Outras despesas", "Venda de animais", "Outras receitas" };
            var date = new DateTimeOffset(2026, 10, 2, 0, 0, 0, TimeSpan.Zero);
            e.HasData(names.Select((name, i) => new CategoriaFinanceira(Guid.Parse($"37600000-0000-0000-0000-{i + 1:000000000000}"), name, name.ToUpperInvariant(), i < 5 ? NaturezaFinanceira.Despesa : NaturezaFinanceira.Receita, i == 5 ? "VENDA_ANIMAIS" : null, true, 1, date, date)));
        });
        b.Entity<LancamentoCaixa>(e =>
        {
            e.ToTable("LancamentosCaixa", t =>
            {
                t.HasCheckConstraint("CK_Lancamento_Valor", "\"Valor\" > 0");
                t.HasCheckConstraint("CK_Lancamento_Enums", "\"Tipo\" IN (1,2) AND \"FormaPagamento\" BETWEEN 1 AND 5 AND \"Origem\" BETWEEN 1 AND 3");
                t.HasCheckConstraint("CK_Lancamento_Origem", "(\"Origem\" = 1 AND \"LancamentoOriginalId\" IS NULL) OR (\"Origem\" <> 1 AND \"LancamentoOriginalId\" IS NOT NULL AND length(trim(\"MotivoAjuste\")) > 0)");
            });
            e.HasKey(x => x.Id); e.Property(x => x.Valor).HasPrecision(18, 2);
            e.Property(x => x.Descricao).HasMaxLength(200); e.Property(x => x.Observacao).HasMaxLength(2000); e.Property(x => x.MotivoAjuste).HasMaxLength(2000);
            e.Property(x => x.Versao).IsConcurrencyToken();
            e.HasOne<CategoriaFinanceira>().WithMany().HasForeignKey(x => x.CategoriaId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Pessoa>().WithMany().HasForeignKey(x => x.PessoaId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Animal>().WithMany().HasForeignKey(x => x.AnimalId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<LancamentoCaixa>().WithMany().HasForeignKey(x => x.LancamentoOriginalId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.DataMovimento, x.Id });
            e.HasIndex(x => x.LancamentoOriginalId).IsUnique().HasFilter("\"Origem\" = 2").HasDatabaseName("UX_Caixa_ReversaoOriginal");
        });
        b.Entity<AuditoriaLancamento>(e =>
        {
            e.ToTable("AuditoriasLancamento"); e.HasKey(x => x.Id); e.Property(x => x.Operacao).HasMaxLength(30); e.Property(x => x.Motivo).HasMaxLength(2000);
            e.HasOne<LancamentoCaixa>().WithMany().HasForeignKey(x => x.LancamentoId).OnDelete(DeleteBehavior.Restrict); e.HasIndex(x => new { x.LancamentoId, x.CreatedAtUtc });
        });
        b.Entity<MesCaixa>(e => { e.ToTable("MesesCaixa"); e.HasKey(x => x.Mes); e.Property(x => x.Versao).IsConcurrencyToken(); });
        b.Entity<FechamentoCaixa>(e =>
        {
            e.ToTable("FechamentosCaixa"); e.HasKey(x => x.Id); e.HasIndex(x => x.Mes).IsUnique(); e.Property(x => x.Observacao).HasMaxLength(2000);
            e.Property(x => x.SaldoAbertura).HasPrecision(18,2); e.Property(x => x.Receitas).HasPrecision(18,2); e.Property(x => x.Despesas).HasPrecision(18,2); e.Property(x => x.SaldoFinal).HasPrecision(18,2); e.Property(x => x.SaldoConferido).HasPrecision(18,2);
            e.HasOne<MesCaixa>().WithMany().HasForeignKey(x => x.Mes).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<IdempotenciaFinanceira>(e =>
        {
            e.ToTable("IdempotenciasFinanceiras"); e.HasKey(x => new { x.AutorId, x.Operacao, x.Chave });
            e.Property(x => x.Operacao).HasMaxLength(100); e.Property(x => x.Chave).HasMaxLength(100); e.Property(x => x.PayloadHash).HasMaxLength(64);
        });
    }
}
