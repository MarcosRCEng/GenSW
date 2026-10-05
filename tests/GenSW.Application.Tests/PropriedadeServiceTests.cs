using GenSW.Application.Animals;
using GenSW.Application.Properties;
using GenSW.Domain.Properties;
using Xunit;

namespace GenSW.Application.Tests;

public sealed class PropriedadeServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 5);

    [Fact]
    public async Task Transfer_closes_then_adds_in_one_scope_and_rejects_stale_expectations()
    {
        var repository = new FakeRepository();
        var service = new PropriedadeService(repository, new FixedClock());
        var first = await service.TransferAsync(repository.AnimalId, new(repository.First.Id, Today, null));
        var second = await service.TransferAsync(repository.AnimalId, new(repository.Second.Id, Today, first.Atual!.Id));
        Assert.Equal(repository.Second.Id, second.Atual!.PropriedadeId);
        Assert.Equal(Today, second.Historico.Single(x => x.Id == first.Atual.Id).DataFim);
        Assert.Equal(2, second.Historico.Count);
        Assert.Equal(2, repository.Commits);
        Assert.Equal(new[] { "begin", "add", "save", "commit", "begin", "save", "add", "save", "commit" }, repository.Operations);
        var error = await Assert.ThrowsAsync<AnimalEvolutionException>(() =>
            service.TransferAsync(repository.AnimalId, new(repository.First.Id, Today, first.Atual.Id)));
        Assert.Equal("vinculo_desatualizado", error.Code);
        Assert.Equal(2, repository.Links.Count);
    }

    [Fact]
    public async Task Detach_and_relink_preserve_gap_and_reject_overlap_with_closed_history()
    {
        var repository = new FakeRepository();
        var service = new PropriedadeService(repository, new FixedClock());
        var first = await service.TransferAsync(repository.AnimalId, new(repository.First.Id, Today.AddDays(-3), null));
        var detached = await service.DetachAsync(repository.AnimalId, new(Today.AddDays(-1), first.Atual!.Id));
        Assert.Null(detached.Atual);
        await Assert.ThrowsAsync<ArgumentException>(() => service.TransferAsync(repository.AnimalId,
            new(repository.Second.Id, Today.AddDays(-2), null)));
        var relink = await service.TransferAsync(repository.AnimalId, new(repository.Second.Id, Today, null));
        Assert.Equal(Today, relink.Atual!.DataInicio);
        Assert.Equal(2, relink.Historico.Count);
    }

    [Fact]
    public async Task Inactivation_preserves_current_link_but_blocks_new_assignments()
    {
        var repository = new FakeRepository();
        var service = new PropriedadeService(repository, new FixedClock());
        var first = await service.TransferAsync(repository.AnimalId, new(repository.First.Id, Today, null));
        await service.SetActiveAsync(repository.First.Id, false);
        Assert.Equal(first.Atual!.Id, (await service.HistoryAsync(repository.AnimalId)).Atual!.Id);
        await service.DetachAsync(repository.AnimalId, new(Today, first.Atual.Id));
        var error = await Assert.ThrowsAsync<AnimalEvolutionException>(() =>
            service.TransferAsync(repository.AnimalId, new(repository.First.Id, Today, null)));
        Assert.Equal("propriedade_inativa", error.Code);
        Assert.Single(repository.Links);
    }

    [Theory]
    [InlineData(0, 25, "nome", "asc")]
    [InlineData(1, 101, "nome", "asc")]
    [InlineData(int.MaxValue, 100, "nome", "asc")]
    [InlineData(1, 25, "invalid", "asc")]
    [InlineData(1, 25, "nome", "invalid")]
    public async Task List_rejects_invalid_queries_before_repository(int page, int size, string sort, string direction)
    {
        var service = new PropriedadeService(new FakeRepository(), new FixedClock());
        await Assert.ThrowsAsync<ArgumentException>(() => service.ListAsync(new(page, size, null, null, sort, direction)));
    }

    private sealed class FixedClock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }

    private sealed class FakeRepository : IPropriedadeRepository
    {
        public Guid AnimalId { get; } = Guid.NewGuid();
        public Propriedade First { get; } = Propriedade.Criar("Norte", null, null, Now);
        public Propriedade Second { get; } = Propriedade.Criar("Sul", null, null, Now);
        public List<VinculoAnimalPropriedade> Links { get; } = [];
        public List<string> Operations { get; } = [];
        public int Commits { get; private set; }
        public Task AddAsync(Propriedade item, CancellationToken ct) => Task.CompletedTask;
        public Task<Propriedade?> GetAsync(Guid id, bool tracking, CancellationToken ct) =>
            Task.FromResult(new[] { First, Second }.SingleOrDefault(x => x.Id == id));
        public Task<PropriedadePage> ListAsync(PropriedadeListQuery query, CancellationToken ct) => throw new NotImplementedException();
        public Task<bool> NomeExistsAsync(string nome, Guid? excludingId, CancellationToken ct) => Task.FromResult(false);
        public Task<IAnimalMutationScope> BeginMutationAsync(Guid? animalId, CancellationToken ct)
        { Operations.Add("begin"); return Task.FromResult<IAnimalMutationScope>(new Scope(this)); }
        public Task<Propriedade?> LockPropriedadeAsync(Guid id, CancellationToken ct) => GetAsync(id, true, ct);
        public Task<bool> AnimalExistsAsync(Guid id, CancellationToken ct) => Task.FromResult(id == AnimalId);
        public Task<VinculoAnimalPropriedade?> GetCurrentAsync(Guid animalId, CancellationToken ct) =>
            Task.FromResult(Links.SingleOrDefault(x => x.AnimalId == animalId && x.DataFim is null));
        public Task<DateOnly?> GetLastEndAsync(Guid animalId, CancellationToken ct) =>
            Task.FromResult(Links.Where(x => x.AnimalId == animalId).Select(x => x.DataFim).DefaultIfEmpty().Max());
        public Task<IReadOnlyList<VinculoAnimalPropriedadeResult>> HistoryAsync(Guid animalId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<VinculoAnimalPropriedadeResult>>(Links.Where(x => x.AnimalId == animalId).Select(x =>
                new VinculoAnimalPropriedadeResult(x.Id, x.AnimalId, x.PropriedadeId,
                    x.PropriedadeId == First.Id ? First.Nome : Second.Nome,
                    x.PropriedadeId == First.Id ? First.Ativo : Second.Ativo,
                    x.DataInicio, x.DataFim, x.Observacao, x.CreatedAtUtc, x.UpdatedAtUtc)).ToArray());
        public Task AddVinculoAsync(VinculoAnimalPropriedade item, CancellationToken ct)
        { Operations.Add("add"); Links.Add(item); return Task.CompletedTask; }
        public Task SaveAsync(CancellationToken ct) { Operations.Add("save"); return Task.CompletedTask; }
        private sealed class Scope(FakeRepository repository) : IAnimalMutationScope
        {
            public Task CommitAsync(CancellationToken ct = default)
            { repository.Commits++; repository.Operations.Add("commit"); return Task.CompletedTask; }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
