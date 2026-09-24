using GenSW.Application.Animals;
using GenSW.Application.Animals.Identificacoes;
using GenSW.Domain.Animals;
using Xunit;

namespace GenSW.Application.Tests;

public sealed class IdentificacaoAnimalServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 12, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Nested_reads_require_the_parent_and_keep_a_missing_or_mismatched_row_in_the_owner_scope()
    {
        var repository = new FakeRepository();
        var unknownAnimalId = Guid.NewGuid();
        var service = CreateService(repository);

        await Assert.ThrowsAsync<AnimalNotFoundException>(() =>
            service.ListByAnimalAsync(unknownAnimalId, new IdentificacaoAnimalListQuery()));
        await Assert.ThrowsAsync<AnimalNotFoundException>(() =>
            service.GetByAnimalAsync(unknownAnimalId, Guid.NewGuid()));

        var animal = repository.AddAnimal();
        await Assert.ThrowsAsync<IdentificacaoAnimalNotFoundException>(() =>
            service.GetByAnimalAsync(animal.Id, Guid.NewGuid()));

        var otherAnimal = repository.AddAnimal();
        var otherRow = repository.AddIdentification(otherAnimal.Id, TipoIdentificacaoAnimal.Brinco, "B-1");
        var mismatch = await Assert.ThrowsAsync<IdentificacaoAnimalNotFoundException>(() =>
            service.GetByAnimalAsync(animal.Id, otherRow.Id));

        Assert.Equal(animal.Id, mismatch.AnimalId);
        Assert.Equal(otherRow.Id, mismatch.IdentificacaoId);
    }

    [Theory]
    [InlineData(TipoIdentificacaoAnimal.Anilha, null, " A-1 ")]
    [InlineData(TipoIdentificacaoAnimal.Outro, " Colar ", " X-1 ")]
    public async Task Create_rejects_duplicate_normalized_keys_before_adding(
        TipoIdentificacaoAnimal tipo, string? descricaoTipo, string valor)
    {
        var repository = new FakeRepository { Duplicate = true };
        var animal = repository.AddAnimal();
        var service = CreateService(repository);

        var exception = await Assert.ThrowsAsync<IdentificacaoAnimalDuplicateException>(() => service.CreateAsync(
            animal.Id, new CreateIdentificacaoAnimalCommand(tipo, descricaoTipo, valor, false, null, null)));

        Assert.Equal(IdentificacaoAnimalDuplicateConflictSource.PreCheck, exception.ConflictSource);
        Assert.Equal(tipo, exception.Tipo);
        Assert.Equal(descricaoTipo?.Trim(), exception.DescricaoTipo);
        Assert.Equal(valor.Trim(), exception.Valor);
        Assert.Empty(repository.Identifications);
        Assert.Equal((tipo, descricaoTipo?.Trim(), valor.Trim()), repository.LastDuplicateCheck);
    }

    [Fact]
    public async Task Create_non_principal_checks_the_parent_and_persists_without_a_mutation_scope()
    {
        var repository = new FakeRepository();
        var animal = repository.AddAnimal();
        var service = CreateService(repository);

        var result = await service.CreateAsync(animal.Id,
            new CreateIdentificacaoAnimalCommand(TipoIdentificacaoAnimal.Microchip, null, "  MC-01  ", false,
                new DateOnly(2026, 9, 1), "  aplicada  "));

        Assert.Equal("MC-01", result.Valor);
        Assert.False(result.Principal);
        Assert.Equal("aplicada", result.Observacao);
        Assert.Equal(0, repository.BeginCalls);
        Assert.Equal(1, repository.SaveCalls);
    }

    [Fact]
    public async Task Create_principal_clears_and_flushes_the_old_principal_before_adding_and_flushing_the_new_one()
    {
        var repository = new FakeRepository();
        var animal = repository.AddAnimal();
        var oldPrincipal = repository.AddIdentification(animal.Id, TipoIdentificacaoAnimal.Anilha, "A-1", true);
        var service = CreateService(repository);

        var created = await service.CreateAsync(animal.Id,
            new CreateIdentificacaoAnimalCommand(TipoIdentificacaoAnimal.Brinco, null, "B-1", true, null, null));

        Assert.False(oldPrincipal.Principal);
        Assert.True(created.Principal);
        Assert.True(repository.ScopeCommitted);
        Assert.Equal(["Begin", "Lock", "Duplicate", "CurrentPrincipal", "Save1", "Add", "Save2", "Commit"],
            repository.Events);
        Assert.False(repository.Snapshots[0].Committed);
        Assert.DoesNotContain(repository.Snapshots[0].Rows, row => row.Ativo && row.Principal);
        Assert.Single(repository.Snapshots[1].Rows, row => row.Id == created.Id && row.Ativo && row.Principal);
    }

    [Fact]
    public async Task Create_principal_rolls_back_the_old_principal_when_the_insert_flush_fails()
    {
        var failure = new InvalidOperationException("second flush failed");
        var repository = new FakeRepository { SaveFailureOnCall = 2, SaveFailure = failure };
        var animal = repository.AddAnimal();
        var oldPrincipal = repository.AddIdentification(animal.Id, TipoIdentificacaoAnimal.Anilha, "A-1", true);
        var service = CreateService(repository);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(animal.Id,
            new CreateIdentificacaoAnimalCommand(TipoIdentificacaoAnimal.Brinco, null, "B-1", true, null, null)));

        Assert.Same(failure, thrown);
        Assert.False(repository.ScopeCommitted);
        Assert.True(oldPrincipal.Principal);
        Assert.Single(repository.Identifications);
        Assert.Equal("Rollback", repository.Events[^1]);
        Assert.DoesNotContain(repository.Snapshots[0].Rows, row => row.Ativo && row.Principal);
    }

    [Fact]
    public async Task Set_principal_switches_in_two_flushes_and_rolls_back_both_changes_when_the_second_fails()
    {
        var repository = new FakeRepository();
        var animal = repository.AddAnimal();
        var oldPrincipal = repository.AddIdentification(animal.Id, TipoIdentificacaoAnimal.Anilha, "A-1", true);
        var target = repository.AddIdentification(animal.Id, TipoIdentificacaoAnimal.Brinco, "B-1");
        var service = CreateService(repository);

        var result = await service.SetPrincipalAsync(animal.Id, target.Id,
            new SetIdentificacaoAnimalPrincipalCommand(true));

        Assert.False(oldPrincipal.Principal);
        Assert.True(result.Principal);
        Assert.Equal(["Begin", "Lock", "Target", "CurrentPrincipal", "Save1", "Save2", "Commit"], repository.Events);
        Assert.False(repository.Snapshots[0].Committed);
        Assert.DoesNotContain(repository.Snapshots[0].Rows, row => row.Ativo && row.Principal);
        Assert.True(repository.ScopeCommitted);

        var rollbackRepository = new FakeRepository
        {
            SaveFailureOnCall = 2,
            SaveFailure = new InvalidOperationException("second flush failed")
        };
        var rollbackAnimal = rollbackRepository.AddAnimal();
        var rollbackOld = rollbackRepository.AddIdentification(rollbackAnimal.Id, TipoIdentificacaoAnimal.Anilha, "A-1", true);
        var rollbackTarget = rollbackRepository.AddIdentification(rollbackAnimal.Id, TipoIdentificacaoAnimal.Brinco, "B-1");

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService(rollbackRepository).SetPrincipalAsync(
            rollbackAnimal.Id, rollbackTarget.Id, new SetIdentificacaoAnimalPrincipalCommand(true)));

        Assert.True(rollbackOld.Principal);
        Assert.False(rollbackTarget.Principal);
        Assert.False(rollbackRepository.ScopeCommitted);
        Assert.Equal("Rollback", rollbackRepository.Events[^1]);
    }

    [Fact]
    public async Task Set_principal_is_idempotent_for_the_current_active_row_and_removal_uses_one_flush()
    {
        var repository = new FakeRepository();
        var animal = repository.AddAnimal();
        var current = repository.AddIdentification(animal.Id, TipoIdentificacaoAnimal.Anilha, "A-1", true);
        var service = CreateService(repository);

        var unchanged = await service.SetPrincipalAsync(animal.Id, current.Id,
            new SetIdentificacaoAnimalPrincipalCommand(true));

        Assert.True(unchanged.Principal);
        Assert.Single(repository.Snapshots);
        Assert.Single(repository.Snapshots[0].Rows, row => row.Ativo && row.Principal);
        Assert.Equal(["Begin", "Lock", "Target", "CurrentPrincipal", "Save1", "Commit"], repository.Events);

        repository.ResetObservations();
        var removed = await service.SetPrincipalAsync(animal.Id, current.Id,
            new SetIdentificacaoAnimalPrincipalCommand(false));

        Assert.False(removed.Principal);
        Assert.Equal(["Begin", "Lock", "Target", "Save1", "Commit"], repository.Events);
    }

    [Fact]
    public async Task Set_principal_refuses_an_inactive_target_without_activating_or_clearing_the_current_principal()
    {
        var repository = new FakeRepository();
        var animal = repository.AddAnimal();
        var current = repository.AddIdentification(animal.Id, TipoIdentificacaoAnimal.Anilha, "A-1", true);
        var inactive = repository.AddIdentification(animal.Id, TipoIdentificacaoAnimal.Brinco, "B-1");
        inactive.Inativar(Now);
        var service = CreateService(repository);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SetPrincipalAsync(animal.Id, inactive.Id,
            new SetIdentificacaoAnimalPrincipalCommand(true)));

        Assert.True(current.Principal);
        Assert.False(inactive.Ativo);
        Assert.False(inactive.Principal);
        Assert.False(repository.ScopeCommitted);
        Assert.Equal(0, repository.SaveCalls);
    }

    [Fact]
    public async Task Set_ativo_inactivates_and_clears_a_principal_while_reactivation_never_promotes()
    {
        var repository = new FakeRepository();
        var animal = repository.AddAnimal();
        var identification = repository.AddIdentification(animal.Id, TipoIdentificacaoAnimal.Anilha, "A-1", true);
        var service = CreateService(repository);

        var inactive = await service.SetAtivoAsync(animal.Id, identification.Id,
            new SetIdentificacaoAnimalAtivoCommand(false));

        Assert.False(inactive.Ativo);
        Assert.False(inactive.Principal);
        Assert.Equal(["Begin", "Lock", "Target", "Save1", "Commit"], repository.Events);

        repository.ResetObservations();
        var active = await service.SetAtivoAsync(animal.Id, identification.Id,
            new SetIdentificacaoAnimalAtivoCommand(true));

        Assert.True(active.Ativo);
        Assert.False(active.Principal);
        Assert.Equal(0, repository.BeginCalls);
        Assert.Equal(1, repository.SaveCalls);
    }

    [Fact]
    public async Task Metadata_update_distinguishes_absent_value_and_explicit_null_and_rejects_an_empty_patch()
    {
        var repository = new FakeRepository();
        var animal = repository.AddAnimal();
        var identification = repository.AddIdentification(animal.Id, TipoIdentificacaoAnimal.Outro, "X-1",
            descricaoTipo: "Colar", dataAplicacao: new DateOnly(2026, 9, 1), observacao: "original");
        var service = CreateService(repository);

        var dateOnly = await service.UpdateMetadataAsync(animal.Id, identification.Id,
            new UpdateIdentificacaoAnimalMetadataCommand(true, null, false, "ignorada"));
        Assert.Null(dateOnly.DataAplicacao);
        Assert.Equal("original", dateOnly.Observacao);

        var observationOnly = await service.UpdateMetadataAsync(animal.Id, identification.Id,
            new UpdateIdentificacaoAnimalMetadataCommand(false, new DateOnly(2026, 9, 9), true, null));
        Assert.Null(observationOnly.DataAplicacao);
        Assert.Null(observationOnly.Observacao);

        var bothValues = await service.UpdateMetadataAsync(animal.Id, identification.Id,
            new UpdateIdentificacaoAnimalMetadataCommand(true, new DateOnly(2026, 9, 9), true, "  nova  "));
        Assert.Equal(new DateOnly(2026, 9, 9), bothValues.DataAplicacao);
        Assert.Equal("nova", bothValues.Observacao);

        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateMetadataAsync(animal.Id, identification.Id,
            new UpdateIdentificacaoAnimalMetadataCommand(false, null, false, null)));
    }

    [Fact]
    public async Task Mutations_keep_missing_or_mismatched_rows_in_the_nested_owner_scope()
    {
        var repository = new FakeRepository();
        var animal = repository.AddAnimal();
        var otherAnimal = repository.AddAnimal();
        var otherRow = repository.AddIdentification(otherAnimal.Id, TipoIdentificacaoAnimal.Anilha, "A-1");
        var service = CreateService(repository);

        await Assert.ThrowsAsync<IdentificacaoAnimalNotFoundException>(() => service.UpdateMetadataAsync(
            animal.Id, otherRow.Id, new UpdateIdentificacaoAnimalMetadataCommand(true, null, false, null)));
        await Assert.ThrowsAsync<IdentificacaoAnimalNotFoundException>(() => service.SetAtivoAsync(
            animal.Id, otherRow.Id, new SetIdentificacaoAnimalAtivoCommand(true)));
        await Assert.ThrowsAsync<IdentificacaoAnimalNotFoundException>(() => service.SetPrincipalAsync(
            animal.Id, otherRow.Id, new SetIdentificacaoAnimalPrincipalCommand(true)));
    }

    [Fact]
    public async Task Lists_validate_boundaries_and_delegate_nested_and_global_queries()
    {
        var repository = new FakeRepository();
        var animal = repository.AddAnimal();
        var nestedPage = new PagedIdentificacaoAnimalResult([], 2, 10, 0, 0);
        var globalPage = new PagedIdentificacaoAnimalGlobalResult([], 3, 20, 0, 0);
        repository.NestedPage = nestedPage;
        repository.GlobalPage = globalPage;
        var service = CreateService(repository);
        var nestedQuery = new IdentificacaoAnimalListQuery(2, 10, TipoIdentificacaoAnimal.Anilha, "A", true, false);
        var globalQuery = new IdentificacaoAnimalListQuery(3, 20, TipoIdentificacaoAnimal.Outro, "X", false, null);

        Assert.Same(nestedPage, await service.ListByAnimalAsync(animal.Id, nestedQuery));
        Assert.Same(nestedQuery, repository.LastNestedQuery);
        Assert.Same(globalPage, await service.ListGlobalAsync(globalQuery));
        Assert.Same(globalQuery, repository.LastGlobalQuery);
        Assert.Equal(1, repository.AnimalExistsCalls);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.ListByAnimalAsync(animal.Id, new IdentificacaoAnimalListQuery(0)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.ListGlobalAsync(new IdentificacaoAnimalListQuery(PageSize: 0)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.ListGlobalAsync(new IdentificacaoAnimalListQuery(PageSize: 101)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.ListGlobalAsync(new IdentificacaoAnimalListQuery(Tipo: (TipoIdentificacaoAnimal)99)));
    }

    [Fact]
    public async Task Generic_repository_failures_are_not_translated_by_the_service()
    {
        var failure = new InvalidOperationException("database unavailable");
        var repository = new FakeRepository { SaveFailureOnCall = 1, SaveFailure = failure };
        var animal = repository.AddAnimal();
        var service = CreateService(repository);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(animal.Id,
            new CreateIdentificacaoAnimalCommand(TipoIdentificacaoAnimal.Anilha, null, "A-1", false, null, null)));

        Assert.Same(failure, thrown);
    }

    private static IdentificacaoAnimalService CreateService(FakeRepository repository) =>
        new(repository, new FixedTimeProvider(Now));

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeRepository : IIdentificacaoAnimalRepository
    {
        private readonly List<Animal> animals = [];
        private Transaction? transaction;

        public List<IdentificacaoAnimal> Identifications { get; } = [];
        public List<string> Events { get; } = [];
        public List<DurableSnapshot> Snapshots { get; } = [];
        public bool Duplicate { get; set; }
        public int SaveFailureOnCall { get; set; }
        public Exception? SaveFailure { get; set; }
        public int BeginCalls { get; private set; }
        public int SaveCalls { get; private set; }
        public int AnimalExistsCalls { get; private set; }
        public bool ScopeCommitted { get; private set; }
        public (TipoIdentificacaoAnimal Tipo, string? DescricaoTipo, string Valor)? LastDuplicateCheck { get; private set; }
        public IdentificacaoAnimalListQuery? LastNestedQuery { get; private set; }
        public IdentificacaoAnimalListQuery? LastGlobalQuery { get; private set; }
        public PagedIdentificacaoAnimalResult NestedPage { get; set; } = new([], 1, 25, 0, 0);
        public PagedIdentificacaoAnimalGlobalResult GlobalPage { get; set; } = new([], 1, 25, 0, 0);

        public Animal AddAnimal()
        {
            var animal = Animal.Criar($"AN-{animals.Count + 1:000000}", null, Guid.NewGuid(), null, null,
                SexoAnimal.Femea, null, EscopoAnimal.Operacional, DateOnly.FromDateTime(Now.UtcDateTime), Now);
            animals.Add(animal);
            return animal;
        }

        public IdentificacaoAnimal AddIdentification(Guid animalId, TipoIdentificacaoAnimal tipo, string valor,
            bool principal = false, string? descricaoTipo = null, DateOnly? dataAplicacao = null,
            string? observacao = null)
        {
            var identification = IdentificacaoAnimal.Criar(animalId, tipo, descricaoTipo, valor, principal,
                dataAplicacao, observacao, Now);
            Identifications.Add(identification);
            return identification;
        }

        public void ResetObservations()
        {
            Events.Clear();
            Snapshots.Clear();
            BeginCalls = 0;
            SaveCalls = 0;
            AnimalExistsCalls = 0;
            ScopeCommitted = false;
        }

        public Task<IIdentificacaoAnimalMutationScope> BeginMutationAsync(CancellationToken cancellationToken = default)
        {
            BeginCalls++;
            Events.Add("Begin");
            transaction = new Transaction(this, Identifications.ToArray(), Identifications.ToDictionary(
                item => item.Id, item => new RowState(item.Id, item.Ativo, item.Principal)));
            return Task.FromResult<IIdentificacaoAnimalMutationScope>(transaction);
        }

        public Task<Animal?> LockAnimalAsync(Guid animalId, CancellationToken cancellationToken = default)
        {
            Events.Add("Lock");
            return Task.FromResult(animals.SingleOrDefault(item => item.Id == animalId));
        }

        public Task<bool> AnimalExistsAsync(Guid animalId, CancellationToken cancellationToken = default)
        {
            AnimalExistsCalls++;
            return Task.FromResult(animals.Any(item => item.Id == animalId));
        }

        public Task AddAsync(IdentificacaoAnimal identificacao, CancellationToken cancellationToken = default)
        {
            Events.Add("Add");
            Identifications.Add(identificacao);
            return Task.CompletedTask;
        }

        public Task<IdentificacaoAnimal?> GetByAnimalForUpdateAsync(Guid animalId, Guid identificacaoId,
            CancellationToken cancellationToken = default)
        {
            Events.Add("Target");
            return Task.FromResult(Identifications.SingleOrDefault(item =>
                item.AnimalId == animalId && item.Id == identificacaoId));
        }

        public Task<IdentificacaoAnimalResult?> GetByAnimalReadOnlyAsync(Guid animalId, Guid identificacaoId,
            CancellationToken cancellationToken = default)
        {
            var item = Identifications.SingleOrDefault(candidate =>
                candidate.AnimalId == animalId && candidate.Id == identificacaoId);
            return Task.FromResult(item is null ? null : ToResult(item));
        }

        public Task<PagedIdentificacaoAnimalResult> ListByAnimalAsync(Guid animalId,
            IdentificacaoAnimalListQuery query, CancellationToken cancellationToken = default)
        {
            LastNestedQuery = query;
            return Task.FromResult(NestedPage);
        }

        public Task<PagedIdentificacaoAnimalGlobalResult> ListGlobalAsync(IdentificacaoAnimalListQuery query,
            CancellationToken cancellationToken = default)
        {
            LastGlobalQuery = query;
            return Task.FromResult(GlobalPage);
        }

        public Task<bool> HasDuplicateAsync(TipoIdentificacaoAnimal tipo, string? descricaoTipo, string valor,
            CancellationToken cancellationToken = default)
        {
            Events.Add("Duplicate");
            LastDuplicateCheck = (tipo, descricaoTipo, valor);
            return Task.FromResult(Duplicate);
        }

        public Task<IdentificacaoAnimal?> GetCurrentPrincipalForUpdateAsync(Guid animalId,
            CancellationToken cancellationToken = default)
        {
            Events.Add("CurrentPrincipal");
            return Task.FromResult(Identifications.SingleOrDefault(item =>
                item.AnimalId == animalId && item.Ativo && item.Principal));
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCalls++;
            Events.Add($"Save{SaveCalls}");
            Snapshots.Add(new DurableSnapshot(SaveCalls, ScopeCommitted,
                Identifications.Select(item => new RowState(item.Id, item.Ativo, item.Principal)).ToArray()));
            return SaveCalls == SaveFailureOnCall && SaveFailure is not null
                ? Task.FromException(SaveFailure)
                : Task.CompletedTask;
        }

        private static IdentificacaoAnimalResult ToResult(IdentificacaoAnimal item) => new(
            item.Id, item.AnimalId, item.Tipo, item.DescricaoTipo, item.Valor, item.Principal,
            item.DataAplicacao, item.Observacao, item.Ativo, item.CreatedAtUtc, item.UpdatedAtUtc);

        public sealed record RowState(Guid Id, bool Ativo, bool Principal);
        public sealed record DurableSnapshot(int SaveNumber, bool Committed, IReadOnlyList<RowState> Rows);

        private sealed class Transaction(
            FakeRepository owner,
            IReadOnlyList<IdentificacaoAnimal> originalItems,
            IReadOnlyDictionary<Guid, RowState> originalStates) : IIdentificacaoAnimalMutationScope
        {
            private bool committed;

            public Task CommitAsync(CancellationToken cancellationToken = default)
            {
                committed = true;
                owner.ScopeCommitted = true;
                owner.Events.Add("Commit");
                return Task.CompletedTask;
            }

            public ValueTask DisposeAsync()
            {
                if (committed)
                {
                    return ValueTask.CompletedTask;
                }

                owner.Events.Add("Rollback");
                owner.Identifications.RemoveAll(item => !originalStates.ContainsKey(item.Id));
                foreach (var item in originalItems)
                {
                    var state = originalStates[item.Id];
                    if (state.Ativo && !item.Ativo) item.Reativar(Now);
                    if (!state.Ativo && item.Ativo) item.Inativar(Now);
                    if (state.Principal && !item.Principal) item.DefinirPrincipal(Now);
                    if (!state.Principal && item.Principal) item.RemoverPrincipal(Now);
                }

                return ValueTask.CompletedTask;
            }
        }
    }
}
