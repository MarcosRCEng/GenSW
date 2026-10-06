using GenSW.Domain.Catalog;
using GenSW.Domain.Formulation;
using Microsoft.EntityFrameworkCore;

namespace GenSW.Infrastructure.Persistence;

internal static class FormulationModelConfiguration
{
    internal static void Configure(ModelBuilder b, bool postgres)
    {
        b.Entity<CategoriaItem>(e =>
        {
            e.ToTable("CategoriasItens", t => t.HasCheckConstraint("CK_CategoriasItens_Revisao", "\"Revisao\" > 0")); e.HasKey(x => x.Id);
            e.Property(x => x.Nome).HasMaxLength(200); e.Property(x => x.NomeNormalizado).HasMaxLength(200); e.HasIndex(x => x.NomeNormalizado).IsUnique();
        });
        b.Entity<Item>(e =>
        {
            e.ToTable("Itens", t =>
            {
                t.HasCheckConstraint("CK_Itens_Revisao", "\"Revisao\" > 0");
                t.HasCheckConstraint("CK_Itens_Unidade", "\"Unidade\" IN ('kg', 'L', 'un')");
                t.HasCheckConstraint("CK_Itens_Classe", "\"Classe\" IN ('Alimentar', 'OutroMaterialIncorporado', 'Embalagem', 'Consumivel')");
            }); e.HasKey(x => x.Id);
            e.Property(x => x.Codigo).HasMaxLength(50); e.Property(x => x.CodigoNormalizado).HasMaxLength(50);
            e.Property(x => x.Nome).HasMaxLength(200); e.Property(x => x.Descricao).HasMaxLength(2000);
            e.Property(x => x.Classe).HasMaxLength(30); e.Property(x => x.Unidade).HasMaxLength(3);
            e.HasIndex(x => x.CodigoNormalizado).IsUnique(); e.HasIndex(x => new { x.Ativo, x.Nome, x.Id });
            e.HasOne<CategoriaItem>().WithMany().HasForeignKey(x => x.CategoriaId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<ConversaoItem>(e =>
        {
            e.ToTable("ConversoesItens", t =>
            {
                t.HasCheckConstraint("CK_ConversoesItens_Fator", "\"Fator\" > 0"); t.HasCheckConstraint("CK_ConversoesItens_Numero", "\"Numero\" > 0");
                t.HasCheckConstraint("CK_ConversoesItens_Unidades", "\"Origem\" IN ('kg','g','L','mL','un') AND \"Destino\" IN ('kg','g','L','mL','un') AND \"Origem\" <> \"Destino\"");
            }); e.HasKey(x => x.Id); e.Property(x => x.Fator).HasPrecision(20, 6);
            e.Property(x => x.Origem).HasMaxLength(3); e.Property(x => x.Destino).HasMaxLength(3);
            e.Property(x => x.Fonte).HasMaxLength(1000); e.Property(x => x.Metodo).HasMaxLength(500);
            e.Property(x => x.Contexto).HasMaxLength(2000); e.Property(x => x.ReferenciaAmostra).HasMaxLength(200); e.Property(x => x.Proveniencia).HasMaxLength(20);
            e.HasIndex(x => new { x.ItemId, x.Numero }).IsUnique(); e.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<NutritionProfile>(e =>
        {
            e.ToTable("PerfisNutricionais", t =>
            {
                t.HasCheckConstraint("CK_PerfisNutricionais_Versao", "\"Numero\" > 0 AND \"Revisao\" > 0");
                t.HasCheckConstraint("CK_PerfisNutricionais_Estado", "\"Estado\" IN ('Rascunho','Publicado','Inativo')");
            }); e.HasKey(x => x.Id); e.Property(x => x.Estado).HasMaxLength(20); e.Property(x => x.ConteudoJson).HasColumnType("jsonb");
            e.HasIndex(x => new { x.ItemId, x.Numero }).IsUnique(); e.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<Recipe>(e =>
        {
            e.ToTable("Receitas", t => t.HasCheckConstraint("CK_Receitas_Revisao", "\"Revisao\" > 0")); e.HasKey(x => x.Id);
            e.Property(x => x.Codigo).HasMaxLength(50); e.Property(x => x.CodigoNormalizado).HasMaxLength(50); e.Property(x => x.Nome).HasMaxLength(200); e.Property(x => x.Finalidade).HasMaxLength(2000);
            e.HasIndex(x => x.CodigoNormalizado).IsUnique(); e.HasIndex(x => new { x.Ativo, x.Nome, x.Id });
        });
        b.Entity<RecipeVersion>(e =>
        {
            e.ToTable("ReceitasVersoes", t =>
            {
                t.HasCheckConstraint("CK_ReceitasVersoes_Versao", "\"Numero\" > 0 AND \"Revisao\" > 0");
                t.HasCheckConstraint("CK_ReceitasVersoes_Estado", "\"Estado\" IN ('Rascunho','Publicado','Inativo')");
            }); e.HasKey(x => x.Id); e.Property(x => x.Estado).HasMaxLength(20); e.Property(x => x.ConteudoJson).HasColumnType("jsonb");
            e.HasIndex(x => new { x.ReceitaId, x.Numero }).IsUnique(); e.HasOne<Recipe>().WithMany().HasForeignKey(x => x.ReceitaId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<RecipeReference>(e =>
        {
            e.ToTable("ReceitasReferencias"); e.HasKey(x => x.Id);
            e.HasOne<RecipeVersion>().WithMany().HasForeignKey(x => x.VersaoId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<RecipeVersion>().WithMany().HasForeignKey(x => x.SubVersaoId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<NutritionProfile>().WithMany().HasForeignKey(x => x.PerfilId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ConversaoItem>().WithMany().HasForeignKey(x => x.ConversaoId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<CatalogAudit>(e =>
        {
            e.ToTable("HistoricoFormulacao"); e.HasKey(x => x.Id); e.Property(x => x.Tipo).HasMaxLength(30); e.Property(x => x.Operacao).HasMaxLength(30);
            e.Property(x => x.AntesJson).HasColumnType("jsonb"); e.Property(x => x.DepoisJson).HasColumnType("jsonb"); e.HasIndex(x => new { x.RegistroId, x.CreatedAtUtc, x.Id });
        });
        b.Entity<FormulationSnapshot>(e =>
        {
            e.ToTable("SnapshotsFormulacao", t => t.HasCheckConstraint("CK_SnapshotsFormulacao_Tipo", "\"Tipo\" IN ('Simulacao','Comparacao')")); e.HasKey(x => x.Id);
            e.Property(x => x.Tipo).HasMaxLength(20); e.Property(x => x.Chave).HasMaxLength(100); e.Property(x => x.Hash).HasMaxLength(64); e.Property(x => x.ConteudoJson).HasColumnType("jsonb");
            e.HasIndex(x => new { x.AutorId, x.Tipo, x.Chave }).IsUnique(); e.HasIndex(x => new { x.Tipo, x.CreatedAtUtc, x.Id });
        });
        // PostgreSQL-specific immutability is installed by the additive migration; SQLite rule tests use the domain guards.
    }
}
