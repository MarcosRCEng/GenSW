using GenSW.Application.Inventory;
using GenSW.Domain.Catalog;
using GenSW.Domain.Inventory;
using GenSW.Domain.People;
using GenSW.Infrastructure.Identity;
using GenSW.Infrastructure.Inventory;
using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GenSW.API.Tests;

public sealed class InventoryReadClockTests
{
    [Fact]
    public async Task Balance_detail_and_filtered_pages_use_one_instant_for_expiry_and_metadata_across_midnight()
    {
        await using var pg = await EphemeralPostgreSql.StartAsync();
        await using var db = new GenSWDbContext(new DbContextOptionsBuilder<GenSWDbContext>().UseNpgsql(pg.ConnectionString).Options);
        await db.Database.MigrateAsync();
        var expiry = new DateOnly(2026,10,8);
        var beforeMidnight = new DateTimeOffset(2026,10,9,2,59,59,TimeSpan.Zero);
        var afterMidnight = beforeMidnight.AddSeconds(2);
        var person = Pessoa.Criar(TipoPessoa.Fisica,"Responsável sintético de relógio",null,beforeMidnight);
        var user = new ApplicationUser { Id=Guid.NewGuid(),PessoaId=person.Id,IsActive=true,CreatedAtUtc=beforeMidnight,UpdatedAtUtc=beforeMidnight,UserName="readclock",NormalizedUserName="READCLOCK" };
        var item = Item.Create(new("READ-CLOCK","Fixture de validade",null,null,"Alimentar","kg",true,true,true,false),beforeMidnight); item.FixUnit();
        var local = LocalEstoque.Create("READ-LOCAL","Local sintético",null,null,"Ordinario",true,beforeMidnight);
        var lot = LoteMaterial.Create(item.Id,"READ-LOT","kg",new("","Fixture","Fixture",user.Id,null,null,null),expiry,"Fixture",user.Id,null,null,null,false,beforeMidnight,expiry);
        var position = PosicaoEstoque.Create(lot.Id,local.Id,"kg"); position.SetBalance(0,0);
        db.AddRange(person,user,item,local,lot,position); await db.SaveChangesAsync(); db.ChangeTracker.Clear();

        foreach (var initial in new[] { beforeMidnight,afterMidnight })
        {
            var eligible = initial == beforeMidnight;
            var detailClock = new CrossingClock(initial,afterMidnight);
            var detailRepository = new InventoryRepository(db,detailClock);
            await using (var scope = await detailRepository.BeginReadAsync(default))
            {
                var detail = await detailRepository.SaldoAsync(lot.Id,local.Id,expiry,default);
                Assert.NotNull(detail); Assert.Equal(eligible,detail.Elegivel);
                Assert.Equal(initial,detail.ObservadoEmUtc); Assert.Equal(1,detailClock.Reads);
                Assert.Equal(!eligible,detail.MotivosIndisponibilidade.Contains("Validade vencida"));
                await scope.CommitAsync(default);
            }
            var pageClock = new CrossingClock(initial,afterMidnight);
            var pageRepository = new InventoryRepository(db,pageClock);
            await using (var scope = await pageRepository.BeginReadAsync(default))
            {
                // The supplied application date is deliberately from before midnight.
                var page = await pageRepository.SaldosAsync(new InventoryQuery(LoteId:lot.Id,Elegivel:eligible),expiry,default);
                Assert.Equal(1,page.TotalItems); var balance=Assert.Single(page.Items);
                Assert.Equal(eligible,balance.Elegivel); Assert.Equal(initial,page.ObservadoEmUtc);
                Assert.Equal(page.ObservadoEmUtc,balance.ObservadoEmUtc); Assert.Equal(1,pageClock.Reads);
                await scope.CommitAsync(default);
            }
        }
    }

    private sealed class CrossingClock(DateTimeOffset first,DateTimeOffset subsequent) : TimeProvider
    {
        public int Reads { get; private set; }
        public override DateTimeOffset GetUtcNow() => ++Reads == 1 ? first : subsequent;
    }
}
