using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GenSW.Infrastructure.Catalog;

public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options)
    : IdentityDbContext<CatalogUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<CatalogAccount> Accounts => Set<CatalogAccount>();
    public DbSet<CatalogBinding> Bindings => Set<CatalogBinding>();
    public DbSet<CatalogSession> Sessions => Set<CatalogSession>();
    public DbSet<CatalogRefreshToken> RefreshTokens => Set<CatalogRefreshToken>();
    public DbSet<CatalogInitialOperator> InitialOperators => Set<CatalogInitialOperator>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<CatalogUser>().ToTable("CatalogUsers").Property(user => user.DisplayName).HasMaxLength(200);
        builder.Entity<CatalogUser>().Property(user => user.NormalizedUserName).IsRequired();
        builder.Entity<IdentityRole<Guid>>().ToTable("CatalogRoles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("CatalogUserRoles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("CatalogUserClaims");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("CatalogRoleClaims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("CatalogUserLogins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("CatalogUserTokens");

        builder.Entity<CatalogAccount>(account =>
        {
            account.ToTable("CatalogAccounts", table =>
            {
                table.HasCheckConstraint("CK_CatalogAccounts_State", "\"State\" BETWEEN 1 AND 4");
                table.HasCheckConstraint("CK_CatalogAccounts_Generation", "\"AccessGeneration\" > 0");
            });
            account.HasIndex(row => row.SubjectId).IsUnique();
            account.HasOne<CatalogUser>().WithMany().HasForeignKey(row => row.SubjectId).OnDelete(DeleteBehavior.Restrict);
            account.HasAlternateKey(row => new { row.Id, row.SubjectId });
        });

        builder.Entity<CatalogBinding>(binding =>
        {
            binding.ToTable("CatalogBindings", table =>
                table.HasCheckConstraint("CK_CatalogBindings_Revision", "\"Revision\" > 0"));
            binding.HasOne<CatalogAccount>().WithMany().HasForeignKey(row => row.AccountId).OnDelete(DeleteBehavior.Restrict);
            binding.HasIndex(row => row.AccountId).IsUnique().HasFilter("\"Published\"");
            binding.HasIndex(row => new { row.AccountId, row.Revision }).IsUnique();
            binding.HasAlternateKey(row => new { row.Id, row.AccountId, row.Revision });
            // Pending reservations also hold the destination until their owner reconciles them.
            binding.HasIndex(row => row.DatabaseId).IsUnique();
            binding.HasIndex(row => row.VolumeId).IsUnique();
            binding.HasIndex(row => row.VolumeRef).IsUnique();
            binding.Property(row => row.DatabaseName).HasMaxLength(63);
            binding.Property(row => row.DatabaseSecretRef).HasMaxLength(200);
            binding.Property(row => row.MaintenanceSecretRef).HasMaxLength(200);
            binding.Property(row => row.VolumeRef).HasMaxLength(200);
            binding.Property(row => row.ExpectedMigration).HasMaxLength(150);
            binding.Property(row => row.VerifiedMigration).HasMaxLength(150);
            binding.Property(row => row.LastMaintenanceFailure).HasMaxLength(80);
        });

        builder.Entity<CatalogSession>(session =>
        {
            session.ToTable("CatalogSessions", table => table.HasCheckConstraint("CK_CatalogSessions_Audience", """
                ("Audience" = 1 AND "AccountId" IS NOT NULL AND "BindingId" IS NOT NULL AND
                    "BindingRevision" IS NOT NULL AND "AccessGeneration" IS NOT NULL AND "AccessGeneration" > 0)
                OR ("Audience" = 2 AND "AccountId" IS NULL AND "BindingId" IS NULL AND
                    "BindingRevision" IS NULL AND "AccessGeneration" IS NULL)
                """));
            session.HasOne<CatalogUser>().WithMany().HasForeignKey(row => row.SubjectId).OnDelete(DeleteBehavior.Restrict);
            session.HasOne<CatalogAccount>().WithMany()
                .HasForeignKey(row => new { row.AccountId, row.SubjectId })
                .HasPrincipalKey(row => new { row.Id, row.SubjectId }).OnDelete(DeleteBehavior.Restrict);
            session.HasOne<CatalogBinding>().WithMany()
                .HasForeignKey(row => new { row.BindingId, row.AccountId, row.BindingRevision })
                .HasPrincipalKey(row => new { row.Id, row.AccountId, row.Revision }).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CatalogRefreshToken>(token =>
        {
            token.ToTable("CatalogRefreshTokens", table =>
                table.HasCheckConstraint("CK_CatalogRefreshTokens_Digest", "length(\"Digest\") = 32"));
            token.HasOne<CatalogSession>().WithMany().HasForeignKey(row => row.SessionId).OnDelete(DeleteBehavior.Restrict);
            token.HasIndex(row => row.Digest).IsUnique();
            token.HasIndex(row => row.SessionId).IsUnique().HasFilter("\"ConsumedAtUtc\" IS NULL");
        });

        builder.Entity<CatalogInitialOperator>(initialOperator =>
        {
            initialOperator.ToTable("CatalogInitialOperator", table =>
                table.HasCheckConstraint("CK_CatalogInitialOperator_Singleton", "\"Id\" = 1"));
            initialOperator.Property(row => row.Id).ValueGeneratedNever();
            initialOperator.HasOne<CatalogUser>().WithMany().HasForeignKey(row => row.SubjectId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
