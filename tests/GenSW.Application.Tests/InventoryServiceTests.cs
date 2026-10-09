using System.Reflection;
using GenSW.Application.Authentication;
using GenSW.Application.Inventory;
using GenSW.Domain.Inventory;
using Xunit;

namespace GenSW.Application.Tests;

public sealed class InventoryServiceTests
{
    private static readonly Guid Actor = Guid.NewGuid();
    private sealed class Authentication : IAuthenticationSessionService
    {
        public bool Active = true;
        public bool Admin { get; set; }
        public int Reads;
        public Task<CurrentUserResult?> GetCurrentUserAsync(Guid id, CancellationToken ct) { Reads++; return Task.FromResult<CurrentUserResult?>(Active ? new() { UserId = id, Nome = "Teste", Roles = Admin ? ["Admin"] : [] } : null); }
        public Task<AuthenticationSessionResult?> LoginAsync(string userName, string password, CancellationToken ct) => throw new NotSupportedException();
        public Task<AuthenticationSessionResult?> RefreshAsync(string refreshToken, CancellationToken ct) => throw new NotSupportedException();
        public Task LogoutAsync(string? refreshToken, CancellationToken ct) => throw new NotSupportedException();
    }
    public class RepositoryProxy : DispatchProxy
    {
        public Func<string, object?[], object?> Handler = null!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Handler(targetMethod!.Name, args!);
    }
    private sealed class Scope : IInventoryScope
    {
        public bool Committed;
        public Task CommitAsync(CancellationToken ct) { Committed = true; return Task.CompletedTask; }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
    private sealed class Fixture
    {
        public readonly Authentication Auth = new();
        public readonly List<object> Added = [];
        public readonly Scope Mutation = new();
        public readonly Dictionary<Guid, LocalEstoque> Locals = [];
        public InventoryService Service;
        public Func<Task<IInventoryScope>>? WaitBeforeMutation;
        private long sequence;
        public Fixture()
        {
            var repo = DispatchProxy.Create<IInventoryRepository, RepositoryProxy>(); ((RepositoryProxy)(object)repo).Handler = Handle;
            Service = new(repo, Auth, TimeProvider.System);
        }
        private object? Handle(string name, object?[] args) => name switch
        {
            "BeginMutationAsync" => WaitBeforeMutation?.Invoke() ?? Task.FromResult<IInventoryScope>(Mutation),
            "BeginReadAsync" => Task.FromResult<IInventoryScope>(new Scope()),
            "SaveAsync" => Task.CompletedTask,
            "ClearTracking" => null,
            "NextSequenceAsync" => Task.FromResult(++sequence),
            "LastSequenceAsync" => Task.FromResult(sequence),
            "ReplayAsync" => Task.FromResult(Added.OfType<ComandoEstoque>().SingleOrDefault(x => x.AutorId == (Guid)args[0]! && x.Operacao == (string)args[1]! && x.RecursoId == (Guid)args[2]! && x.Chave == (string)args[3]!)),
            "Add" => Add(args[0]!),
            "LocalAsync" => Task.FromResult(Locals.GetValueOrDefault((Guid)args[0]!)),
            "LocalViewAsync" => Task.FromResult<LocalView?>(Locals.TryGetValue((Guid)args[0]!, out var local) ? new(local.Id, local.Codigo, local.Nome, local.Descricao, local.PropriedadeId, null, local.Finalidade, local.Ativo, local.Revisao, local.CreatedAtUtc, local.UpdatedAtUtc) : null),
            _ => throw new NotSupportedException("Unexpected repository operation: " + name)
        };
        private object? Add(object entity) { Added.Add(entity); if (entity is LocalEstoque local) Locals[local.Id] = local; return null; }
    }
    [Fact]
    public async Task Author_is_checked_after_waiting_for_the_mutation_scope()
    {
        var f = new Fixture(); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.WaitBeforeMutation = async () => { entered.SetResult(); await release.Task; return f.Mutation; };
        var work = f.Service.CreateLocalAsync(new("LOCAL", "Local"), "key", Actor, default);
        await entered.Task; f.Auth.Active = false; release.SetResult();
        var error = await Assert.ThrowsAsync<InventoryException>(() => work); Assert.Equal(401, error.Status); Assert.Empty(f.Added); Assert.False(f.Mutation.Committed);
    }
    [Fact]
    public async Task Removed_admin_role_denies_state_change_before_any_record_is_read()
    {
        var f = new Fixture(); var error = await Assert.ThrowsAsync<InventoryException>(() => f.Service.LocalStateAsync(Guid.NewGuid(), "Ativo", new(1, "Motivo", "Evidência", Ativo: false), "key", Actor, default));
        Assert.Equal(403, error.Status); Assert.Empty(f.Added); Assert.Equal(1, f.Auth.Reads);
    }
    [Fact]
    public async Task Metadata_change_cannot_alter_status_or_purpose_without_a_separate_command()
    {
        var f = new Fixture(); await f.Service.CreateLocalAsync(new("LOCAL", "Local"), "create", Actor, default); var local = Assert.Single(f.Locals.Values); var count = f.Added.Count;
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.UpdateLocalAsync(local.Id, new(local.Codigo, "Nome", Finalidade: "Segregacao", VersaoEsperada: 1), "edit", Actor, default));
        Assert.Equal("Ordinario", local.Finalidade); Assert.Equal(count, f.Added.Count);
    }
    [Fact]
    public async Task Invalid_metadata_replay_still_obeys_request_shape_contract()
    {
        var f = new Fixture(); await Assert.ThrowsAsync<ArgumentException>(() => f.Service.CreateLocalAsync(new("", "Local"), "key", Actor, default)); Assert.Empty(f.Added);
        await Assert.ThrowsAsync<ArgumentNullException>(() => f.Service.CreateLocalAsync(null!, "key", Actor, default));
    }
    [Fact]
    public async Task Replay_preserves_response_even_after_the_current_record_changes()
    {
        var f = new Fixture(); var command = new LocalCommand("LOCAL", "Nome original");
        var first = await f.Service.CreateLocalAsync(command, "key", Actor, default); var local = Assert.Single(f.Locals.Values);
        local.Update(local.Codigo, "Nome posterior", null, null, local.Revisao, DateTimeOffset.UtcNow);
        var replay = await f.Service.CreateLocalAsync(command, "key", Actor, default);
        Assert.True(replay.Replayed); Assert.Equal(first.RespostaJson, replay.RespostaJson); Assert.Equal(first.Location, replay.Location); Assert.Equal(201, replay.StatusHttp);
        Assert.Single(f.Added.OfType<ComandoEstoque>()); Assert.Single(f.Added.OfType<HistoricoEstoque>());
    }
    [Fact]
    public async Task Divergent_payload_conflicts_without_new_audit_or_registration()
    {
        var f = new Fixture(); await f.Service.CreateLocalAsync(new("LOCAL", "Original"), "key", Actor, default);
        var count = f.Added.Count; var error = await Assert.ThrowsAsync<InventoryException>(() => f.Service.CreateLocalAsync(new("LOCAL", "Changed"), "key", Actor, default));
        Assert.Equal("idempotencia_divergente", error.Code); Assert.Equal(count, f.Added.Count);
    }
    [Fact]
    public async Task Replay_still_requires_an_active_current_actor()
    {
        var f = new Fixture(); var c = new LocalCommand("LOCAL", "Original"); await f.Service.CreateLocalAsync(c, "key", Actor, default); f.Auth.Active = false;
        var error = await Assert.ThrowsAsync<InventoryException>(() => f.Service.CreateLocalAsync(c, "key", Actor, default)); Assert.Equal(401, error.Status); Assert.Single(f.Added.OfType<ComandoEstoque>());
    }
    [Fact]
    public async Task Exceptional_lot_creation_replay_preserves_original_admin_requirement()
    {
        var f = new Fixture(); var c = new LoteCommand(Guid.NewGuid(), "LOTE", "Inventário", "Fonte", Actor);
        f.Added.Add(new ComandoEstoque(Guid.NewGuid(), Actor, "LoteCriacao", Guid.Empty, "key", InventoryService.CanonicalHash(c), 201, "/lotes/old", "{}", null, Guid.NewGuid(), DateTimeOffset.UtcNow, true));
        var error = await Assert.ThrowsAsync<InventoryException>(() => f.Service.CreateLoteAsync(c, "key", Actor, default)); Assert.Equal(403, error.Status); Assert.Single(f.Added);
    }
    [Fact]
    public void Canonical_payload_normalizes_only_semantic_decimal_fields()
    {
        var c = new InventoryMovementCommand(Guid.NewGuid(), "1", "kg", Actor, "001", new(1, 1, 1, 0), Fonte: "001");
        Assert.Equal(InventoryService.CanonicalHash(c), InventoryService.CanonicalHash(c with { Quantidade = "1.000000" }));
        Assert.NotEqual(InventoryService.CanonicalHash(c), InventoryService.CanonicalHash(c with { Fonte = "1" }));
        Assert.NotEqual(InventoryService.CanonicalHash(c), InventoryService.CanonicalHash(c with { Motivo = "1" }));
        Assert.NotEqual(InventoryService.CanonicalHash(c), InventoryService.CanonicalHash(c with { VersoesEsperadas = new(1, 1, 1, 1) }));
    }
    [Theory]
    [InlineData("+1")]
    [InlineData(" 1")]
    [InlineData("1e2")]
    [InlineData("-1")]
    [InlineData("01")]
    [InlineData("9223372036854775808")]
    public void Sequence_contract_rejects_noncanonical_or_overflow_strings(string seq) => Assert.Throws<ArgumentException>(() => new InventoryQuery(SeqAte: seq).Validate());
}
