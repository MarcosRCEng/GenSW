using GenSW.Domain.People;
using GenSW.Domain.Species;
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
