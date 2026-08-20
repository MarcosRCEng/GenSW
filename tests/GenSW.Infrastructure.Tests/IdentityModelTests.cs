using GenSW.Domain.People;
using GenSW.Infrastructure.Identity;
using GenSW.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace GenSW.Infrastructure.Tests;

public sealed class IdentityModelTests : IDisposable
{
    private readonly GenSWDbContext context;

    public IdentityModelTests()
    {
        var options = new DbContextOptionsBuilder<GenSWDbContext>()
            .UseNpgsql("Host=localhost;Database=gensw_model_tests")
            .Options;

        context = new GenSWDbContext(options);
    }

    [Fact]
    public void GenSWDbContext_contains_the_Guid_Identity_model_and_preserves_default_table_names()
    {
        Assert.True(typeof(IdentityUser<Guid>).IsAssignableFrom(typeof(ApplicationUser)));
        Assert.True(
            typeof(IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>)
                .IsAssignableFrom(typeof(GenSWDbContext)));

        var userEntity = GetEntity<ApplicationUser>();
        var roleEntity = GetEntity<IdentityRole<Guid>>();

        Assert.Equal(typeof(Guid), userEntity.FindProperty(nameof(ApplicationUser.Id))!.ClrType);
        Assert.Equal("AspNetUsers", userEntity.GetTableName());
        Assert.Equal("AspNetRoles", roleEntity.GetTableName());
        Assert.NotNull(context.Model.FindEntityType(typeof(IdentityUserRole<Guid>)));
        Assert.NotNull(context.Model.FindEntityType(typeof(IdentityUserClaim<Guid>)));
        Assert.NotNull(context.Model.FindEntityType(typeof(IdentityUserLogin<Guid>)));
        Assert.NotNull(context.Model.FindEntityType(typeof(IdentityUserToken<Guid>)));
        Assert.NotNull(context.Model.FindEntityType(typeof(IdentityRoleClaim<Guid>)));
    }

    [Fact]
    public void ApplicationUser_has_one_required_unique_Pessoa_with_restricted_deletion()
    {
        var userEntity = GetEntity<ApplicationUser>();
        var pessoaEntity = GetEntity<Pessoa>();
        var pessoaId = userEntity.FindProperty(nameof(ApplicationUser.PessoaId))!;
        var relationship = userEntity.GetForeignKeys().Single(foreignKey =>
            foreignKey.Properties.Count == 1 && foreignKey.Properties[0] == pessoaId);

        Assert.Equal(typeof(Guid), pessoaId.ClrType);
        Assert.False(pessoaId.IsNullable);
        Assert.Equal(pessoaEntity, relationship.PrincipalEntityType);
        Assert.True(relationship.IsRequired);
        Assert.True(relationship.IsUnique);
        Assert.Equal(DeleteBehavior.Restrict, relationship.DeleteBehavior);
        Assert.Contains(
            userEntity.GetIndexes(),
            index => index.IsUnique && index.Properties.Count == 1 && index.Properties[0] == pessoaId);
        Assert.Null(relationship.PrincipalToDependent);
    }

    [Fact]
    public void ApplicationUser_keeps_IsActive_separate_from_Identity_lockout()
    {
        var userEntity = GetEntity<ApplicationUser>();

        Assert.NotNull(userEntity.FindProperty(nameof(ApplicationUser.IsActive)));
        Assert.NotNull(userEntity.FindProperty(nameof(ApplicationUser.LockoutEnd)));
        Assert.NotEqual(
            userEntity.FindProperty(nameof(ApplicationUser.IsActive)),
            userEntity.FindProperty(nameof(ApplicationUser.LockoutEnd)));
    }

    [Fact]
    public void RefreshSession_has_only_a_unique_canonical_hash_and_lookup_indexes()
    {
        var sessionEntity = GetEntity<RefreshSession>();
        var tokenHash = sessionEntity.FindProperty(nameof(RefreshSession.TokenHash))!;
        var userId = sessionEntity.FindProperty(nameof(RefreshSession.UserId))!;
        var familyId = sessionEntity.FindProperty(nameof(RefreshSession.FamilyId))!;

        Assert.False(tokenHash.IsNullable);
        Assert.Equal(typeof(byte[]), tokenHash.ClrType);
        Assert.Null(sessionEntity.FindProperty("Token"));
        Assert.Null(sessionEntity.FindProperty("RawToken"));
        Assert.Contains(
            sessionEntity.GetIndexes(),
            index => index.IsUnique && index.Properties.Count == 1 && index.Properties[0] == tokenHash);
        Assert.Contains(
            sessionEntity.GetIndexes(),
            index => index.Properties.Count == 1 && index.Properties[0] == userId);
        Assert.Contains(
            sessionEntity.GetIndexes(),
            index => index.Properties.Count == 1 && index.Properties[0] == familyId);
    }

    [Fact]
    public void RefreshSession_has_required_user_and_optional_replacement_relationships()
    {
        var sessionEntity = GetEntity<RefreshSession>();
        var userId = sessionEntity.FindProperty(nameof(RefreshSession.UserId))!;
        var replacementId = sessionEntity.FindProperty(nameof(RefreshSession.ReplacedBySessionId))!;
        var userRelationship = sessionEntity.GetForeignKeys().Single(foreignKey =>
            foreignKey.Properties.Count == 1 && foreignKey.Properties[0] == userId);
        var replacementRelationship = sessionEntity.GetForeignKeys().Single(foreignKey =>
            foreignKey.Properties.Count == 1 && foreignKey.Properties[0] == replacementId);

        Assert.Equal(GetEntity<ApplicationUser>(), userRelationship.PrincipalEntityType);
        Assert.True(userRelationship.IsRequired);
        Assert.Equal(DeleteBehavior.Restrict, userRelationship.DeleteBehavior);
        Assert.Equal(sessionEntity, replacementRelationship.PrincipalEntityType);
        Assert.False(replacementRelationship.IsRequired);
        Assert.True(replacementId.IsNullable);
        Assert.Equal(DeleteBehavior.Restrict, replacementRelationship.DeleteBehavior);
    }

    public void Dispose()
    {
        context.Dispose();
    }

    private IEntityType GetEntity<TEntity>()
    {
        return context.Model.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException($"Entity {typeof(TEntity).Name} is missing from the model.");
    }
}
