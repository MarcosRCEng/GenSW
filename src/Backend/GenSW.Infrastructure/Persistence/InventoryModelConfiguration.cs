using GenSW.Domain.Catalog;
using GenSW.Domain.Formulation;
using GenSW.Domain.Inventory;
using GenSW.Domain.Properties;
using GenSW.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

namespace GenSW.Infrastructure.Persistence;

internal static class InventoryModelConfiguration
{
    internal static void Configure(ModelBuilder b, bool postgres)
    {
        if (postgres) b.HasSequence<long>("SequenciaEstoque");
        b.Entity<LocalEstoque>(e =>
        {
            e.ToTable("LocaisEstoque", t => { t.HasCheckConstraint("CK_LocaisEstoque_Revisao", "\"Revisao\" > 0"); t.HasCheckConstraint("CK_LocaisEstoque_Finalidade", "\"Finalidade\" IN ('Ordinario','Segregacao')"); });
            e.HasKey(x => x.Id); e.Property(x => x.Codigo).HasMaxLength(50); e.Property(x => x.CodigoNormalizado).HasMaxLength(50); e.Property(x => x.Nome).HasMaxLength(200); e.Property(x => x.Descricao).HasMaxLength(2000); e.Property(x => x.Finalidade).HasMaxLength(20); e.Property(x => x.Revisao).IsConcurrencyToken();
            e.HasIndex(x => x.CodigoNormalizado).IsUnique(); e.HasIndex(x => new { x.Ativo, x.Nome, x.Id }); e.HasIndex(x => x.PropriedadeId);
            e.HasOne<Propriedade>().WithMany().HasForeignKey(x => x.PropriedadeId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<LoteMaterial>(e =>
        {
            e.ToTable("LotesMateriais", t =>
            {
                t.HasCheckConstraint("CK_LotesMateriais_Revisao", "\"Revisao\" > 0");
                t.HasCheckConstraint("CK_LotesMateriais_Unidade", "\"Unidade\" IN ('kg','L','un')");
                t.HasCheckConstraint("CK_LotesMateriais_Situacao", "\"Situacao\" IN ('Liberado','Bloqueado','Encerrado')");
                t.HasCheckConstraint("CK_LotesMateriais_Validade", "\"Validade\" IS NULL OR (\"FonteValidade\" IS NOT NULL AND length(trim(\"FonteValidade\")) > 0 AND \"ResponsavelValidadeId\" IS NOT NULL AND (\"DataOrigem\" IS NULL OR \"Validade\" >= \"DataOrigem\") AND (\"Fabricacao\" IS NULL OR \"Validade\" >= \"Fabricacao\") AND (\"Coleta\" IS NULL OR \"Validade\" >= \"Coleta\"))");
            });
            e.HasKey(x => x.Id); e.Property(x => x.Codigo).HasMaxLength(50); e.Property(x => x.CodigoNormalizado).HasMaxLength(50); e.Property(x => x.CodigoExterno).HasMaxLength(100); e.Property(x => x.Origem).HasMaxLength(1000); e.Property(x => x.Fonte).HasMaxLength(1000); e.Property(x => x.FonteValidade).HasMaxLength(1000); e.Property(x => x.Aplicabilidade).HasMaxLength(2000); e.Property(x => x.Situacao).HasMaxLength(20); e.Property(x => x.Unidade).HasMaxLength(3); e.Property(x => x.Revisao).IsConcurrencyToken();
            e.HasIndex(x => x.CodigoNormalizado).IsUnique(); e.HasIndex(x => new { x.ItemId, x.Situacao, x.Validade, x.Id });
            e.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ResponsavelId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ResponsavelValidadeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<NutritionProfile>().WithMany().HasForeignKey(x => x.PerfilNutricionalId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ConversaoItem>().WithMany().HasForeignKey(x => x.ConversaoItemId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<PosicaoEstoque>(e =>
        {
            e.ToTable("PosicoesEstoque", t =>
            {
                t.HasCheckConstraint("CK_PosicoesEstoque_Quantidade", "\"Quantidade\" >= 0");
                t.HasCheckConstraint("CK_PosicoesEstoque_Unidade", "\"Unidade\" IN ('kg','L','un') AND (\"Unidade\" <> 'un' OR \"Quantidade\" = round(\"Quantidade\",0))");
                t.HasCheckConstraint("CK_PosicoesEstoque_Revisao", "\"Revisao\" > 0");
            }); e.HasKey(x => new { x.LoteId, x.LocalId }); e.Property(x => x.Unidade).HasMaxLength(3); e.Property(x => x.Quantidade).HasPrecision(20,6); e.Property(x => x.Revisao).IsConcurrencyToken();
            e.HasOne<LoteMaterial>().WithMany().HasForeignKey(x => x.LoteId).OnDelete(DeleteBehavior.Restrict); e.HasOne<LocalEstoque>().WithMany().HasForeignKey(x => x.LocalId).OnDelete(DeleteBehavior.Restrict); e.HasIndex(x => x.LocalId);
        });
        b.Entity<EventoEstoque>(e =>
        {
            e.ToTable("EventosEstoque", t => { t.HasCheckConstraint("CK_EventosEstoque_Sequencia", "\"Sequencia\" > 0"); t.HasCheckConstraint("CK_EventosEstoque_Data", "\"DataObservada\" IS NULL OR \"DataObservada\" <= \"DataOperacional\""); }); e.HasKey(x => x.Id);
            e.Property(x => x.Sequencia).ValueGeneratedNever(); e.Property(x => x.Tipo).HasMaxLength(30); e.Property(x => x.Motivo).HasMaxLength(2000); e.Property(x => x.Documento).HasMaxLength(1000); e.Property(x => x.Algoritmo).HasMaxLength(50); e.Property(x => x.SnapshotJson).HasColumnType("jsonb");
            e.HasIndex(x => x.Sequencia).IsUnique(); e.HasIndex(x => new { x.Tipo, x.DataOperacional, x.Id });
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AutorId).OnDelete(DeleteBehavior.Restrict); e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ResponsavelId).OnDelete(DeleteBehavior.Restrict); e.HasOne<EventoEstoque>().WithMany().HasForeignKey(x => x.EventoReferenciaId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<MovimentoEstoque>(e =>
        {
            e.ToTable("MovimentosEstoque", t =>
            {
                t.HasCheckConstraint("CK_MovimentosEstoque_Quantidade", "\"Quantidade\" > 0 AND \"SaldoAnterior\" >= 0 AND \"SaldoPosterior\" >= 0");
                t.HasCheckConstraint("CK_MovimentosEstoque_Ordinal", "\"Ordinal\" > 0");
                t.HasCheckConstraint("CK_MovimentosEstoque_Sentido", "\"Sentido\" IN ('Entrada','Saida') AND \"SaldoPosterior\" = \"SaldoAnterior\" + CASE WHEN \"Sentido\" = 'Entrada' THEN \"Quantidade\" ELSE -\"Quantidade\" END");
                t.HasCheckConstraint("CK_MovimentosEstoque_Unidade", "\"Unidade\" IN ('kg','L','un') AND \"UnidadeDeclarada\" IN ('kg','g','L','mL','un') AND (\"Unidade\" <> 'un' OR (\"Quantidade\" = round(\"Quantidade\",0) AND \"SaldoAnterior\" = round(\"SaldoAnterior\",0) AND \"SaldoPosterior\" = round(\"SaldoPosterior\",0)))");
                if (postgres) t.HasCheckConstraint("CK_MovimentosEstoque_Declarado", "\"QuantidadeDeclarada\" ~ '^[0-9]{1,14}(\\.[0-9]{1,6})?$' AND (\"QuantidadeDeclarada\"::numeric > 0 OR (\"TipoEvento\" = 'Ajuste' AND \"QuantidadeDeclarada\"::numeric = 0)) AND (\"UnidadeDeclarada\" <> 'un' OR \"QuantidadeDeclarada\"::numeric = trunc(\"QuantidadeDeclarada\"::numeric)) AND \"Residuo\" ~ '^-?[0-9]+(\\.[0-9]+)?$'");
            });
            e.HasKey(x => x.Id); e.Property(x => x.Unidade).HasMaxLength(3); e.Property(x => x.UnidadeDeclarada).HasMaxLength(3); e.Property(x => x.Sentido).HasMaxLength(10); e.Property(x => x.TipoEvento).HasMaxLength(30); e.Property(x => x.QuantidadeDeclarada).HasMaxLength(21); e.Property(x => x.Residuo).HasMaxLength(60); e.Property(x => x.Quantidade).HasPrecision(20,6); e.Property(x => x.SaldoAnterior).HasPrecision(20,6); e.Property(x => x.SaldoPosterior).HasPrecision(20,6); e.Property(x => x.SnapshotJson).HasColumnType("jsonb");
            e.HasIndex(x => new { x.EventoId, x.Ordinal }).IsUnique(); e.HasIndex(x => new { x.LoteId, x.LocalId }).IsUnique().HasFilter("\"TipoEvento\" = 'Abertura'").HasDatabaseName("UX_MovimentosEstoque_Abertura"); e.HasIndex(x => new { x.LoteId, x.LocalId, x.EventoId });
            e.HasOne<EventoEstoque>().WithMany().HasForeignKey(x => x.EventoId).OnDelete(DeleteBehavior.Restrict); e.HasOne<PosicaoEstoque>().WithMany().HasForeignKey(x => new { x.LoteId, x.LocalId }).OnDelete(DeleteBehavior.Restrict); e.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ConversaoItem>().WithMany().HasForeignKey(x => x.ConversaoItemId).OnDelete(DeleteBehavior.Restrict); e.HasOne<NutritionProfile>().WithMany().HasForeignKey(x => x.PerfilNutricionalId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<HistoricoEstoque>(e =>
        {
            e.ToTable("HistoricoEstoque", t => t.HasCheckConstraint("CK_HistoricoEstoque_Sequencia", "\"Sequencia\" > 0")); e.HasKey(x => x.Id); e.Property(x => x.Sequencia).ValueGeneratedNever(); e.Property(x => x.Tipo).HasMaxLength(30); e.Property(x => x.Operacao).HasMaxLength(40); e.Property(x => x.Motivo).HasMaxLength(2000); e.Property(x => x.AntesJson).HasColumnType("jsonb"); e.Property(x => x.DepoisJson).HasColumnType("jsonb"); e.HasIndex(x => new { x.RegistroId, x.Sequencia, x.Id }); e.HasIndex(x => x.Sequencia).IsUnique(); e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AutorId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<ComandoEstoque>(e =>
        {
            e.ToTable("ComandosEstoque", t => t.HasCheckConstraint("CK_ComandosEstoque_Sucesso", "\"StatusHttp\" IN (200,201) AND length(\"Chave\") BETWEEN 1 AND 100 AND length(\"HashPayload\") = 64")); e.HasKey(x => x.Id); e.Property(x => x.Operacao).HasMaxLength(40); e.Property(x => x.Chave).HasMaxLength(100); e.Property(x => x.HashPayload).HasMaxLength(64); e.Property(x => x.Location).HasMaxLength(300); e.Property(x => x.RespostaJson).HasColumnType("text"); e.HasIndex(x => new { x.AutorId, x.Operacao, x.RecursoId, x.Chave }).IsUnique();
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AutorId).OnDelete(DeleteBehavior.Restrict); e.HasOne<EventoEstoque>().WithMany().HasForeignKey(x => x.EventoId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
