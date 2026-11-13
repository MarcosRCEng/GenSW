using GenSW.Application.Optimization;
using GenSW.Domain.Formulation;
using GenSW.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

namespace GenSW.Infrastructure.Persistence;

internal static class OptimizationModelConfiguration
{
    internal static void Configure(ModelBuilder model)
    {
        model.Entity<OptimizationExecution>(entity =>
        {
            entity.ToTable("ExecucoesOtimizacao", table =>
            {
                table.HasCheckConstraint("CK_ExecucoesOtimizacao_Estado", "(\"Estado\" = 'EmCalculo' AND \"Revisao\" = 1) OR (\"Estado\" = 'Finalizada' AND \"Revisao\" = 2)");
                table.HasCheckConstraint("CK_ExecucoesOtimizacao_Prazo", "\"DeadlineUtc\" > \"CreatedAtUtc\"");
                table.HasCheckConstraint("CK_ExecucoesOtimizacao_Hash", "length(\"Hash\") = 64 AND length(\"Chave\") > 0");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.Chave).HasMaxLength(100);
            entity.Property(x => x.Hash).HasMaxLength(64);
            entity.Property(x => x.Estado).HasMaxLength(16);
            entity.Property(x => x.Fase).HasMaxLength(200);
            entity.Property(x => x.PedidoJson).HasColumnType("jsonb");
            entity.Property(x => x.EntradasJson).HasColumnType("jsonb");
            entity.Property(x => x.ModeloJson).HasColumnType("jsonb");
            entity.HasIndex(x => new { x.AutorId, x.Chave }).IsUnique();
            entity.HasIndex(x => new { x.Estado, x.DeadlineUtc, x.Id });
            entity.HasIndex(x => new { x.CreatedAtUtc, x.Id });
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AutorId).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<OptimizationResultRow>(entity =>
        {
            entity.ToTable("ResultadosOtimizacao", table =>
                table.HasCheckConstraint("CK_ResultadosOtimizacao_Desfecho", "\"Desfecho\" IN ('SolucaoViavel','InviabilidadeDemonstrada','DadosIncompletos','DadosIncompativeis','LimiteTempo','ErroAlgoritmo','Inconclusivo')"));
            entity.HasKey(x => x.ExecutionId);
            entity.Property(x => x.ExecutionId).ValueGeneratedNever();
            entity.Property(x => x.Desfecho).HasMaxLength(32);
            entity.Property(x => x.ConteudoJson).HasColumnType("jsonb");
            entity.HasIndex(x => x.Desfecho);
            entity.HasOne<OptimizationExecution>().WithOne().HasForeignKey<OptimizationResultRow>(x => x.ExecutionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<OptimizationDerivation>(entity =>
        {
            entity.ToTable("DerivacoesOtimizacao", table =>
                table.HasCheckConstraint("CK_DerivacoesOtimizacao_Hash", "length(\"Hash\") = 64 AND length(\"Chave\") > 0"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.Chave).HasMaxLength(100);
            entity.Property(x => x.Hash).HasMaxLength(64);
            entity.Property(x => x.ConteudoJson).HasColumnType("jsonb");
            entity.HasIndex(x => new { x.AutorId, x.Chave }).IsUnique();
            entity.HasIndex(x => x.VersaoId).IsUnique();
            entity.HasOne<OptimizationResultRow>().WithMany().HasForeignKey(x => x.ExecutionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<RecipeVersion>().WithMany().HasForeignKey(x => x.VersaoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Recipe>().WithMany().HasForeignKey(x => x.ReceitaId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AutorId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
