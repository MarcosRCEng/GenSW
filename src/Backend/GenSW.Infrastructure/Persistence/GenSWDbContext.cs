using GenSW.Domain.People;
using GenSW.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GenSW.Infrastructure.Persistence;

public sealed class GenSWDbContext(DbContextOptions<GenSWDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Pessoa> Pessoas => Set<Pessoa>();

    public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Pessoa>(pessoa =>
        {
            pessoa.ToTable("Pessoas");
            pessoa.HasKey(entity => entity.Id);
            pessoa.Property(entity => entity.Nome).IsRequired();
            pessoa.Property(entity => entity.CreatedAtUtc).IsRequired();
            pessoa.Property(entity => entity.UpdatedAtUtc).IsRequired();
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
