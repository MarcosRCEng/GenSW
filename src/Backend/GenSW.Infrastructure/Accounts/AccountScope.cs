using System.Collections.Concurrent;
using System.Data.Common;
using GenSW.Application.Accounts;
using GenSW.Infrastructure.Catalog;
using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GenSW.Infrastructure.Accounts;

public sealed record VerifiedAccountDestination(
    DbContextOptions<GenSWDbContext> DatabaseOptions, string PrivateRoot,
    Guid PessoaId, string ActorName, IReadOnlyList<string> Roles);

public interface IAccountDestinationVerifier
{
    Task<VerifiedAccountDestination> VerifyAsync(CatalogAccount account, CatalogBinding binding, CancellationToken ct);
}

public sealed class AccountRevocations
{
    private readonly ConcurrentDictionary<(Guid, long), CancellationTokenSource> tokens = new();

    internal CancellationToken Watch(Guid account, long generation) =>
        tokens.GetOrAdd((account, generation), _ => new CancellationTokenSource()).Token;

    internal async Task CancelAsync(Guid account, long generation)
    {
        if (tokens.TryRemove((account, generation), out var source))
            await source.CancelAsync();
    }
}

public sealed class AccountScope(
    CatalogDbContext catalog, CatalogBindings bindings, IAccountDestinationVerifier verifier,
    AccountRevocations revocations) : IErpAccountScope
{
    private ErpAccountContext? current;
    private VerifiedAccountDestination? destination;
    private Guid volumeId;

    public ErpAccountContext Current => current ?? throw new AccountUnavailableException();
    public DbContextOptions<GenSWDbContext> DatabaseOptions =>
        destination?.DatabaseOptions ?? throw new AccountUnavailableException();
    internal string PrivateRoot => destination?.PrivateRoot ?? throw new AccountUnavailableException();
    internal Guid VolumeId => destination is not null ? volumeId : throw new AccountUnavailableException();

    public async Task<ErpAccountContext> ResolveAsync(CatalogAccount account, CatalogBinding binding, CancellationToken ct)
    {
        var verified = await verifier.VerifyAsync(account, binding, ct);
        return Context(account, binding, verified);
    }

    public async Task EstablishAsync(CatalogAccount account, CatalogBinding binding, CancellationToken ct)
    {
        if (current is not null)
            throw new InvalidOperationException("An ERP scope has already been established.");

        var verified = await verifier.VerifyAsync(account, binding, ct);
        volumeId = binding.VolumeId;
        destination = verified;
        current = Context(account, binding, verified);
    }

    public async Task BindAsync(Guid accountId, Guid bindingId, long bindingRevision, long accessGeneration,
        Guid actorId, CancellationToken ct)
    {
        var resolved = await LoadAsync(new ErpAccountKey(accountId, bindingId, bindingRevision, accessGeneration, actorId), ct);
        await EstablishAsync(resolved.Account, resolved.Binding, ct);
    }

    public Task<IReadOnlyList<ErpAccountKey>> ListActiveAsync(CancellationToken ct) => TranslateAsync<IReadOnlyList<ErpAccountKey>>(async () =>
        await (from account in catalog.Accounts.AsNoTracking()
               join binding in catalog.Bindings.AsNoTracking() on account.Id equals binding.AccountId
               where account.State == CatalogAccountState.Active && account.Confirmed &&
                     binding.Published && binding.Ready && !binding.InMaintenance
               orderby account.Id
               select new ErpAccountKey(account.Id, binding.Id, binding.Revision, account.AccessGeneration, binding.ErpActorId))
            .ToListAsync(ct));

    public async Task RevalidateAsync(CancellationToken ct)
    {
        var expected = Current;
        var resolved = await LoadAsync(expected.Key, ct);
        var actual = await ResolveAsync(resolved.Account, resolved.Binding, ct);
        if (!actual.Roles.SequenceEqual(expected.Roles) || actual.PessoaId != expected.PessoaId)
            throw new AccountUnavailableException();
    }

    private Task<CatalogErpBinding> LoadAsync(ErpAccountKey key, CancellationToken ct) => TranslateAsync(async () =>
    {
        var subject = await (from account in catalog.Accounts.AsNoTracking()
                             join user in catalog.Users.AsNoTracking() on account.SubjectId equals user.Id
                             where account.Id == key.AccountId && user.IsActive
                             select (Guid?)account.SubjectId).SingleOrDefaultAsync(ct);
        var result = subject is Guid id ? await bindings.ResolveAsync(id, ct) : null;
        if (result is null || result.Account.Id != key.AccountId || result.Binding.Id != key.BindingId ||
            result.Binding.Revision != key.BindingRevision || result.Account.AccessGeneration != key.AccessGeneration ||
            result.Binding.ErpActorId != key.ActorId)
            throw new AccountUnavailableException();
        return result;
    });

    private ErpAccountContext Context(CatalogAccount account, CatalogBinding binding, VerifiedAccountDestination verified) =>
        new(account.Id, binding.Id, binding.Revision, account.AccessGeneration, binding.ErpActorId,
            verified.PessoaId, verified.ActorName, verified.Roles, revocations.Watch(account.Id, account.AccessGeneration));

    internal static async Task<T> TranslateAsync<T>(Func<Task<T>> action)
    {
        try { return await action(); }
        catch (Exception ex) when (ex is DbException or TimeoutException or IOException or UnauthorizedAccessException
                                      or ArgumentException or InvalidOperationException)
        {
            throw new AccountUnavailableException();
        }
    }
}

internal sealed class PostgreSqlAccountDestination(
    CatalogDbContext catalog, AccountResources resources) : IAccountDestinationVerifier
{
    public Task<VerifiedAccountDestination> VerifyAsync(CatalogAccount account, CatalogBinding binding, CancellationToken ct) =>
        AccountScope.TranslateAsync(async () =>
        {
            if (account.Id != binding.AccountId || account.Id == Guid.Empty || binding.Id == Guid.Empty ||
                binding.Revision < 1 || account.AccessGeneration < 1 || binding.DatabaseId == Guid.Empty ||
                binding.VolumeId == Guid.Empty || binding.ErpActorId == Guid.Empty)
                throw new AccountUnavailableException();

            var root = resources.Volume(binding);
            var otherVolumes = await catalog.Bindings.AsNoTracking()
                .Where(other => other.AccountId != account.Id)
                .Select(other => other.VolumeRef).ToListAsync(ct);
            foreach (var reference in otherVolumes)
            {
                var configured = resources.ConfiguredVolume(reference);
                if (string.IsNullOrWhiteSpace(configured) || !Path.IsPathFullyQualified(configured))
                    continue;
                if (PrivateVolume.Overlaps(root, PrivateVolume.CanonicalPath(configured)))
                    throw new AccountUnavailableException();
            }
            PrivateVolume.Verify(root, binding.VolumeId);

            var key = new ErpAccountKey(account.Id, binding.Id, binding.Revision, account.AccessGeneration, binding.ErpActorId);
            var options = new DbContextOptionsBuilder<GenSWDbContext>()
                .UseNpgsql(resources.RuntimeConnection(binding))
                .AddInterceptors(new AccountConnectionInterceptor(key))
                .Options;
            await using var db = new GenSWDbContext(options);
            await db.Database.OpenConnectionAsync(ct);
            await VerifyIdentityAsync((NpgsqlConnection)db.Database.GetDbConnection(), account, binding, ct);
            await VerifyHistoryAsync(db, binding.ExpectedMigration, ct);

            var actor = await db.Users.AsNoTracking().Where(x => x.Id == binding.ErpActorId && x.IsActive && x.Pessoa.Ativo)
                .Select(x => new { x.PessoaId, x.Pessoa.Nome }).SingleOrDefaultAsync(ct);
            if (actor is null)
                throw new AccountUnavailableException();

            var roles = await (from membership in db.UserRoles.AsNoTracking()
                               join role in db.Roles.AsNoTracking() on membership.RoleId equals role.Id
                               where membership.UserId == binding.ErpActorId && role.Name != null
                               orderby role.Name
                               select role.Name!).ToArrayAsync(ct);

            return new VerifiedAccountDestination(options, root, actor.PessoaId, actor.Nome, Array.AsReadOnly(roles));
        });

    internal static async Task VerifyIdentityAsync(NpgsqlConnection connection, CatalogAccount account,
        CatalogBinding binding, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT current_database(), database_id, account_id, binding_id, binding_revision, generation, enabled,
              has_schema_privilege(session_user, 'public', 'CREATE'),
              has_database_privilege(session_user, current_database(), 'CREATE'),
              pg_has_role(session_user, (SELECT nspowner FROM pg_namespace WHERE nspname = 'gensw_scope'), 'MEMBER'),
              (SELECT rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls
                 FROM pg_roles WHERE rolname = session_user),
              EXISTS (SELECT 1 FROM pg_database d WHERE d.datname <> current_database() AND d.datallowconn
                      AND has_database_privilege(session_user, d.oid, 'CONNECT')),
              EXISTS (SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
                      WHERE n.nspname = 'public' AND c.relkind IN ('r', 'p') AND c.relname <> '__EFMigrationsHistory'
                      AND NOT EXISTS (SELECT 1 FROM pg_trigger t WHERE t.tgrelid = c.oid
                          AND t.tgname = 'gensw_generation_guard' AND t.tgenabled IN ('O', 'A')
                          AND t.tgfoid = 'gensw_scope.guard_write()'::regprocedure))
            FROM gensw_scope.identity()
            """;
        await using var row = await command.ExecuteReaderAsync(ct);
        if (!await row.ReadAsync(ct) || row.GetString(0) != binding.DatabaseName ||
            row.GetGuid(1) != binding.DatabaseId || row.GetGuid(2) != account.Id || row.GetGuid(3) != binding.Id ||
            row.GetInt64(4) != binding.Revision || row.GetInt64(5) != account.AccessGeneration ||
            !row.GetBoolean(6) || row.GetBoolean(7) || row.GetBoolean(8) || row.GetBoolean(9) || row.GetBoolean(10) ||
            row.GetBoolean(11) || row.GetBoolean(12))
            throw new AccountUnavailableException();
    }

    internal static async Task VerifyHistoryAsync(GenSWDbContext db, string expectedMigration, CancellationToken ct)
    {
        var expected = db.Database.GetMigrations().ToArray();
        var actual = await db.Database.GetAppliedMigrationsAsync(ct);
        if (expected.Length == 0 || expected[^1] != expectedMigration || !expected.SequenceEqual(actual))
            throw new AccountUnavailableException();
    }
}
