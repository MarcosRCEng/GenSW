using GenSW.Application;
using GenSW.Application.Animals;
using GenSW.Application.Animals.Proles;
using GenSW.Domain.Animals;
using GenSW.Domain.Species;
using GenSW.Infrastructure;
using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GenSW.Infrastructure.Tests;

public sealed class ProleConcurrencyTests : IAsyncLifetime
{
    private GenSW.API.Tests.EphemeralPostgreSql pg = null!;
    private ServiceProvider services = null!;
    private Guid speciesId, cycleId;
    private static readonly DateOnly Date = new(2026, 1, 1);
    public async Task InitializeAsync()
    {
        pg = await GenSW.API.Tests.EphemeralPostgreSql.StartAsync();
        var collection = new ServiceCollection(); collection.AddLogging();
        collection.AddInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string,string?> { ["ConnectionStrings:GenSW"] = pg.ConnectionString }).Build());
        collection.AddApplication(); services = collection.BuildServiceProvider();
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GenSWDbContext>(); await db.Database.MigrateAsync();
        var now = DateTimeOffset.UtcNow; var species = Especie.Criar("Proles concorrentes", null, now);
        Animal Parent(string code, SexoAnimal sex) => Animal.Criar(code, null, species.Id, null, null, sex, null, EscopoAnimal.Operacional, Date, now);
        var father = Parent("P1", SexoAnimal.Macho); var mother = Parent("P2", SexoAnimal.Femea);
        var cross = Cruzamento.Criar(father.Id, mother.Id, StatusCruzamento.Concluido, null, null, null, null, now);
        var cycle = CicloReprodutivo.Criar(cross.Id, TipoCicloReprodutivo.Gestacional, StatusCicloReprodutivo.Concluido,
            dataPostura:null, dataInicioIncubacao:null, dataEclosao:null, ovosPostos:null, ovosFerteis:null,
            ovosIncubados:null, ovosEclodidos:null, ovosInviaveis:null, pesoMedioOvoGramas:null,
            dataInicioGestacao:Date, dataPrevistaParto:Date, dataParto:Date, nascidos:1, nascidosVivos:1,
            nascidosMortos:0, pesoAoNascerGramas:null, observacao:null, now:now);
        db.AddRange(species, father, mother, cross, cycle); await db.SaveChangesAsync(); speciesId=species.Id; cycleId=cycle.Id;
    }
    public async Task DisposeAsync() { await services.DisposeAsync(); await pg.DisposeAsync(); }
    private static ProleCommand Create(TipoRegistroProle kind) => new(kind,1,TipoOrigemProle.Nascimento,Date,null,SexoAnimal.Indeterminado,"Saudável",null);
    private static ProleUpdateCommand Update() => new(TipoOrigemProle.Nascimento,Date,null,SexoAnimal.Indeterminado,"Saudável",null);
    private async Task<bool> Run(Func<IProleService,Task> action)
    {
        await using var scope=services.CreateAsyncScope();
        try { await action(scope.ServiceProvider.GetRequiredService<IProleService>()); return true; }
        catch(InvalidOperationException) { return false; }
    }
    [Fact]
    public async Task Concurrent_creation_and_split_respect_cycle_and_batch_quantities()
    {
        Assert.Single(await Task.WhenAll(Run(async s=>{await s.CreateAsync(cycleId,Create(TipoRegistroProle.Lote));}),Run(async s=>{await s.CreateAsync(cycleId,Create(TipoRegistroProle.Lote));})),x=>x);
        await using var scope=services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<GenSWDbContext>();
        var batch=await db.Proles.SingleAsync();
        Assert.Single(await Task.WhenAll(Run(async s=>{await s.SplitAsync(batch.Id,Update());}),Run(async s=>{await s.SplitAsync(batch.Id,Update());})),x=>x);
        db.ChangeTracker.Clear();
        Assert.Equal(1,(await db.Proles.SingleAsync(x=>x.Id==batch.Id)).QuantidadeDesdobrada);
        Assert.Equal(2,await db.Proles.CountAsync());Assert.Equal(1,(await db.CiclosReprodutivos.SingleAsync()).Nascidos);
    }
    [Fact]
    public async Task Concurrent_automatic_conversion_creates_one_animal_and_no_automatic_filiation()
    {
        Guid id;
        await using(var scope=services.CreateAsyncScope()) id=(await scope.ServiceProvider.GetRequiredService<IProleService>().CreateAsync(cycleId,Create(TipoRegistroProle.Individual))).Id;
        var command=new ProleConversaoCommand(null,"Filhote",speciesId,null,null,EscopoAnimal.Operacional);
        Assert.Single(await Task.WhenAll(Run(async s=>{await s.ConvertAsync(id,command);}),Run(async s=>{await s.ConvertAsync(id,command);})),x=>x);
        await using var check=services.CreateAsyncScope();var db=check.ServiceProvider.GetRequiredService<GenSWDbContext>();
        Assert.Equal(3,await db.Animais.CountAsync());Assert.NotNull((await db.Proles.SingleAsync()).AnimalId);
        Assert.Equal(0,await db.FiliacoesAnimal.CountAsync());
    }
    [Fact]
    public async Task Outer_rollback_after_automatic_animal_creation_leaves_no_published_animal()
    {
        await using(var scope=services.CreateAsyncScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<GenSWDbContext>();
            await using var transaction=await db.Database.BeginTransactionAsync();
            await scope.ServiceProvider.GetRequiredService<IAnimalService>().CreateAsync(new(null,"Não publicar",speciesId,null,null,SexoAnimal.Indeterminado,Date,EscopoAnimal.Operacional));
            Assert.Equal(3,await db.Animais.CountAsync());
            // Disposal simulates failure before the outer conversion commits.
        }
        await using var check=services.CreateAsyncScope();
        Assert.Equal(2,await check.ServiceProvider.GetRequiredService<GenSWDbContext>().Animais.CountAsync());
    }
}
