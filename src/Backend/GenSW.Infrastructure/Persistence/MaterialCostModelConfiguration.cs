using GenSW.Domain.Catalog;
using GenSW.Domain.Inventory;
using GenSW.Domain.MaterialCosts;
using GenSW.Domain.Production;
using GenSW.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GenSW.Infrastructure.Persistence;

internal static class MaterialCostModelConfiguration
{
    private static EntityTypeBuilder<T> Fact<T>(ModelBuilder model, string table, bool postgres) where T : class
    {
        var b = model.Entity<T>(); b.ToTable(table); b.HasKey("Id");
        foreach (var property in typeof(T).GetProperties())
        {
            if (property.PropertyType == typeof(string))
            {
                var p = b.Property(property.Name);
                if (property.Name.EndsWith("Json", StringComparison.Ordinal) || property.Name == "RestoRateio") p.HasColumnType(postgres && property.Name != "RespostaJson" ? "jsonb" : "TEXT");
                else p.HasMaxLength(property.Name is "Chave" or "Algoritmo" or "Operacao" or "Tipo" or "Estado" or "Cobertura" ? 100 : property.Name is "HashPayload" or "HashDocumento" ? 128 : 4000);
            }
            if (property.PropertyType == typeof(decimal) || property.PropertyType == typeof(decimal?))
                b.Property(property.Name).HasPrecision(property.Name.Contains("Quantidade", StringComparison.Ordinal) || property.Name.Contains("Unitario", StringComparison.Ordinal) ? 20 : property.Name == "Percentual" ? 9 : 18,
                    property.Name.Contains("Quantidade", StringComparison.Ordinal) || property.Name.Contains("Unitario", StringComparison.Ordinal) || property.Name == "Percentual" ? 6 : 2);
        }
        if (typeof(T).GetProperty("AutorId") is not null) b.HasOne<ApplicationUser>().WithMany().HasForeignKey("AutorId").OnDelete(DeleteBehavior.Restrict);
        if (typeof(T).GetProperty("ResponsavelId") is not null) b.HasOne<ApplicationUser>().WithMany().HasForeignKey("ResponsavelId").OnDelete(DeleteBehavior.Restrict);
        return b;
    }
    internal static void Configure(ModelBuilder model, bool postgres)
    {
        if(postgres)model.HasSequence<long>("SequenciaCustoMaterial");
        var reference = Fact<PrecoReferenciaMaterial>(model,"PrecosReferenciaMateriais",postgres);
        reference.HasOne<Item>().WithMany().HasForeignKey(x=>x.ItemId).OnDelete(DeleteBehavior.Restrict);
        reference.HasOne<PrecoReferenciaMaterial>().WithMany().HasForeignKey(x=>x.SubstituiId).OnDelete(DeleteBehavior.Restrict);
        reference.HasIndex(x=>x.SubstituiId).IsUnique(); reference.HasIndex(x=>new{x.ItemId,x.VigenciaInicio,x.Id});
        reference.ToTable("PrecosReferenciaMateriais",t=>t.HasCheckConstraint("CK_PrecoMaterial_Dados","\"Moeda\"='BRL' AND \"QuantidadeBase\">0 AND \"ValorUnitario\">=0 AND (\"VigenciaFim\" IS NULL OR \"VigenciaFim\">\"VigenciaInicio\")"));
        var evidence = Fact<EvidenciaCustoMaterial>(model,"EvidenciasCustoMateriais",postgres);
        evidence.HasOne<Item>().WithMany().HasForeignKey(x=>x.ItemId).OnDelete(DeleteBehavior.Restrict);
        evidence.ToTable("EvidenciasCustoMateriais",t=>t.HasCheckConstraint("CK_EvidenciaMaterial_Dados","\"Moeda\"='BRL' AND \"Quantidade\">0 AND \"Total\">=0"));
        var layer = Fact<CamadaCustoMaterial>(model,"CamadasCustoMateriais",postgres);
        layer.HasOne<LoteMaterial>().WithMany().HasForeignKey(x=>x.LoteId).OnDelete(DeleteBehavior.Restrict);
        layer.HasOne<Item>().WithMany().HasForeignKey(x=>x.ItemId).OnDelete(DeleteBehavior.Restrict);
        layer.HasOne<MovimentoEstoque>().WithMany().HasForeignKey(x=>x.MovimentoOrigemId).OnDelete(DeleteBehavior.Restrict);
        layer.HasOne<SaidaProducao>().WithMany().HasForeignKey(x=>x.SaidaProducaoId).OnDelete(DeleteBehavior.Restrict);
        layer.HasOne<EvidenciaCustoMaterial>().WithMany().HasForeignKey(x=>x.EvidenciaId).OnDelete(DeleteBehavior.Restrict);
        layer.HasOne<CamadaCustoMaterial>().WithMany().HasForeignKey(x=>x.CamadaReferenciaId).OnDelete(DeleteBehavior.Restrict);
        layer.HasIndex(x=>x.MovimentoOrigemId).IsUnique(); layer.HasIndex(x=>x.SaidaProducaoId).IsUnique(); layer.HasIndex(x=>new{x.LoteId,x.SequenciaOrigem,x.Id});
        layer.ToTable("CamadasCustoMateriais",t=>t.HasCheckConstraint("CK_CamadaMaterial_Dados","\"QuantidadeOriginal\">0 AND \"ValorConhecido\">=0 AND \"Estado\" IN ('Completo','Parcial','Desconhecido') AND \"Unidade\" IN ('kg','L','un') AND (\"Unidade\"<>'un' OR trunc(\"QuantidadeOriginal\")=\"QuantidadeOriginal\")"));
        var ev = Fact<EventoCustoMaterial>(model,"EventosCustoMateriais",postgres);
        ev.HasIndex(x=>x.Sequencia).IsUnique(); ev.HasIndex(x=>x.EventoOriginalId).IsUnique();
        ev.HasOne<EventoEstoque>().WithMany().HasForeignKey(x=>x.EventoEstoqueId).OnDelete(DeleteBehavior.Restrict);
        ev.HasOne<ExecucaoProducao>().WithMany().HasForeignKey(x=>x.ExecucaoId).OnDelete(DeleteBehavior.Restrict);
        ev.HasOne<EventoCustoMaterial>().WithMany().HasForeignKey(x=>x.EventoOriginalId).OnDelete(DeleteBehavior.Restrict);
        var part = Fact<ParcelaCustoMaterial>(model,"ParcelasCustoMateriais",postgres);
        part.HasOne<EventoCustoMaterial>().WithMany().HasForeignKey(x=>x.EventoCustoId).OnDelete(DeleteBehavior.Restrict);
        part.HasOne<CamadaCustoMaterial>().WithMany().HasForeignKey(x=>x.CamadaId).OnDelete(DeleteBehavior.Restrict);
        part.HasOne<LocalEstoque>().WithMany().HasForeignKey(x=>x.LocalId).OnDelete(DeleteBehavior.Restrict);
        part.HasOne<MovimentoEstoque>().WithMany().HasForeignKey(x=>x.MovimentoEstoqueId).OnDelete(DeleteBehavior.Restrict);
        part.HasOne<ParcelaCustoMaterial>().WithMany().HasForeignKey(x=>x.ParcelaOriginalId).OnDelete(DeleteBehavior.Restrict);
        part.HasIndex(x=>x.ParcelaOriginalId).IsUnique(); part.HasIndex(x=>new{x.CamadaId,x.LocalId,x.EventoCustoId});
        part.ToTable("ParcelasCustoMateriais",t=>t.HasCheckConstraint("CK_ParcelaMaterial_Dados","\"Quantidade\">0 AND \"ValorConhecido\">=0 AND \"Sentido\" IN ('Entrada','Saida')"));
        var pos = model.Entity<PosicaoCustoMaterial>(); pos.ToTable("PosicoesCustoMateriais"); pos.HasKey(x=>new{x.CamadaId,x.LocalId});
        pos.HasOne<CamadaCustoMaterial>().WithMany().HasForeignKey(x=>x.CamadaId).OnDelete(DeleteBehavior.Restrict);
        pos.HasOne<LocalEstoque>().WithMany().HasForeignKey(x=>x.LocalId).OnDelete(DeleteBehavior.Restrict);
        pos.Property(x=>x.Quantidade).HasPrecision(20,6); pos.Property(x=>x.ValorConhecido).HasPrecision(18,2); pos.Property(x=>x.Revisao).IsConcurrencyToken();
        pos.Property(x=>x.ComponentesJson).HasColumnType(postgres?"jsonb":"TEXT");pos.Property(x=>x.LacunasJson).HasColumnType(postgres?"jsonb":"TEXT");
        pos.ToTable("PosicoesCustoMateriais",t=>t.HasCheckConstraint("CK_PosicaoMaterial_Dados","\"Quantidade\">=0 AND \"ValorConhecido\">=0 AND (\"Quantidade\">0 OR \"ValorConhecido\"=0) AND \"Estado\" IN ('Completo','Parcial','Desconhecido')"));
        var calc=Fact<ApuracaoCustoExecucao>(model,"ApuracoesCustoExecucoes",postgres);
        calc.HasOne<ExecucaoProducao>().WithMany().HasForeignKey(x=>x.ExecucaoId).OnDelete(DeleteBehavior.Restrict);
        calc.HasOne<ApuracaoCustoExecucao>().WithMany().HasForeignKey(x=>x.ApuracaoOriginalId).OnDelete(DeleteBehavior.Restrict);
        calc.HasIndex(x=>x.ExecucaoId).IsUnique().HasFilter("\"ApuracaoOriginalId\" IS NULL");
        calc.HasIndex(x=>x.ApuracaoOriginalId).IsUnique();
        calc.ToTable("ApuracoesCustoExecucoes",t=>t.HasCheckConstraint("CK_ApuracaoMaterial_Dados","\"ValorConhecido\">=0 AND (\"ValorTotal\" IS NULL OR (\"Cobertura\"='Completo' AND \"ValorTotal\"=\"ValorConhecido\")) AND (\"Cobertura\"='Completo' OR \"ValorTotal\" IS NULL)"));
        var dest=Fact<DestinoCustoExecucao>(model,"DestinosCustoExecucoes",postgres);
        dest.HasOne<ApuracaoCustoExecucao>().WithMany().HasForeignKey(x=>x.ApuracaoId).OnDelete(DeleteBehavior.Restrict);
        dest.HasOne<SaidaProducao>().WithMany().HasForeignKey(x=>x.SaidaProducaoId).OnDelete(DeleteBehavior.Restrict);
        dest.HasOne<CamadaCustoMaterial>().WithMany().HasForeignKey(x=>x.CamadaSaidaId).OnDelete(DeleteBehavior.Restrict);
        dest.HasIndex(x=>new{x.ApuracaoId,x.DestinoId}).IsUnique();
        dest.ToTable("DestinosCustoExecucoes",t=>t.HasCheckConstraint("CK_DestinoMaterial_Dados","\"Percentual\" BETWEEN 0 AND 100 AND \"ValorConhecido\">=0 AND \"Tipo\" IN ('Saida','Perda') AND (\"ValorTotal\" IS NULL OR \"Cobertura\"='Completo') AND (\"Tipo\"<>'Perda' OR (\"CustoUnitarioCompleto\" IS NULL AND \"SaidaProducaoId\" IS NULL)) AND (\"Quantidade\">0 OR (\"Percentual\"=0 AND \"CustoUnitarioCompleto\" IS NULL))"));
        var command=Fact<ComandoCustoMaterial>(model,"ComandosCustoMateriais",postgres);
        command.HasIndex(x=>new{x.RecursoId,x.Operacao,x.Chave}).IsUnique();
        command.HasOne<EventoCustoMaterial>().WithMany().HasForeignKey(x=>x.EventoId).OnDelete(DeleteBehavior.Restrict);
    }
}
