using GenSW.Domain.Catalog;
using GenSW.Domain.Formulation;
using GenSW.Domain.Inventory;
using GenSW.Domain.Production;
using GenSW.Domain.Properties;
using GenSW.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

namespace GenSW.Infrastructure.Persistence;

internal static class ProductionModelConfiguration
{
    internal static void Configure(ModelBuilder b, bool postgres)
    {
        b.Entity<OrdemProducao>(e=>{
            e.ToTable("OrdensProducao",t=>{t.HasCheckConstraint("CK_OrdensProducao_Estado","\"Estado\" IN ('Rascunho','Planejada','Iniciada','Confirmada','Cancelada','AbortadaSemConsumo','Revertida')");t.HasCheckConstraint("CK_OrdensProducao_Revisao","\"Revisao\">0");});
            e.HasKey(x=>x.Id); e.Property(x=>x.Codigo).HasMaxLength(50);e.Property(x=>x.CodigoNormalizado).HasMaxLength(50);e.HasIndex(x=>x.CodigoNormalizado).IsUnique();e.Property(x=>x.Revisao).IsConcurrencyToken();e.Property(x=>x.Estado).HasMaxLength(30);e.Property(x=>x.SnapshotPlanejadoJson).HasColumnType("jsonb");e.Property(x=>x.PlanoHash).HasMaxLength(64);e.Property(x=>x.Tamanho).HasMaxLength(21);e.Property(x=>x.Unidade).HasMaxLength(3);e.Property(x=>x.PropriedadeNome).HasMaxLength(200);
            e.HasOne<RecipeVersion>().WithMany().HasForeignKey(x=>x.ReceitaVersaoId).OnDelete(DeleteBehavior.Restrict);e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x=>x.ResponsavelId).OnDelete(DeleteBehavior.Restrict);e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x=>x.AutorId).OnDelete(DeleteBehavior.Restrict);e.HasOne<Propriedade>().WithMany().HasForeignKey(x=>x.PropriedadeId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x=>new{x.Estado,x.CreatedAtUtc,x.Id});
        });
        b.Entity<ReservaProducao>(e=>{
            e.ToTable("ReservasProducao",t=>{t.HasCheckConstraint("CK_ReservasProducao_Quantidades","\"QuantidadeOriginal\">0 AND \"QuantidadeAtiva\">=0 AND \"QuantidadeConsumida\">=0 AND \"QuantidadeLiberada\">=0 AND \"QuantidadeOriginal\"=\"QuantidadeAtiva\"+\"QuantidadeConsumida\"+\"QuantidadeLiberada\"");
                var integral="\"Unidade\" IN ('kg','L','un') AND (\"Unidade\"<>'un' OR (\"QuantidadeOriginal\"=trunc(\"QuantidadeOriginal\") AND \"QuantidadeAtiva\"=trunc(\"QuantidadeAtiva\") AND \"QuantidadeConsumida\"=trunc(\"QuantidadeConsumida\") AND \"QuantidadeLiberada\"=trunc(\"QuantidadeLiberada\")))";
                if(!postgres)foreach(var column in new[]{"QuantidadeOriginal","QuantidadeAtiva","QuantidadeConsumida","QuantidadeLiberada"})integral=integral.Replace($"trunc(\"{column}\")",$"round(\"{column}\",0)",StringComparison.Ordinal);
                t.HasCheckConstraint("CK_ReservasProducao_Unidade",integral);});
            e.HasKey(x=>x.Id);e.Property(x=>x.Revisao).IsConcurrencyToken();e.Property(x=>x.Unidade).HasMaxLength(3);e.Property(x=>x.SnapshotJson).HasColumnType("jsonb");foreach(var name in new[]{"QuantidadeOriginal","QuantidadeAtiva","QuantidadeConsumida","QuantidadeLiberada"})e.Property<decimal>(name).HasPrecision(20,6);
            e.HasOne<OrdemProducao>().WithMany().HasForeignKey(x=>x.OrdemId).OnDelete(DeleteBehavior.Restrict);e.HasOne<PosicaoEstoque>().WithMany().HasForeignKey(x=>new{x.LoteId,x.LocalId}).OnDelete(DeleteBehavior.Restrict);e.HasIndex(x=>new{x.LoteId,x.LocalId});e.HasIndex(x=>new{x.OrdemId,x.LinhaId});
        });
        b.Entity<ExecucaoProducao>(e=>{e.ToTable("ExecucoesProducao",t=>t.HasCheckConstraint("CK_ExecucoesProducao_Resultado","\"Resultado\" IN ('Normal','ComDesvio','PerdaTotal','ConsumoIrregular','FatoApto')"));e.HasKey(x=>x.Id);e.HasIndex(x=>x.OrdemId).IsUnique();e.Property(x=>x.SnapshotJson).HasColumnType("jsonb");e.Property(x=>x.Algoritmo).HasMaxLength(50);e.HasOne<OrdemProducao>().WithMany().HasForeignKey(x=>x.OrdemId).OnDelete(DeleteBehavior.Restrict);e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x=>x.AutorId).OnDelete(DeleteBehavior.Restrict);e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x=>x.ResponsavelId).OnDelete(DeleteBehavior.Restrict);});
        b.Entity<ExecucaoMovimento>(e=>{e.ToTable("ExecucaoMovimentos",t=>t.HasCheckConstraint("CK_ExecucaoMovimentos_Papel","\"Papel\" IN ('Entrada','Saida','Compensacao')"));e.HasKey(x=>x.Id);e.HasIndex(x=>x.MovimentoEstoqueId).IsUnique();e.HasOne<ExecucaoProducao>().WithMany().HasForeignKey(x=>x.ExecucaoId).OnDelete(DeleteBehavior.Restrict);e.HasOne<MovimentoEstoque>().WithMany().HasForeignKey(x=>x.MovimentoEstoqueId).OnDelete(DeleteBehavior.Restrict);});
        b.Entity<SaidaProducao>(e=>{e.ToTable("SaidasProducao",t=>t.HasCheckConstraint("CK_SaidasProducao_Quantidade","\"Quantidade\">0"));e.HasKey(x=>x.Id);e.HasIndex(x=>x.LoteId).IsUnique();e.Property(x=>x.Quantidade).HasPrecision(20,6);e.HasOne<ExecucaoProducao>().WithMany().HasForeignKey(x=>x.ExecucaoId).OnDelete(DeleteBehavior.Restrict);e.HasOne<LoteMaterial>().WithMany().HasForeignKey(x=>x.LoteId).OnDelete(DeleteBehavior.Restrict);e.HasOne<LocalEstoque>().WithMany().HasForeignKey(x=>x.LocalId).OnDelete(DeleteBehavior.Restrict);e.HasOne<EventoEstoque>().WithMany().HasForeignKey(x=>x.EventoId).OnDelete(DeleteBehavior.Restrict);});
        b.Entity<GenealogiaMaterial>(e=>{e.ToTable("GenealogiaMateriais");e.HasKey(x=>x.Id);e.HasIndex(x=>new{x.ExecucaoId,x.LoteEntradaId,x.LoteSaidaId}).IsUnique();e.HasOne<ExecucaoProducao>().WithMany().HasForeignKey(x=>x.ExecucaoId).OnDelete(DeleteBehavior.Restrict);e.HasOne<LoteMaterial>().WithMany().HasForeignKey(x=>x.LoteEntradaId).OnDelete(DeleteBehavior.Restrict);e.HasOne<LoteMaterial>().WithMany().HasForeignKey(x=>x.LoteSaidaId).OnDelete(DeleteBehavior.Restrict);});
        b.Entity<HistoricoProducao>(e=>{e.ToTable("HistoricoProducao");e.HasKey(x=>x.Id);e.HasOne<OrdemProducao>().WithMany().HasForeignKey(x=>x.OrdemId).OnDelete(DeleteBehavior.Restrict);e.Property(x=>x.AntesJson).HasColumnType("jsonb");e.Property(x=>x.DepoisJson).HasColumnType("jsonb");e.Property(x=>x.Motivo).HasMaxLength(2000);e.HasIndex(x=>new{x.OrdemId,x.CreatedAtUtc,x.Id});});
        b.Entity<OcorrenciaProducao>(e=>{e.ToTable("OcorrenciasProducao");e.HasKey(x=>x.Id);e.HasOne<OrdemProducao>().WithMany().HasForeignKey(x=>x.OrdemId).OnDelete(DeleteBehavior.Restrict);e.Property(x=>x.Motivo).HasMaxLength(2000);e.Property(x=>x.Evidencia).HasMaxLength(2000);e.HasIndex(x=>new{x.OrdemId,x.CreatedAtUtc,x.Id});});
        b.Entity<ComandoProducao>(e=>{e.ToTable("ComandosProducao",t=>t.HasCheckConstraint("CK_ComandosProducao_Chave","length(\"Chave\") BETWEEN 1 AND 100 AND length(\"HashPayload\")=64 AND \"StatusHttp\" IN (200,201)"));e.HasKey(x=>x.Id);e.HasOne<OrdemProducao>().WithMany().HasForeignKey(x=>x.OrdemId).OnDelete(DeleteBehavior.Restrict);e.HasIndex(x=>new{x.OrdemId,x.Operacao,x.Chave}).IsUnique();e.Property(x=>x.Operacao).HasMaxLength(50);e.Property(x=>x.Chave).HasMaxLength(100);e.Property(x=>x.HashPayload).HasMaxLength(64);e.Property(x=>x.RespostaJson).HasColumnType("text");});
        b.Entity<ReversaoProducao>(e=>{e.ToTable("ReversoesProducao");e.HasKey(x=>x.Id);e.HasIndex(x=>x.ExecucaoId).IsUnique();e.HasOne<ExecucaoProducao>().WithMany().HasForeignKey(x=>x.ExecucaoId).OnDelete(DeleteBehavior.Restrict);e.Property(x=>x.Motivo).HasMaxLength(2000);e.Property(x=>x.Evidencia).HasMaxLength(2000);});
        b.Entity<ReservaProducao>(e=>e.ToTable("ReservasProducao",t=>t.HasCheckConstraint("CK_ReservasProducao_Revisao","\"Revisao\">0")));
        b.Entity<ExecucaoProducao>(e=>{e.Property(x=>x.Resultado).HasMaxLength(30);e.ToTable("ExecucoesProducao",t=>t.HasCheckConstraint("CK_ExecucoesProducao_Data","\"DataObservada\" IS NULL OR \"DataObservada\"<=\"DataOperacional\""));});
        b.Entity<ExecucaoMovimento>(e=>e.Property(x=>x.Papel).HasMaxLength(20));
        b.Entity<SaidaProducao>(e=>e.Property(x=>x.SituacaoOriginal).HasMaxLength(20));
        b.Entity<HistoricoProducao>(e=>{e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x=>x.AutorId).OnDelete(DeleteBehavior.Restrict);e.Property(x=>x.Operacao).HasMaxLength(50);e.ToTable("HistoricoProducao",t=>t.HasCheckConstraint("CK_HistoricoProducao_Motivo","length(trim(\"Motivo\"))>0"));});
        b.Entity<OcorrenciaProducao>(e=>{e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x=>x.AutorId).OnDelete(DeleteBehavior.Restrict);e.Property(x=>x.Tipo).HasMaxLength(50);e.ToTable("OcorrenciasProducao",t=>{t.HasCheckConstraint("CK_OcorrenciasProducao_Evidencia","length(trim(\"Motivo\"))>0 AND length(trim(\"Evidencia\"))>0");t.HasCheckConstraint("CK_OcorrenciasProducao_Data",postgres?"\"DataObservada\" IS NULL OR \"DataObservada\"<=(\"CreatedAtUtc\" AT TIME ZONE 'America/Sao_Paulo')::date":"\"DataObservada\" IS NULL OR \"DataObservada\"<=date(\"CreatedAtUtc\",'-3 hours')");});});
        b.Entity<ComandoProducao>(e=>{e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x=>x.AutorId).OnDelete(DeleteBehavior.Restrict);e.Property(x=>x.Location).HasMaxLength(300);});
        b.Entity<ReversaoProducao>(e=>{e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x=>x.AutorId).OnDelete(DeleteBehavior.Restrict);e.ToTable("ReversoesProducao",t=>t.HasCheckConstraint("CK_ReversoesProducao_Evidencia","length(trim(\"Motivo\"))>0 AND length(trim(\"Evidencia\"))>0"));});
    }
}
