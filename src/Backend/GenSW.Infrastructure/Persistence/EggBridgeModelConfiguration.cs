using GenSW.Domain.Animals;
using GenSW.Domain.Catalog;
using GenSW.Domain.EggBridge;
using GenSW.Domain.Inventory;
using GenSW.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

namespace GenSW.Infrastructure.Persistence;

internal static class EggBridgeModelConfiguration
{
    internal static void Configure(ModelBuilder b, bool postgres)
    {
        if (postgres) b.HasSequence<long>("SequenciaConciliacaoOvos");
        b.Entity<AncoraConciliacaoOvos>(e => {
            e.ToTable("AncorasConciliacaoOvos", t => t.HasCheckConstraint("CK_AncorasConciliacaoOvos_Revisao", "\"Revisao\">=0")); e.HasKey(x => x.Id); e.HasIndex(x => x.ItemId).IsUnique();
            e.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<RecorteConciliacaoOvos>(e => {
            e.ToTable("RecortesConciliacaoOvos", t => t.HasCheckConstraint("CK_RecortesConciliacaoOvos_Conferencia", "\"RevisaoAncora\">=0 AND \"SequenciaAte\">=0 AND length(trim(\"Fronteira\"))>0 AND length(trim(\"FontesConsultadas\"))>0 AND length(trim(\"Motivo\"))>0"));
            e.HasKey(x => x.Id); e.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict); e.HasIndex(x => new { x.ItemId, x.CreatedAtUtc, x.Id });
            e.Property(x => x.OrdemAprovacao).ValueGeneratedOnAdd().HasDefaultValue(0L); e.HasIndex(x => x.OrdemAprovacao).IsUnique();
            e.ToTable("RecortesConciliacaoOvos", t => t.HasCheckConstraint("CK_RecortesConciliacaoOvos_Ordem", "\"OrdemAprovacao\">0"));
            e.Property(x => x.Fronteira).HasMaxLength(2000); e.Property(x => x.FontesConsultadas).HasMaxLength(2000); e.Property(x => x.Lacunas).HasMaxLength(2000);
        });
        b.Entity<DecisaoConciliacaoOvo>(e => {
            e.ToTable("DecisoesConciliacaoOvos", t => { t.HasCheckConstraint("CK_DecisoesConciliacaoOvos_Decisao", "\"Decisao\" IN ('NaoRecebidoComprovado','RecebidoComEquivalencia','Pendente') AND (\"Decisao\"='Pendente' OR \"IndividualComprovado\")"); t.HasCheckConstraint("CK_DecisoesConciliacaoOvos_Hash", "length(\"HashOrigem\")=64"); });
            e.HasKey(x => x.Id); e.HasIndex(x => new { x.RecorteId, x.ProducaoOvoId }).IsUnique(); e.HasOne<RecorteConciliacaoOvos>().WithMany().HasForeignKey(x => x.RecorteId).OnDelete(DeleteBehavior.Restrict); e.HasOne<ProducaoOvo>().WithMany().HasForeignKey(x => x.ProducaoOvoId).OnDelete(DeleteBehavior.Restrict); e.Property(x => x.Decisao).HasMaxLength(40); e.Property(x => x.HashOrigem).HasMaxLength(64); e.Property(x => x.Evidencia).HasMaxLength(2000);
        });
        b.Entity<ProvaEntradaOvos>(e => {
            e.ToTable("ProvasEntradaOvos", t => t.HasCheckConstraint("CK_ProvasEntradaOvos_Contagem", "\"Contagem\" BETWEEN 1 AND 99999999999999 AND length(trim(\"Evidencia\"))>0")); e.HasKey(x => x.Id); e.HasIndex(x => x.MovimentoEstoqueId).IsUnique(); e.HasAlternateKey(x => new { x.Id, x.MovimentoEstoqueId });
            e.HasOne<MovimentoEstoque>().WithMany().HasForeignKey(x => x.MovimentoEstoqueId).OnDelete(DeleteBehavior.Restrict); e.HasOne<EventoEstoque>().WithMany().HasForeignKey(x => x.EventoId).OnDelete(DeleteBehavior.Restrict); e.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<GrupoPonteOvos>(e => {
            e.ToTable("GruposPonteOvos", t => { t.HasCheckConstraint("CK_GruposPonteOvos_Operacao", "(\"Operacao\"='Entrada' AND \"ProvaId\" IS NULL) OR (\"Operacao\"='Associacao' AND \"ProvaId\" IS NOT NULL)"); t.HasCheckConstraint("CK_GruposPonteOvos_Quantidade", "\"Quantidade\" BETWEEN 1 AND 99999999999999"); }); e.HasKey(x => x.Id); e.HasIndex(x => new { x.ItemId, x.CreatedAtUtc, x.Id });
            e.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict); e.HasOne<LoteMaterial>().WithMany().HasForeignKey(x => x.LoteId).OnDelete(DeleteBehavior.Restrict); e.HasOne<LocalEstoque>().WithMany().HasForeignKey(x => x.LocalId).OnDelete(DeleteBehavior.Restrict); e.HasOne<RecorteConciliacaoOvos>().WithMany().HasForeignKey(x => x.RecorteId).OnDelete(DeleteBehavior.Restrict); e.HasOne<ProvaEntradaOvos>().WithMany().HasForeignKey(x => x.ProvaId).OnDelete(DeleteBehavior.Restrict); e.HasOne<EventoEstoque>().WithMany().HasForeignKey(x => x.EventoId).OnDelete(DeleteBehavior.Restrict); e.HasOne<MovimentoEstoque>().WithMany().HasForeignKey(x => x.MovimentoEstoqueId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.SnapshotLoteJson).HasColumnType("jsonb"); e.Property(x => x.Operacao).HasMaxLength(20); e.Property(x => x.SituacaoOriginal).HasMaxLength(20);
        });
        b.Entity<PonteProducaoOvo>(e => {
            e.ToTable("PontesProducaoOvos", t => t.HasCheckConstraint("CK_PontesProducaoOvos_Snapshot", "length(\"HashOrigem\")=64 AND \"Algoritmo\"='gensw-egg-bridge/1.0'")); e.HasKey(x => x.Id); e.HasIndex(x => x.ProducaoOvoId).IsUnique(); e.HasOne<ProducaoOvo>().WithMany().HasForeignKey(x => x.ProducaoOvoId).OnDelete(DeleteBehavior.Restrict); e.HasOne<GrupoPonteOvos>().WithMany().HasForeignKey(x => x.GrupoId).OnDelete(DeleteBehavior.Restrict); e.Property(x => x.SnapshotJson).HasColumnType("text"); e.Property(x => x.HashOrigem).HasMaxLength(64); e.Property(x => x.Algoritmo).HasMaxLength(50);
        });
        b.Entity<AlocacaoProvaOvos>(e => {
            e.ToTable("AlocacoesProvaOvos", t => t.HasCheckConstraint("CK_AlocacoesProvaOvos_Ordinal", "\"Ordinal\" BETWEEN 1 AND 99999999999999")); e.HasKey(x => x.Id); e.HasIndex(x => x.PonteId).IsUnique(); e.HasIndex(x => new { x.MovimentoEstoqueId, x.Ordinal }).IsUnique(); e.HasOne<PonteProducaoOvo>().WithMany().HasForeignKey(x => x.PonteId).OnDelete(DeleteBehavior.Restrict); e.HasOne<ProvaEntradaOvos>().WithMany().HasForeignKey(x => new { x.ProvaId, x.MovimentoEstoqueId }).HasPrincipalKey(x => new { x.Id, x.MovimentoEstoqueId }).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<OrigemLoteOvos>(e => { e.ToTable("OrigensLoteOvos"); e.HasKey(x => x.Id); e.HasIndex(x => x.LoteId).IsUnique(); e.HasIndex(x => x.GrupoId).IsUnique(); e.HasOne<LoteMaterial>().WithMany().HasForeignKey(x => x.LoteId).OnDelete(DeleteBehavior.Restrict); e.HasOne<GrupoPonteOvos>().WithMany().HasForeignKey(x => x.GrupoId).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<TratamentoPonteOvos>(e => {
            e.ToTable("TratamentosPonteOvos", t => t.HasCheckConstraint("CK_TratamentosPonteOvos_Tipo", "\"Tipo\" IN ('Anotacao','Invalidacao','Compensacao') AND ((\"Tipo\"='Compensacao')=(\"EventoId\" IS NOT NULL)) AND length(trim(\"Evidencia\"))>0")); e.HasKey(x => x.Id); e.HasIndex(x => new { x.GrupoId, x.Tipo }).IsUnique().HasFilter("\"Tipo\" IN ('Invalidacao','Compensacao')"); e.HasOne<GrupoPonteOvos>().WithMany().HasForeignKey(x => x.GrupoId).OnDelete(DeleteBehavior.Restrict); e.HasOne<EventoEstoque>().WithMany().HasForeignKey(x => x.EventoId).OnDelete(DeleteBehavior.Restrict); e.Property(x => x.Tipo).HasMaxLength(20);
        });
        b.Entity<ComandoPonteOvos>(e => {
            e.ToTable("ComandosPonteOvos", t => t.HasCheckConstraint("CK_ComandosPonteOvos_Chave", "length(\"Chave\") BETWEEN 1 AND 100 AND length(\"HashPayload\")=64 AND \"StatusHttp\" IN (200,201)")); e.HasKey(x => x.Id); e.HasIndex(x => new { x.Operacao, x.Chave }).IsUnique(); e.HasOne<GrupoPonteOvos>().WithMany().HasForeignKey(x => x.GrupoId).OnDelete(DeleteBehavior.Restrict); e.Property(x => x.Operacao).HasMaxLength(50); e.Property(x => x.Chave).HasMaxLength(100); e.Property(x => x.HashPayload).HasMaxLength(64); e.Property(x => x.Location).HasMaxLength(300); e.Property(x => x.RespostaJson).HasColumnType("text");
        });
        b.Entity<OrigemRecebimentoOvos>(e => {
            e.ToTable("OrigensRecebimentoOvos", t => t.HasCheckConstraint("CK_OrigensRecebimentoOvos_Evidencia", "length(trim(\"Evidencia\"))>0")); e.HasKey(x => x.Id); e.HasIndex(x => x.EventoId).IsUnique(); e.HasOne<EventoEstoque>().WithMany().HasForeignKey(x => x.EventoId).OnDelete(DeleteBehavior.Restrict); e.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict); e.HasOne<RecorteConciliacaoOvos>().WithMany().HasForeignKey(x => x.RecorteId).OnDelete(DeleteBehavior.Restrict); e.Property(x => x.Evidencia).HasMaxLength(2000);
        });
        foreach (var entity in new[] { typeof(AncoraConciliacaoOvos), typeof(RecorteConciliacaoOvos), typeof(ProvaEntradaOvos), typeof(GrupoPonteOvos), typeof(TratamentoPonteOvos), typeof(ComandoPonteOvos), typeof(OrigemRecebimentoOvos) })
        {
            b.Entity(entity).HasOne(typeof(ApplicationUser)).WithMany().HasForeignKey("AutorId").OnDelete(DeleteBehavior.Restrict);
            if (entity.GetProperty("ResponsavelId") is not null) b.Entity(entity).HasOne(typeof(ApplicationUser)).WithMany().HasForeignKey("ResponsavelId").OnDelete(DeleteBehavior.Restrict);
            if (entity.GetProperty("Motivo") is not null) b.Entity(entity).Property("Motivo").HasMaxLength(2000);
            if (entity.GetProperty("Evidencia") is not null) b.Entity(entity).Property("Evidencia").HasMaxLength(2000);
        }
    }
}
