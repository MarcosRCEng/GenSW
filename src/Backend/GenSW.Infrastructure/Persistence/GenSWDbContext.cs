using GenSW.Domain.People;
using GenSW.Domain.Species;
using GenSW.Domain.Breeds;
using GenSW.Domain.Varieties;
using GenSW.Domain.Animals;
using GenSW.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GenSW.Infrastructure.Persistence;

public sealed class GenSWDbContext(DbContextOptions<GenSWDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Pessoa> Pessoas => Set<Pessoa>();

    public DbSet<Especie> Especies => Set<Especie>();

    public DbSet<Raca> Racas => Set<Raca>();

    public DbSet<Variedade> Variedades => Set<Variedade>();

    public DbSet<Animal> Animais => Set<Animal>();

    public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        var usesNpgsql = Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL";
        var commonNameCanonicalConstraint = usesNpgsql
            ? "\"NomeComum\" <> '' AND \"NomeComum\" !~ U&'[\\0009-\\000D\\0085\\00A0\\1680\\2000-\\200A\\2028\\2029\\202F\\205F\\3000]' AND \"NomeComum\" !~ '(^ | $|  )'"
            : "\"NomeComum\" <> '' AND \"NomeComum\" = trim(\"NomeComum\") AND \"NomeComum\" NOT LIKE '%  %'";
        var scientificNameCanonicalConstraint = usesNpgsql
            ? "\"NomeCientifico\" IS NULL OR (\"NomeCientifico\" <> '' AND \"NomeCientifico\" !~ U&'[\\0009-\\000D\\0085\\00A0\\1680\\2000-\\200A\\2028\\2029\\202F\\205F\\3000]' AND \"NomeCientifico\" !~ '(^ | $|  )')"
            : "\"NomeCientifico\" IS NULL OR (\"NomeCientifico\" <> '' AND \"NomeCientifico\" = trim(\"NomeCientifico\") AND \"NomeCientifico\" NOT LIKE '%  %')";
        var nameCanonicalConstraint = usesNpgsql
            ? "\"Nome\" <> '' AND \"Nome\" !~ U&'[\\0009-\\000D\\0085\\00A0\\1680\\2000-\\200A\\2028\\2029\\202F\\205F\\3000]' AND \"Nome\" !~ '(^ | $|  )'"
            : "\"Nome\" <> '' AND \"Nome\" = trim(\"Nome\") AND \"Nome\" NOT LIKE '%  %'";
        var optionalNameCanonicalConstraint = usesNpgsql
            ? "\"Nome\" IS NULL OR (\"Nome\" <> '' AND \"Nome\" !~ U&'[\\0009-\\000D\\0085\\00A0\\1680\\2000-\\200A\\2028\\2029\\202F\\205F\\3000]' AND \"Nome\" !~ '(^ | $|  )')"
            : "\"Nome\" IS NULL OR (\"Nome\" <> '' AND \"Nome\" = trim(\"Nome\") AND \"Nome\" NOT LIKE '%  %')";

        builder.Entity<Pessoa>(pessoa =>
        {
            pessoa.ToTable("Pessoas", table =>
                table.HasCheckConstraint("CK_Pessoas_TipoPessoa", "\"TipoPessoa\" IN (1, 2)"));
            pessoa.HasKey(entity => entity.Id);
            pessoa.Property(entity => entity.TipoPessoa).HasConversion<int>().IsRequired();
            pessoa.Property(entity => entity.Nome).IsRequired().HasMaxLength(200);
            pessoa.Property(entity => entity.NomeFantasia).HasMaxLength(200);
            pessoa.Property(entity => entity.Ativo).IsRequired();
            pessoa.Property(entity => entity.CreatedAtUtc).IsRequired();
            pessoa.Property(entity => entity.UpdatedAtUtc).IsRequired();
        });

        builder.Entity<Especie>(especie =>
        {
            especie.ToTable("Especies", table =>
            {
                table.HasCheckConstraint(
                    "CK_Especies_NomeComum_Canonical",
                    commonNameCanonicalConstraint);
                table.HasCheckConstraint(
                    "CK_Especies_NomeCientifico_Canonical",
                    scientificNameCanonicalConstraint);
            });
            especie.HasKey(entity => entity.Id);
            especie.Property(entity => entity.NomeComum).IsRequired().HasMaxLength(200);
            especie.Property(entity => entity.NomeCientifico).HasMaxLength(200);
            especie.Property(entity => entity.Ativo).IsRequired().HasDefaultValue(true);
            especie.Property(entity => entity.CreatedAtUtc).IsRequired();
            especie.Property(entity => entity.UpdatedAtUtc).IsRequired();
        });

        builder.Entity<Raca>(raca =>
        {
            raca.ToTable("Racas", table =>
                table.HasCheckConstraint("CK_Racas_Nome_Canonical", nameCanonicalConstraint));
            raca.HasKey(entity => entity.Id);
            raca.Property(entity => entity.EspecieId).IsRequired();
            raca.Property(entity => entity.Nome).IsRequired().HasMaxLength(200);
            raca.Property(entity => entity.Ativo).IsRequired().HasDefaultValue(true);
            raca.Property(entity => entity.CreatedAtUtc).IsRequired();
            raca.Property(entity => entity.UpdatedAtUtc).IsRequired();
            raca.HasOne<Especie>().WithMany().HasForeignKey(entity => entity.EspecieId).IsRequired().OnDelete(DeleteBehavior.Restrict);
            raca.HasAlternateKey(entity => new { entity.Id, entity.EspecieId }).HasName("AK_Racas_Id_EspecieId");
        });

        builder.Entity<Variedade>(variedade =>
        {
            variedade.ToTable("Variedades", table =>
                table.HasCheckConstraint("CK_Variedades_Nome_Canonical", nameCanonicalConstraint));
            variedade.HasKey(entity => entity.Id);
            variedade.Property(entity => entity.EspecieId).IsRequired();
            variedade.Property(entity => entity.Nome).IsRequired().HasMaxLength(200);
            variedade.Property(entity => entity.Ativo).IsRequired().HasDefaultValue(true);
            variedade.Property(entity => entity.CreatedAtUtc).IsRequired();
            variedade.Property(entity => entity.UpdatedAtUtc).IsRequired();
            variedade.HasOne<Especie>().WithMany().HasForeignKey(entity => entity.EspecieId).IsRequired().OnDelete(DeleteBehavior.Restrict);
            variedade.HasAlternateKey(entity => new { entity.Id, entity.EspecieId }).HasName("AK_Variedades_Id_EspecieId");
        });

        builder.Entity<Animal>(animal =>
        {
            animal.ToTable("Animais", table =>
            {
                table.HasCheckConstraint("CK_Animais_CodigoInterno_Canonical", nameCanonicalConstraint.Replace("\"Nome\"", "\"CodigoInterno\""));
                table.HasCheckConstraint("CK_Animais_Nome_Canonical", optionalNameCanonicalConstraint);
                table.HasCheckConstraint("CK_Animais_Sexo", "\"Sexo\" IN (1, 2, 3)");
                table.HasCheckConstraint("CK_Animais_Escopo", "\"Escopo\" IN (1, 2)");
            });
            animal.HasKey(entity => entity.Id);
            animal.Property(entity => entity.CodigoInterno).IsRequired().HasMaxLength(64);
            animal.Property(entity => entity.Nome).HasMaxLength(200);
            animal.Property(entity => entity.EspecieId).IsRequired();
            animal.Property(entity => entity.Sexo).HasConversion<int>().IsRequired();
            animal.Property(entity => entity.DataNascimento).HasColumnType("date");
            animal.Property(entity => entity.Escopo).HasConversion<int>().IsRequired();
            animal.Property(entity => entity.Ativo).IsRequired().HasDefaultValue(true);
            animal.Property(entity => entity.CreatedAtUtc).IsRequired();
            animal.Property(entity => entity.UpdatedAtUtc).IsRequired();

            animal.HasIndex(entity => entity.CodigoInterno).IsUnique().HasDatabaseName("UX_Animais_CodigoInterno_CaseInsensitive");
            animal.HasIndex(entity => entity.EspecieId);
            animal.HasIndex(entity => new { entity.RacaId, entity.EspecieId });
            animal.HasIndex(entity => new { entity.VariedadeId, entity.EspecieId });

            animal.HasOne<Especie>().WithMany().HasForeignKey(entity => entity.EspecieId).IsRequired().OnDelete(DeleteBehavior.Restrict);
            animal.HasOne<Raca>().WithMany()
                .HasForeignKey(entity => new { entity.RacaId, entity.EspecieId })
                .HasPrincipalKey(entity => new { entity.Id, entity.EspecieId })
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_Animais_Racas_RacaId_EspecieId");
            animal.HasOne<Variedade>().WithMany()
                .HasForeignKey(entity => new { entity.VariedadeId, entity.EspecieId })
                .HasPrincipalKey(entity => new { entity.Id, entity.EspecieId })
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_Animais_Variedades_VariedadeId_EspecieId");
        });

        builder.Entity<ApplicationUser>(user =>
        {
            user.Property(entity => entity.PessoaId).IsRequired();
            user.Property(entity => entity.IsActive).IsRequired();
            user.Property(entity => entity.CreatedAtUtc).IsRequired();
            user.Property(entity => entity.UpdatedAtUtc).IsRequired();

            user.HasIndex(entity => entity.PessoaId).IsUnique();
            user.HasOne(entity => entity.Pessoa)
                .WithOne()
                .HasForeignKey<ApplicationUser>(entity => entity.PessoaId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<RefreshSession>(session =>
        {
            session.ToTable("RefreshSessions");
            session.HasKey(entity => entity.Id);
            session.Property(entity => entity.TokenHash).IsRequired();
            session.Property(entity => entity.CreatedAtUtc).IsRequired();
            session.Property(entity => entity.ExpiresAtUtc).IsRequired();

            session.HasIndex(entity => entity.TokenHash).IsUnique();
            session.HasIndex(entity => entity.UserId);
            session.HasIndex(entity => entity.FamilyId);

            session.HasOne(entity => entity.User)
                .WithMany()
                .HasForeignKey(entity => entity.UserId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);

            session.HasOne(entity => entity.ReplacedBySession)
                .WithMany()
                .HasForeignKey(entity => entity.ReplacedBySessionId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
