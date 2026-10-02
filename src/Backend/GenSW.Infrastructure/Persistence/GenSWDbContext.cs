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
    public DbSet<PesagemAnimal> PesagensAnimal => Set<PesagemAnimal>();
    public DbSet<ImagemAnimal> ImagensAnimal => Set<ImagemAnimal>();
    public DbSet<ImagemVariedade> ImagensVariedade => Set<ImagemVariedade>();

    public DbSet<IdentificacaoAnimal> IdentificacoesAnimal => Set<IdentificacaoAnimal>();

    public DbSet<RegistroAnimal> RegistrosAnimal => Set<RegistroAnimal>();
    public DbSet<FiliacaoAnimal> FiliacoesAnimal => Set<FiliacaoAnimal>();
    public DbSet<Cruzamento> Cruzamentos => Set<Cruzamento>();
    public DbSet<CicloReprodutivo> CiclosReprodutivos => Set<CicloReprodutivo>();
    public DbSet<ProducaoOvo> ProducoesOvos => Set<ProducaoOvo>();

    public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        ImageModelConfiguration.Configure(builder);
        builder.Entity<FiliacaoAnimal>().HasIndex(x => new { x.ProgenitorId, x.AnimalId }).HasFilter("\"Ativa\"").HasDatabaseName("IX_FiliacoesAnimal_DescendentesAtivos");
        builder.Entity<FiliacaoAnimal>().HasIndex(x => x.ProgenitorId);
        builder.Entity<PesagemAnimal>(entity =>
        {
            entity.ToTable("PesagensAnimal", table =>
            {
                table.HasCheckConstraint("CK_PesagensAnimal_Peso", "\"PesoGramas\" BETWEEN 0.01 AND 99999999.99");
                table.HasCheckConstraint("CK_PesagensAnimal_Marco", "\"TipoMarco\" BETWEEN 1 AND 6");
                table.HasCheckConstraint("CK_PesagensAnimal_Idade", "(\"TipoMarco\" = 3 AND \"IdadeReferenciaDias\" IS NOT NULL AND \"IdadeReferenciaDias\" >= 0) OR (\"TipoMarco\" <> 3 AND \"IdadeReferenciaDias\" IS NULL)");
                table.HasCheckConstraint("CK_PesagensAnimal_Outro", "\"TipoMarco\" <> 6 OR (\"DescricaoMarco\" IS NOT NULL AND length(trim(\"DescricaoMarco\")) > 0)");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PesoGramas).HasPrecision(10, 2);
            entity.Property(x => x.DescricaoMarco).HasMaxLength(100);
            entity.Property(x => x.Observacao).HasMaxLength(2000);
            entity.HasOne<Animal>().WithMany().HasForeignKey(x => x.AnimalId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.AnimalId, x.DataMedicao, x.Id });
        });
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
            especie.Property(entity => entity.Ovipara).IsRequired().HasDefaultValue(false);
            especie.Property(entity => entity.PesoPadraoOvoGramas).HasPrecision(10, 2);
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

        var identificacaoValorCanonicalConstraint = usesNpgsql
            ? "\"Valor\" <> '' AND \"Valor\" = btrim(\"Valor\")"
            : "\"Valor\" <> '' AND \"Valor\" = trim(\"Valor\")";
        var identificacaoDescricaoTipoSemanticsConstraint = usesNpgsql
            ? "(\"Tipo\" IN (1, 2, 3, 4, 5) AND \"DescricaoTipo\" IS NULL) OR (\"Tipo\" = 6 AND \"DescricaoTipo\" IS NOT NULL AND \"DescricaoTipo\" <> '' AND \"DescricaoTipo\" = btrim(\"DescricaoTipo\"))"
            : "(\"Tipo\" IN (1, 2, 3, 4, 5) AND \"DescricaoTipo\" IS NULL) OR (\"Tipo\" = 6 AND \"DescricaoTipo\" IS NOT NULL AND \"DescricaoTipo\" <> '' AND \"DescricaoTipo\" = trim(\"DescricaoTipo\"))";

        builder.Entity<IdentificacaoAnimal>(identificacao =>
        {
            identificacao.ToTable("IdentificacoesAnimal", table =>
            {
                table.HasCheckConstraint("CK_IdentificacoesAnimal_Tipo", "\"Tipo\" IN (1, 2, 3, 4, 5, 6)");
                table.HasCheckConstraint("CK_IdentificacoesAnimal_Valor_Canonical", identificacaoValorCanonicalConstraint);
                table.HasCheckConstraint("CK_IdentificacoesAnimal_DescricaoTipo_Semantics", identificacaoDescricaoTipoSemanticsConstraint);
            });
            identificacao.HasKey(entity => entity.Id);
            identificacao.Property(entity => entity.AnimalId).IsRequired();
            identificacao.Property(entity => entity.Tipo).HasConversion<int>().IsRequired();
            identificacao.Property(entity => entity.DescricaoTipo).HasMaxLength(100);
            identificacao.Property(entity => entity.Valor).IsRequired().HasMaxLength(128);
            identificacao.Property(entity => entity.Principal).IsRequired();
            identificacao.Property(entity => entity.DataAplicacao).HasColumnType("date");
            identificacao.Property(entity => entity.Observacao).HasMaxLength(1000);
            identificacao.Property(entity => entity.Ativo).IsRequired().HasDefaultValue(true);
            identificacao.Property(entity => entity.CreatedAtUtc).IsRequired();
            identificacao.Property(entity => entity.UpdatedAtUtc).IsRequired();
            identificacao.HasOne<Animal>().WithMany().HasForeignKey(entity => entity.AnimalId).IsRequired()
                .OnDelete(DeleteBehavior.Restrict);
        });

        var registroNumeroCanonicalConstraint = usesNpgsql
            ? "\"NumeroRegistro\" <> '' AND \"NumeroRegistro\" = btrim(\"NumeroRegistro\")"
            : "\"NumeroRegistro\" <> '' AND \"NumeroRegistro\" = trim(\"NumeroRegistro\")";
        builder.Entity<RegistroAnimal>(registro =>
        {
            registro.ToTable("RegistrosAnimal", table =>
            {
                table.HasCheckConstraint("CK_RegistrosAnimal_TipoRegistro", "\"TipoRegistro\" IN (1, 2)");
                table.HasCheckConstraint("CK_RegistrosAnimal_NumeroRegistro_Canonical", registroNumeroCanonicalConstraint);
                table.HasCheckConstraint("CK_RegistrosAnimal_DataFim", "\"DataFim\" IS NULL OR \"DataFim\" >= \"DataInicio\"");
                table.HasCheckConstraint("CK_RegistrosAnimal_Ativo_DataFim", "(\"Ativo\" AND \"DataFim\" IS NULL) OR (NOT \"Ativo\" AND \"DataFim\" IS NOT NULL)");
            });
            registro.HasKey(entity => entity.Id);
            registro.Property(entity => entity.AnimalId).IsRequired();
            registro.Property(entity => entity.TipoRegistro).HasConversion<int>().IsRequired();
            registro.Property(entity => entity.NumeroRegistro).HasMaxLength(128).IsRequired();
            registro.Property(entity => entity.Ativo).HasDefaultValue(true).IsRequired();
            registro.Property(entity => entity.DataInicio).HasColumnType("date").IsRequired();
            registro.Property(entity => entity.DataFim).HasColumnType("date");
            registro.Property(entity => entity.CreatedAtUtc).IsRequired();
            registro.Property(entity => entity.UpdatedAtUtc).IsRequired();
            registro.HasOne<Animal>().WithMany().HasForeignKey(entity => entity.AnimalId).IsRequired().OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<FiliacaoAnimal>(filiacao =>
        {
            filiacao.ToTable("FiliacoesAnimal", table => { table.HasCheckConstraint("CK_FiliacoesAnimal_Tipo", "\"TipoFiliacao\" IN (1, 2)"); table.HasCheckConstraint("CK_FiliacoesAnimal_Ativa_DataFim", "(\"Ativa\" AND \"DataFim\" IS NULL) OR (NOT \"Ativa\")"); });
            filiacao.HasKey(x => x.Id); filiacao.Property(x => x.AnimalId).IsRequired(); filiacao.Property(x => x.ProgenitorId).IsRequired(); filiacao.Property(x => x.TipoFiliacao).HasConversion<int>().IsRequired(); filiacao.Property(x => x.Ativa).IsRequired().HasDefaultValue(true); filiacao.Property(x => x.DataRegistro).HasColumnType("date"); filiacao.Property(x => x.DataFim).HasColumnType("date"); filiacao.Property(x => x.CreatedAtUtc).IsRequired(); filiacao.Property(x => x.UpdatedAtUtc).IsRequired();
            filiacao.HasOne<Animal>().WithMany().HasForeignKey(x=>x.AnimalId).OnDelete(DeleteBehavior.Restrict); filiacao.HasOne<Animal>().WithMany().HasForeignKey(x=>x.ProgenitorId).OnDelete(DeleteBehavior.Restrict);
            filiacao.HasIndex(x=>new {x.AnimalId,x.TipoFiliacao}).HasFilter("\"Ativa\" = TRUE").IsUnique().HasDatabaseName("UX_FiliacoesAnimal_Animal_Tipo_Ativa");
        });
        builder.Entity<Cruzamento>(cruzamento =>
        {
            cruzamento.ToTable("Cruzamentos", table =>
            {
                table.HasCheckConstraint("CK_Cruzamentos_Status", "\"Status\" IN (1, 2, 3, 4)");
                table.HasCheckConstraint("CK_Cruzamentos_Animais_Distintos", "\"MachoId\" <> \"FemeaId\"");
                table.HasCheckConstraint("CK_Cruzamentos_DataFim", "\"DataFim\" IS NULL OR \"DataInicio\" IS NULL OR \"DataFim\" >= \"DataInicio\"");
            });
            cruzamento.HasKey(x => x.Id); cruzamento.Property(x => x.MachoId).IsRequired(); cruzamento.Property(x => x.FemeaId).IsRequired(); cruzamento.Property(x => x.Status).HasConversion<int>().IsRequired(); cruzamento.Property(x => x.DataInicio).HasColumnType("date"); cruzamento.Property(x => x.DataFim).HasColumnType("date"); cruzamento.Property(x => x.Objetivo).HasMaxLength(500); cruzamento.Property(x => x.Observacao).HasMaxLength(2000); cruzamento.Property(x => x.CreatedAtUtc).IsRequired(); cruzamento.Property(x => x.UpdatedAtUtc).IsRequired();
            cruzamento.HasOne<Animal>().WithMany().HasForeignKey(x => x.MachoId).OnDelete(DeleteBehavior.Restrict); cruzamento.HasOne<Animal>().WithMany().HasForeignKey(x => x.FemeaId).OnDelete(DeleteBehavior.Restrict);
            cruzamento.HasIndex(x => new { x.Status, x.DataInicio }); cruzamento.HasIndex(x => x.MachoId); cruzamento.HasIndex(x => x.FemeaId);
        });
        builder.Entity<CicloReprodutivo>(ciclo =>
        {
            ciclo.ToTable("CiclosReprodutivos", table =>
            {
                table.HasCheckConstraint("CK_CiclosReprodutivos_Tipo", "\"Tipo\" IN (1, 2)");
                table.HasCheckConstraint("CK_CiclosReprodutivos_Status", "\"Status\" IN (1, 2, 3)");
                table.HasCheckConstraint("CK_CiclosReprodutivos_Quantidades", "(\"OvosPostos\" IS NULL OR \"OvosPostos\" >= 0) AND (\"OvosFerteis\" IS NULL OR \"OvosFerteis\" >= 0) AND (\"OvosIncubados\" IS NULL OR \"OvosIncubados\" >= 0) AND (\"OvosEclodidos\" IS NULL OR \"OvosEclodidos\" >= 0) AND (\"OvosInviaveis\" IS NULL OR \"OvosInviaveis\" >= 0) AND (\"Nascidos\" IS NULL OR \"Nascidos\" >= 0) AND (\"NascidosVivos\" IS NULL OR \"NascidosVivos\" >= 0) AND (\"NascidosMortos\" IS NULL OR \"NascidosMortos\" >= 0)");
                table.HasCheckConstraint("CK_CiclosReprodutivos_Pesos", "(\"PesoMedioOvoGramas\" IS NULL OR \"PesoMedioOvoGramas\" > 0) AND (\"PesoAoNascerGramas\" IS NULL OR \"PesoAoNascerGramas\" > 0)");
            });
            ciclo.HasKey(x => x.Id); ciclo.Property(x => x.CruzamentoId).IsRequired(); ciclo.Property(x => x.Tipo).HasConversion<int>().IsRequired(); ciclo.Property(x => x.Status).HasConversion<int>().IsRequired();
            ciclo.Property(x => x.DataPostura).HasColumnType("date"); ciclo.Property(x => x.DataInicioIncubacao).HasColumnType("date"); ciclo.Property(x => x.DataEclosao).HasColumnType("date"); ciclo.Property(x => x.PesoMedioOvoGramas).HasPrecision(10, 2); ciclo.Property(x => x.DataInicioGestacao).HasColumnType("date"); ciclo.Property(x => x.DataPrevistaParto).HasColumnType("date"); ciclo.Property(x => x.DataParto).HasColumnType("date"); ciclo.Property(x => x.PesoAoNascerGramas).HasPrecision(10, 2); ciclo.Property(x => x.Observacao).HasMaxLength(2000); ciclo.Property(x => x.CreatedAtUtc).IsRequired(); ciclo.Property(x => x.UpdatedAtUtc).IsRequired();
            ciclo.HasOne<Cruzamento>().WithMany().HasForeignKey(x => x.CruzamentoId).IsRequired().OnDelete(DeleteBehavior.Restrict); ciclo.HasIndex(x => new { x.CruzamentoId, x.Status }); ciclo.HasIndex(x => x.Tipo);
        });
        builder.Entity<ProducaoOvo>(producao =>
        {
            producao.ToTable("ProducoesOvos", table => table.HasCheckConstraint("CK_ProducoesOvos_PesoGramas", "\"PesoGramas\" > 0"));
            producao.HasKey(x => x.Id); producao.Property(x => x.AnimalId).IsRequired(); producao.Property(x => x.DataPostura).HasColumnType("date").IsRequired(); producao.Property(x => x.PesoGramas).HasPrecision(10, 2).IsRequired(); producao.Property(x => x.Observacao).HasMaxLength(2000); producao.Property(x => x.CreatedAtUtc).IsRequired(); producao.Property(x => x.UpdatedAtUtc).IsRequired();
            producao.HasOne<Animal>().WithMany().HasForeignKey(x => x.AnimalId).IsRequired().OnDelete(DeleteBehavior.Restrict); producao.HasIndex(x => new { x.AnimalId, x.DataPostura });
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
