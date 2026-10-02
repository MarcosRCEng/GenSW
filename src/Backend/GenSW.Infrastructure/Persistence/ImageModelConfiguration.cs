using GenSW.Domain.Animals;
using GenSW.Domain.Varieties;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace GenSW.Infrastructure.Persistence;

internal static class ImageModelConfiguration
{
    internal static void Configure(ModelBuilder builder)
    {
        builder.Ignore<Imagem>();
        ConfigureCommon(builder.Entity<ImagemAnimal>(), "ImagensAnimal", "AnimalId");
        ConfigureCommon(builder.Entity<ImagemVariedade>(), "ImagensVariedade", "VariedadeId");
        builder.Entity<ImagemAnimal>().HasOne<Animal>().WithMany().HasForeignKey(x => x.AnimalId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<ImagemVariedade>().HasOne<Variedade>().WithMany().HasForeignKey(x => x.VariedadeId).OnDelete(DeleteBehavior.Restrict);
    }
    private static void ConfigureCommon<T>(EntityTypeBuilder<T> entity, string table, string owner) where T : Imagem
    {
        entity.HasBaseType((Type?)null);
        entity.ToTable(table, t =>
        {
            t.HasCheckConstraint($"CK_{table}_Ordem", "\"Ordem\" >= 0");
            t.HasCheckConstraint($"CK_{table}_Representativa", "NOT \"Representativa\" OR \"Ativa\"");
            t.HasCheckConstraint($"CK_{table}_Dimensoes", "\"Largura\" BETWEEN 1 AND 2048 AND \"Altura\" BETWEEN 1 AND 2048 AND \"TamanhoBytes\" > 0");
        });
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Legenda).HasMaxLength(200);
        entity.Property(x => x.ArquivoKey).HasMaxLength(64).IsRequired();
        entity.Property(x => x.MiniaturaKey).HasMaxLength(64).IsRequired();
        entity.Property(x => x.Mime).HasMaxLength(32).IsRequired();
        entity.HasIndex(owner).IsUnique().HasFilter("\"Ativa\" AND \"Representativa\"").HasDatabaseName($"UX_{table}_Representativa");
        entity.HasIndex(owner, nameof(Imagem.Ordem), nameof(Imagem.CreatedAtUtc), nameof(Imagem.Id));
    }
}
