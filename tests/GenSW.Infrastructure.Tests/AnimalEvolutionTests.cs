using GenSW.Application.Animals;
using GenSW.Application.Animals.Filiacoes;
using GenSW.Application.Animals.Pesagens;
using GenSW.Application.Images;
using GenSW.Domain.Animals;
using GenSW.Domain.Species;
using GenSW.Domain.Varieties;
using GenSW.Infrastructure.Animals;
using GenSW.Infrastructure.Animals.Filiacoes;
using GenSW.Infrastructure.Animals.Pesagens;
using GenSW.Infrastructure.Images;
using GenSW.Infrastructure.Persistence;
using ImageMagick;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Xunit;
namespace GenSW.Infrastructure.Tests;

public sealed class AnimalEvolutionTests : IAsyncLifetime
{
    private GenSW.API.Tests.EphemeralPostgreSql pg = null!;
    private readonly string volume = Path.Combine(Path.GetTempPath(), "gensw-images-tests-" + Guid.NewGuid().ToString("N"));
    public async Task InitializeAsync() { pg = await GenSW.API.Tests.EphemeralPostgreSql.StartAsync(); Directory.CreateDirectory(volume); }
    public async Task DisposeAsync() { await pg.DisposeAsync(); Directory.Delete(volume, true); }
    private GenSWDbContext Db() => new(new DbContextOptionsBuilder<GenSWDbContext>().UseNpgsql(pg.ConnectionString).Options);
    private static Animal Animal(Guid species, string code, SexoAnimal sex = SexoAnimal.Macho, DateOnly? birth = null) =>
        GenSW.Domain.Animals.Animal.Criar(code, null, species, null, null, sex, birth, EscopoAnimal.Operacional, DateOnly.FromDateTime(DateTime.UtcNow), DateTimeOffset.UtcNow);
    private IPrivateImageStorage Storage() => new PrivateImageStorage(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Images:PrivateRoot"] = volume, ["Images:MinimumFreeBytes"] = "0" }).Build());
    private static byte[] Png() { using var image = new MagickImage(MagickColors.Red, 20, 10); return image.ToByteArray(MagickFormat.Png); }
    private ImageService Images(GenSWDbContext db) => new(new ImageRepository(db), Storage(), new MagickImageProcessor(), TimeProvider.System);

    [Fact]
    public async Task Additive_migration_preserves_existing_animals_and_weight_dates_and_guards_birth()
    {
        await using var db = Db();
        await db.GetService<IMigrator>().MigrateAsync("20260929225633_AddEggProduction");
        var species = Especie.Criar("Pesagens", null, DateTimeOffset.UtcNow);
        var animal = Animal(species.Id, "P1", birth: new(2026, 8, 1)); db.AddRange(species, animal); await db.SaveChangesAsync();
        await db.Database.MigrateAsync();
        Assert.Empty(await db.PesagensAnimal.ToListAsync()); Assert.Empty(await db.ImagensAnimal.ToListAsync());
        Assert.Equal(new DateOnly(2026,8,1), (await db.Animais.SingleAsync()).DataNascimento);
        var weights = new PesagemService(new PesagemRepository(db), TimeProvider.System);
        var item = await weights.SaveAsync(animal.Id, null, new(new(2026,9,1), 12.34m, TipoMarcoPesagem.IdadeEmDias, null, 30, null), default);
        Assert.Equal(31, item.IdadeDiasNaMedicao); Assert.Equal(30, item.IdadeReferenciaDias);
        await weights.SaveAsync(animal.Id, null, new(new(2026,9,1), 12.34m, TipoMarcoPesagem.Livre, null, null, null), default);
        Assert.Equal(2, (await weights.ListAsync(animal.Id, new(), default)).TotalItems);
        var guard = new AnimalMutationGuard(db);
        await using (var scope = await guard.BeginAsync(animal.Id, default))
        {
            await Assert.ThrowsAsync<AnimalEvolutionException>(() => guard.ValidateAsync(animal,
                new(animal.CodigoInterno, null, species.Id, null, null, animal.Sexo, new(2026,9,2), animal.Escopo), default));
        }
        animal.AlterarCadastro(animal.CodigoInterno, null, species.Id, null, null, animal.Sexo, null, animal.Escopo, DateOnly.FromDateTime(DateTime.UtcNow), DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();
        var corrected = await weights.GetAsync(animal.Id, item.Id, default);
        Assert.Null(corrected.IdadeDiasNaMedicao); Assert.Equal(item.DataMedicao, corrected.DataMedicao);
        Assert.Equal(item.CreatedAtUtc, corrected.CreatedAtUtc);
    }

    [Fact]
    public async Task Genealogy_serializes_two_and_three_way_cycles_and_preserves_replaced_history()
    {
        await using var db = Db(); await db.Database.MigrateAsync();
        var species = Especie.Criar("Genealogia", null, DateTimeOffset.UtcNow);
        var a = Animal(species.Id, "A"); var b = Animal(species.Id, "B"); var c = Animal(species.Id, "C");
        db.AddRange(species, a, b, c); await db.SaveChangesAsync();
        async Task<bool> Link(Guid child, Guid parent)
        {
            await using var connection = Db();
            try { await new FiliacaoAnimalService(new FiliacaoAnimalRepository(connection), TimeProvider.System).CreateOrReplaceAsync(child, new(parent, TipoFiliacaoAnimal.Pai, null)); return true; }
            catch (FiliacaoAnimalConflictException) { return false; }
        }
        var results = await Task.WhenAll(Link(a.Id,b.Id), Link(b.Id,a.Id)); Assert.Single(results, x=>x);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM \"FiliacoesAnimal\"");
        results = await Task.WhenAll(Link(a.Id,b.Id), Link(b.Id,c.Id), Link(c.Id,a.Id)); Assert.Equal(2, results.Count(x=>x));
        await db.Database.ExecuteSqlRawAsync("DELETE FROM \"FiliacoesAnimal\"");
        Assert.True(await Link(a.Id, b.Id)); Assert.True(await Link(a.Id, c.Id));
        Assert.Equal(2, await db.FiliacoesAnimal.CountAsync(x=>x.AnimalId==a.Id)); Assert.Single(await db.FiliacoesAnimal.Where(x=>x.AnimalId==a.Id && x.Ativa).ToListAsync());
        var candidates = await new ProgenitorQuery(db).SearchAsync(c.Id, TipoFiliacaoAnimal.Pai, null, 1, 25, default);
        Assert.DoesNotContain(candidates.Items, x=>x.Id==a.Id || x.Id==c.Id);
        c.Inativar(DateTimeOffset.UtcNow); await db.SaveChangesAsync(); Assert.False(await Link(b.Id,c.Id));
        Assert.NotNull((await new FiliacaoAnimalRepository(db).ListAsync(a.Id)).First().Progenitor);
    }

    [Fact]
    public async Task Images_have_separate_owners_unique_concurrent_preference_and_deterministic_fallback()
    {
        await using var db = Db(); await db.Database.MigrateAsync();
        var species = Especie.Criar("Imagens", null, DateTimeOffset.UtcNow);
        var animal = Animal(species.Id, "IMG"); var variety = Variedade.Criar(species.Id, "Catálogo", DateTimeOffset.UtcNow);
        db.AddRange(species, animal, variety); await db.SaveChangesAsync();
        var one = await Images(db).UploadAsync(ImageOwner.Animal, animal.Id, new MemoryStream(Png()), "Primeira", null, default);
        var two = await Images(db).UploadAsync(ImageOwner.Animal, animal.Id, new MemoryStream(Png()), "Segunda", null, default);
        Assert.Equal("ordenacao", (await Images(db).PreferredAsync(ImageOwner.Animal, animal.Id, default)).Origem);
        Assert.Null((await Images(db).PreferredAsync(ImageOwner.Variedade, variety.Id, default)).Imagem);
        async Task Prefer(Guid id) { await using var connection=Db(); await Images(connection).PreferAsync(ImageOwner.Animal, animal.Id, id, true, default); }
        await Task.WhenAll(Prefer(one.Id), Prefer(two.Id)); db.ChangeTracker.Clear();
        Assert.Equal(1, await db.ImagensAnimal.CountAsync(x=>x.Ativa && x.Representativa));
        var preferred = await Images(db).PreferredAsync(ImageOwner.Animal, animal.Id, default);
        await Images(db).SetActiveAsync(ImageOwner.Animal, animal.Id, preferred.Imagem!.Id, false, default);
        await Assert.ThrowsAsync<AnimalEvolutionException>(()=>Images(db).ContentAsync(ImageOwner.Animal, animal.Id, preferred.Imagem.Id, "miniatura", default));
        await Images(db).SetActiveAsync(ImageOwner.Animal, animal.Id, preferred.Imagem.Id, true, default);
        Assert.Equal(0, await db.ImagensAnimal.CountAsync(x=>x.Representativa));
        await Assert.ThrowsAsync<AnimalEvolutionException>(()=>Images(db).ReorderAsync(ImageOwner.Animal, animal.Id, new[]{one.Id}, default));
        await Images(db).ReorderAsync(ImageOwner.Animal, animal.Id, new[]{two.Id,one.Id}, default);
        Assert.Equal(two.Id, (await Images(db).PreferredAsync(ImageOwner.Animal, animal.Id, default)).Imagem!.Id);
        var stored = await db.ImagensAnimal.SingleAsync(x=>x.Id==two.Id); File.Delete(Path.Combine(volume,stored.ArquivoKey));
        Assert.Equal(one.Id, (await Images(db).PreferredAsync(ImageOwner.Animal, animal.Id, default)).Imagem!.Id);
        await Assert.ThrowsAsync<AnimalEvolutionException>(()=>Images(db).ContentAsync(ImageOwner.Variedade,variety.Id,one.Id,"miniatura",default));
    }

    [Fact]
    public async Task Decoder_rejects_false_truncated_animated_and_large_files_and_strips_profiles()
    {
        var processor=new MagickImageProcessor();
        await Assert.ThrowsAsync<AnimalEvolutionException>(()=>processor.ProcessAsync(new MemoryStream("<svg/>"u8.ToArray()),default));
        await Assert.ThrowsAsync<AnimalEvolutionException>(()=>processor.ProcessAsync(new MemoryStream(new byte[5*1024*1024+1]),default));
        await Assert.ThrowsAsync<ArgumentException>(()=>processor.ProcessAsync(new MemoryStream(Png()[..30]),default));
        using var animated=new MagickImageCollection(); animated.Add(new MagickImage(MagickColors.Red,10,10)); animated.Add(new MagickImage(MagickColors.Blue,10,10));
        await Assert.ThrowsAsync<ArgumentException>(()=>processor.ProcessAsync(new MemoryStream(animated.ToByteArray(MagickFormat.WebP)),default));
        foreach (var dimensions in new[] { (8193u, 10u), (5000u, 5000u) })
        {
            var header = Png();
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(16,4), dimensions.Item1);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(20,4), dimensions.Item2);
            uint crc = uint.MaxValue;
            foreach (var value in header.Skip(12).Take(17))
            {
                crc ^= value;
                for (var bit=0;bit<8;bit++) crc=(crc>>1)^((crc&1)==1 ? 0xedb88320u : 0u);
            }
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(29,4), ~crc);
            await Assert.ThrowsAsync<ArgumentException>(()=>processor.ProcessAsync(new MemoryStream(header),default));
        }
        var apng = Png().ToList();
        apng.InsertRange(33, new byte[] {0,0,0,8,97,99,84,76,0,0,0,2,0,0,0,0,0,0,0,0});
        await Assert.ThrowsAsync<ArgumentException>(()=>processor.ProcessAsync(new MemoryStream(apng.ToArray()),default));
        using var source=new MagickImage(MagickColors.Blue,100,50); var profile=new ExifProfile(); profile.SetValue(ExifTag.Artist,"Private"); source.SetProfile(profile);
        var image=await processor.ProcessAsync(new MemoryStream(source.ToByteArray(MagickFormat.Jpeg)),default);
        using var result=new MagickImage(image.View); Assert.Null(result.GetExifProfile()); Assert.Equal(100u,result.Width);
    }

    [Fact]
    public async Task Quota_serializes_concurrent_uploads_and_storage_database_failures_do_not_publish_metadata()
    {
        await using var db = Db(); await db.Database.MigrateAsync();
        var species=Especie.Criar("Cotas",null,DateTimeOffset.UtcNow); var animal=Animal(species.Id,"QUOTA"); db.AddRange(species,animal); await db.SaveChangesAsync();
        var first=await Images(db).UploadAsync(ImageOwner.Animal,animal.Id,new MemoryStream(Png()),null,null,default);
        var source=await db.ImagensAnimal.SingleAsync();
        for(var i=1;i<49;i++) db.Add(ImagemAnimal.Criar(animal.Id,source.ArquivoKey,source.MiniaturaKey,source.TamanhoBytes,source.Largura,source.Altura,i,null,null,DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
        async Task<bool> Upload()
        {
            await using var connection=Db();
            try { await Images(connection).UploadAsync(ImageOwner.Animal,animal.Id,new MemoryStream(Png()),null,null,default); return true; }
            catch(AnimalEvolutionException e) when(e.Code=="cota_imagens") { return false; }
        }
        Assert.Single(await Task.WhenAll(Upload(),Upload()),x=>x);
        Assert.Equal(50,await db.ImagensAnimal.CountAsync(x=>x.Ativa));
        Assert.Equal(4,Directory.GetFiles(volume,"*.png").Length);
        await Images(db).SetActiveAsync(ImageOwner.Animal,animal.Id,first.Id,false,default);
        Assert.True(await Upload());
        await Assert.ThrowsAsync<AnimalEvolutionException>(()=>Images(db).SetActiveAsync(ImageOwner.Animal,animal.Id,first.Id,true,default));
        var other=Animal(species.Id,"FAIL");db.Add(other);await db.SaveChangesAsync();
        var byteLimited=Animal(species.Id,"BYTE-QUOTA");db.Add(byteLimited);
        for(var i=0;i<2;i++) db.Add(ImagemAnimal.Criar(byteLimited.Id,source.ArquivoKey,source.MiniaturaKey,50L*1024*1024,20,10,i,null,null,DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
        var byteError=await Assert.ThrowsAsync<AnimalEvolutionException>(()=>Images(db).UploadAsync(ImageOwner.Animal,byteLimited.Id,new MemoryStream(Png()),null,null,default));
        Assert.Equal("cota_imagens",byteError.Code);
        var before=Directory.GetFiles(volume,"*.png").Length;
        await using(var connection=Db())
        {
            var failing=new ImageService(new FailingSave(new ImageRepository(connection)),Storage(),new MagickImageProcessor(),TimeProvider.System);
            await Assert.ThrowsAsync<InvalidOperationException>(()=>failing.UploadAsync(ImageOwner.Animal,other.Id,new MemoryStream(Png()),null,null,default));
        }
        Assert.Equal(before,Directory.GetFiles(volume,"*.png").Length);
        Assert.False(await db.ImagensAnimal.AnyAsync(x=>x.AnimalId==other.Id));
        var disabledStorage=new PrivateImageStorage(new ConfigurationBuilder().Build());
        var disabled=new ImageService(new ImageRepository(db),disabledStorage,new MagickImageProcessor(),TimeProvider.System);
        var error=await Assert.ThrowsAsync<AnimalEvolutionException>(()=>disabled.UploadAsync(ImageOwner.Animal,other.Id,new MemoryStream(Png()),null,null,default));
        Assert.Equal(503,error.Status);Assert.False(await db.ImagensAnimal.AnyAsync(x=>x.AnimalId==other.Id));
    }

    [Fact]
    public async Task Birth_and_status_mutations_serialize_with_measurement_and_parent_selection()
    {
        await using var db=Db();await db.Database.MigrateAsync();
        var species=Especie.Criar("Concorrência cadastro",null,DateTimeOffset.UtcNow);
        var a=Animal(species.Id,"LOCK-A",birth:new(2026,8,1));var b=Animal(species.Id,"LOCK-B");db.AddRange(species,a,b);await db.SaveChangesAsync();
        var guard=new AnimalMutationGuard(db);
        Task measurement;
        await using(var tx=await guard.BeginAsync(a.Id,default))
        {
            a.AlterarCadastro(a.CodigoInterno,null,species.Id,null,null,a.Sexo,new(2026,9,2),a.Escopo,DateOnly.FromDateTime(DateTime.UtcNow),DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
            measurement=Task.Run(async()=>{await using var connection=Db();await new PesagemService(new PesagemRepository(connection),TimeProvider.System).SaveAsync(a.Id,null,new(new(2026,9,1),10,TipoMarcoPesagem.Livre,null,null,null),default);});
            await Task.Delay(150); Assert.False(measurement.IsCompleted); await tx.CommitAsync();
        }
        await Assert.ThrowsAsync<ArgumentException>(()=>measurement); Assert.Empty(await db.PesagensAnimal.ToListAsync());
        Task filiation;
        await using(var tx=await guard.BeginAsync(b.Id,default))
        {
            b.Inativar(DateTimeOffset.UtcNow);await db.SaveChangesAsync();
            filiation=Task.Run(async()=>{await using var connection=Db();await new FiliacaoAnimalService(new FiliacaoAnimalRepository(connection),TimeProvider.System).CreateOrReplaceAsync(a.Id,new(b.Id,TipoFiliacaoAnimal.Pai,null));});
            await Task.Delay(150);Assert.False(filiation.IsCompleted);await tx.CommitAsync();
        }
        await Assert.ThrowsAsync<FiliacaoAnimalConflictException>(()=>filiation);Assert.Empty(await db.FiliacoesAnimal.ToListAsync());
        b.Reativar(DateTimeOffset.UtcNow);await db.SaveChangesAsync();
        await new FiliacaoAnimalService(new FiliacaoAnimalRepository(db),TimeProvider.System).CreateOrReplaceAsync(a.Id,new(b.Id,TipoFiliacaoAnimal.Pai,null));
        await using(var tx=await guard.BeginAsync(b.Id,default))
            await Assert.ThrowsAsync<AnimalEvolutionException>(()=>guard.ValidateAsync(b,new(b.CodigoInterno,null,species.Id,null,null,SexoAnimal.Femea,null,b.Escopo),default));
        await using(var held=await guard.BeginAsync(a.Id,default))
        {
            await using var connection=Db();
            var timeout=await Assert.ThrowsAsync<AnimalEvolutionException>(()=>new AnimalMutationGuard(connection).BeginAsync(b.Id,default));
            Assert.Equal("conflito_transitorio",timeout.Code);
        }
    }

    [Fact]
    public async Task Eligibility_matches_search_and_write_and_legacy_cycles_are_reported_without_rewriting_history()
    {
        await using var db=Db(); await db.Database.MigrateAsync();
        var species=Especie.Criar("Elegibilidade",null,DateTimeOffset.UtcNow);
        var other=Especie.Criar("Outra",null,DateTimeOffset.UtcNow);
        var breed=GenSW.Domain.Breeds.Raca.Criar(species.Id,"Raça A",DateTimeOffset.UtcNow);
        var different=GenSW.Domain.Breeds.Raca.Criar(species.Id,"Raça B",DateTimeOffset.UtcNow);
        var child=Animal(species.Id,"CHILD");
        var eligible=Animal(species.Id,"ELIGIBLE");
        var unclassified=Animal(species.Id,"NO-BREED");
        var wrongBreed=Animal(species.Id,"WRONG-BREED");
        var wrongSpecies=Animal(other.Id,"WRONG-SPECIES");
        var female=Animal(species.Id,"FEMALE",SexoAnimal.Femea);
        var inactive=Animal(species.Id,"INACTIVE"); inactive.Inativar(DateTimeOffset.UtcNow);
        foreach(var pair in new[]{(child,breed.Id),(eligible,breed.Id),(wrongBreed,different.Id),(female,breed.Id),(inactive,breed.Id)})
            pair.Item1.AlterarCadastro(pair.Item1.CodigoInterno,null,species.Id,pair.Item2,null,pair.Item1.Sexo,null,pair.Item1.Escopo,DateOnly.FromDateTime(DateTime.UtcNow),DateTimeOffset.UtcNow);
        db.AddRange(species,other,breed,different,child,eligible,unclassified,wrongBreed,wrongSpecies,female,inactive); await db.SaveChangesAsync();
        var query=new ProgenitorQuery(db);
        var candidates=await query.SearchAsync(child.Id,TipoFiliacaoAnimal.Pai,null,1,100,default);
        Assert.Equal(eligible.Id,Assert.Single(candidates.Items).Id);
        var service=new FiliacaoAnimalService(new FiliacaoAnimalRepository(db),TimeProvider.System);
        foreach(var invalid in new[]{child,unclassified,wrongBreed,wrongSpecies,female,inactive})
            await Assert.ThrowsAsync<FiliacaoAnimalConflictException>(()=>service.CreateOrReplaceAsync(child.Id,new(invalid.Id,TipoFiliacaoAnimal.Pai,null)));
        foreach(var classification in new[]{(other.Id,(Guid?)null),(species.Id,(Guid?)different.Id)})
        {
            Task pending;
            var guard=new AnimalMutationGuard(db);
            await using(var scope=await guard.BeginAsync(eligible.Id,default))
            {
                await guard.ValidateAsync(eligible,new(eligible.CodigoInterno,null,classification.Item1,classification.Item2,null,eligible.Sexo,null,eligible.Escopo),default);
                eligible.AlterarCadastro(eligible.CodigoInterno,null,classification.Item1,classification.Item2,null,eligible.Sexo,null,eligible.Escopo,DateOnly.FromDateTime(DateTime.UtcNow),DateTimeOffset.UtcNow);
                await db.SaveChangesAsync();
                pending=Task.Run(async()=>{await using var connection=Db();await new FiliacaoAnimalService(new FiliacaoAnimalRepository(connection),TimeProvider.System).CreateOrReplaceAsync(child.Id,new(eligible.Id,TipoFiliacaoAnimal.Pai,null));});
                await Task.Delay(150);Assert.False(pending.IsCompleted);await scope.CommitAsync();
            }
            await Assert.ThrowsAsync<FiliacaoAnimalConflictException>(()=>pending);
            eligible.AlterarCadastro(eligible.CodigoInterno,null,species.Id,breed.Id,null,eligible.Sexo,null,eligible.Escopo,DateOnly.FromDateTime(DateTime.UtcNow),DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }
        await service.CreateOrReplaceAsync(child.Id,new(eligible.Id,TipoFiliacaoAnimal.Pai,null));
        child.AlterarCadastro(child.CodigoInterno,null,species.Id,null,null,child.Sexo,null,child.Escopo,DateOnly.FromDateTime(DateTime.UtcNow),DateTimeOffset.UtcNow); await db.SaveChangesAsync();
        candidates=await query.SearchAsync(child.Id,TipoFiliacaoAnimal.Pai,null,1,100,default);
        Assert.Contains(candidates.Items,x=>x.Id==unclassified.Id);
        Assert.Contains(candidates.Items,x=>x.Id==wrongBreed.Id);
        // Deliberately seed preexisting inconsistent links through persistence, bypassing the new service.
        db.Add(FiliacaoAnimal.Criar(eligible.Id,child.Id,TipoFiliacaoAnimal.Pai,null,DateTimeOffset.UtcNow));
        db.Add(FiliacaoAnimal.Criar(child.Id,wrongSpecies.Id,TipoFiliacaoAnimal.Mae,null,DateTimeOffset.UtcNow));
        eligible.Inativar(DateTimeOffset.UtcNow); await db.SaveChangesAsync();
        var tree=await new AnimalTreeQuery(db,Storage()).GetAsync(child.Id,4,2,default);
        Assert.Contains("ciclo_legado_detectado",tree.Avisos);
        Assert.Contains("filiacao_legada_inconsistente",tree.Avisos);
        Assert.False(tree.Nos.Single(x=>x.AnimalId==eligible.Id).Ativo);
        Assert.Equal(tree.Nos.Count,tree.Nos.Select(x=>x.AnimalId).Distinct().Count());
        Assert.Equal(3,await db.FiliacoesAnimal.CountAsync());
    }

    [Fact]
    public async Task Database_and_private_volume_backup_restore_recovers_metadata_and_authenticated_content()
    {
        await using var db=Db(); await db.Database.MigrateAsync();
        var species=Especie.Criar("Restauração",null,DateTimeOffset.UtcNow);var animal=Animal(species.Id,"RESTORE");
        db.AddRange(species,animal); await db.SaveChangesAsync();
        var item=await Images(db).UploadAsync(ImageOwner.Animal,animal.Id,new MemoryStream(Png()),"Backup",null,default);
        await Images(db).PreferAsync(ImageOwner.Animal,animal.Id,item.Id,true,default);
        var snapshots=Directory.GetFiles(volume,"*.png").ToDictionary(x=>Path.GetFileName(x)!,x=>File.ReadAllBytes(x));
        var dump=Path.Combine(volume,"backup.dump");await pg.BackupAsync(dump);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM \"ImagensAnimal\"");
        foreach(var file in Directory.GetFiles(volume,"*.png")) File.Delete(file);
        await pg.RestoreAsync(dump);
        foreach(var pair in snapshots) await File.WriteAllBytesAsync(Path.Combine(volume,pair.Key!),pair.Value);
        db.ChangeTracker.Clear();
        Assert.Equal(item.Id,(await Images(db).PreferredAsync(ImageOwner.Animal,animal.Id,default)).Imagem!.Id);
        using var restored=await Images(db).ContentAsync(ImageOwner.Animal,animal.Id,item.Id,"visualizacao",default);
        using var copy=new MemoryStream();await restored.CopyToAsync(copy);
        Assert.Contains(snapshots.Values,x=>System.Security.Cryptography.SHA256.HashData(x).SequenceEqual(System.Security.Cryptography.SHA256.HashData(copy.ToArray())));
        Assert.Equal(1,await db.ImagensAnimal.CountAsync());
    }

    private sealed class FailingSave(IImageRepository inner):IImageRepository
    {
        public Task<IAnimalMutationScope> BeginAsync(ImageOwner kind,Guid owner,CancellationToken ct)=>inner.BeginAsync(kind,owner,ct);
        public Task<bool> OwnerExistsAsync(ImageOwner kind,Guid owner,CancellationToken ct)=>inner.OwnerExistsAsync(kind,owner,ct);
        public Task<Imagem?> GetAsync(ImageOwner kind,Guid owner,Guid id,CancellationToken ct)=>inner.GetAsync(kind,owner,id,ct);
        public Task<IReadOnlyList<Imagem>> ActiveAsync(ImageOwner kind,Guid owner,CancellationToken ct)=>inner.ActiveAsync(kind,owner,ct);
        public Task<AnimalEvolutionPage<Imagem>> ListAsync(ImageOwner kind,Guid owner,bool? active,int page,int size,CancellationToken ct)=>inner.ListAsync(kind,owner,active,page,size,ct);
        public Task AddAsync(Imagem image,CancellationToken ct)=>inner.AddAsync(image,ct);
        public Task SaveAsync(CancellationToken ct)=>throw new InvalidOperationException("Injected database failure.");
    }
}
