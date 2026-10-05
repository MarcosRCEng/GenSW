using GenSW.Domain.Animals;
using GenSW.Domain.Properties;
using Microsoft.EntityFrameworkCore;

namespace GenSW.Infrastructure.Persistence;

internal static class PropriedadeModelConfiguration
{
    public static void Configure(ModelBuilder builder, bool postgres)
    {
        builder.Entity<Propriedade>(entity =>
        {
            entity.ToTable("Propriedades", table => table.HasCheckConstraint("CK_Propriedades_Nome_Canonical", postgres
                ? "\"Nome\" <> '' AND \"Nome\" !~ U&'[\\0009-\\000D\\0085\\00A0\\1680\\2000-\\200A\\2028\\2029\\202F\\205F\\3000]' AND \"Nome\" !~ '(^ | $|  )'"
                : "\"Nome\" <> '' AND \"Nome\" = trim(\"Nome\") AND \"Nome\" NOT LIKE '%  %'"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Nome).IsRequired().HasMaxLength(200);
            entity.Property(x => x.NomeNormalizado).IsRequired().HasMaxLength(200);
            entity.HasIndex(x => x.NomeNormalizado).IsUnique().HasDatabaseName("UX_Propriedades_Nome_CaseInsensitive");
            entity.Property(x => x.Localizacao).HasMaxLength(500);
            entity.Property(x => x.Observacao).HasMaxLength(2000);
            entity.HasIndex(x => new { x.Ativo, x.Nome, x.Id });
        });
        builder.Entity<VinculoAnimalPropriedade>(entity =>
        {
            entity.ToTable("VinculosAnimalPropriedade", table =>
                table.HasCheckConstraint("CK_VinculosAnimalPropriedade_Periodo", "\"DataFim\" IS NULL OR \"DataFim\" >= \"DataInicio\""));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Observacao).HasMaxLength(2000);
            entity.HasOne<Animal>().WithMany().HasForeignKey(x => x.AnimalId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Propriedade>().WithMany().HasForeignKey(x => x.PropriedadeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.AnimalId).IsUnique().HasFilter("\"DataFim\" IS NULL")
                .HasDatabaseName("UX_VinculosAnimalPropriedade_Animal_Atual");
            entity.HasIndex(x => new { x.AnimalId, x.DataInicio, x.CreatedAtUtc, x.Id });
            entity.HasIndex(x => new { x.PropriedadeId, x.DataFim });
        });
    }
}
