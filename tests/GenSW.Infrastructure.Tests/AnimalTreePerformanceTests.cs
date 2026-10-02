using System.Data.Common;
using System.Diagnostics;
using GenSW.Domain.Species;
using GenSW.Infrastructure.Animals;
using GenSW.Infrastructure.Animals.Filiacoes;
using GenSW.Infrastructure.Images;
using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Xunit;
using Xunit.Abstractions;
namespace GenSW.Infrastructure.Tests;

public sealed class AnimalTreePerformanceTests(ITestOutputHelper output)
{
    private sealed class Counter : DbCommandInterceptor
    {
        public int Commands;
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand c, CommandEventData e, InterceptionResult<DbDataReader> r, CancellationToken ct = default) { Commands++; return ValueTask.FromResult(r); }
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand c, CommandEventData e, InterceptionResult<int> r, CancellationToken ct = default) { Commands++; return ValueTask.FromResult(r); }
    }
    [Fact]
    public async Task Ten_thousand_animals_tree_is_bounded_deduplicated_and_within_SQL_budget()
    {
        await using var pg = await GenSW.API.Tests.EphemeralPostgreSql.StartAsync();
        var counter = new Counter();
        await using var db = new GenSWDbContext(new DbContextOptionsBuilder<GenSWDbContext>().UseNpgsql(pg.ConnectionString).AddInterceptors(counter).Options);
        await db.Database.MigrateAsync();
        var species = Especie.Criar("Escala", null, DateTimeOffset.UtcNow); db.Add(species); await db.SaveChangesAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Animais" ("Id", "CodigoInterno", "EspecieId", "Sexo", "Escopo", "Ativo", "CreatedAtUtc", "UpdatedAtUtc")
            SELECT md5(i::text)::uuid, 'S' || lpad(i::text,5,'0'), {species.Id}, CASE WHEN i % 2 = 1 THEN 1 ELSE 2 END, 1, true, now(), now()
            FROM generate_series(1,10000) i;
            INSERT INTO "FiliacoesAnimal" ("Id", "AnimalId", "ProgenitorId", "TipoFiliacao", "Ativa", "CreatedAtUtc", "UpdatedAtUtc")
            SELECT md5('p' || i::text)::uuid, md5(i::text)::uuid,
                md5((CASE WHEN i >= 8000 THEN 1 ELSE 2*((i-1)/4)+1 END)::text)::uuid, 1, true, now(), now()
            FROM generate_series(5,10000) i;
            INSERT INTO "FiliacoesAnimal" ("Id", "AnimalId", "ProgenitorId", "TipoFiliacao", "Ativa", "CreatedAtUtc", "UpdatedAtUtc")
            SELECT md5('m' || i::text)::uuid, md5(i::text)::uuid, md5((2*((i-1)/4)+2)::text)::uuid, 2, true, now(), now()
            FROM generate_series(5,10000) i;
            ANALYZE "Animais"; ANALYZE "FiliacoesAnimal";
            """);
        var root = await db.Animais.Where(x=>x.CodigoInterno=="S00001").Select(x=>x.Id).SingleAsync();
        var leaf = await db.Animais.Where(x=>x.CodigoInterno=="S09000").Select(x=>x.Id).SingleAsync();
        var storage = new PrivateImageStorage(new ConfigurationBuilder().Build());
        var tree = new AnimalTreeQuery(db,storage);
        await tree.GetAsync(leaf,4,2,default);
        counter.Commands=0;
        var result = await tree.GetAsync(leaf,4,2,default);
        Assert.InRange(counter.Commands,1,12); var sql = counter.Commands;
        Assert.Equal(result.Nos.Count,result.Nos.Select(x=>x.AnimalId).Distinct().Count());
        Assert.All(result.Arestas, e=> { Assert.Contains(result.Nos,x=>x.AnimalId==e.ProgenitorId); Assert.Contains(result.Nos,x=>x.AnimalId==e.DescendenteId); });
        var broad = await tree.GetAsync(root,4,2,default);
        Assert.True(broad.Truncada); Assert.InRange(broad.Nos.Count,1,100); Assert.InRange(broad.Arestas.Count,1,200);
        var page = await tree.RelationsAsync(root,"descendentes",1,20,default); Assert.Equal(20,page.Arestas.Count); Assert.True(page.TotalItems>2000);
        var times = new List<double>(); var candidates = new List<double>();
        var search = new ProgenitorQuery(db);
        for(var i=0;i<25;i++)
        {
            var watch=Stopwatch.StartNew(); await tree.GetAsync(i%2==0?root:leaf,4,2,default); times.Add(watch.Elapsed.TotalMilliseconds);
            watch.Restart(); await search.SearchAsync(leaf,GenSW.Domain.Animals.TipoFiliacaoAnimal.Pai,"S",1,25,default); candidates.Add(watch.Elapsed.TotalMilliseconds);
        }
        times.Sort(); candidates.Sort();
        output.WriteLine($"Fixture: 10000 animais / 19992 vínculos. SQL máximo medido: {sql}. Árvore p95={times[23]:F1}ms; candidatos p95={candidates[23]:F1}ms. CPU lógico={Environment.ProcessorCount}; SO={Environment.OSVersion}; processo 64 bits={Environment.Is64BitProcess}.");
        Assert.True(times[23] < 500, $"p95 árvore {times[23]:F1}ms"); Assert.True(candidates[23] < 500, $"p95 candidatos {candidates[23]:F1}ms");
        await Assert.ThrowsAsync<ArgumentException>(()=>tree.GetAsync(root,5,2,default));
        await Assert.ThrowsAsync<ArgumentException>(()=>tree.RelationsAsync(root,"todos",1,20,default));
    }
}
