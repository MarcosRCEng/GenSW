using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GenSW.Domain.Animals;
using GenSW.Domain.Species;
using GenSW.Domain.Varieties;
using GenSW.Infrastructure.Persistence;
using ImageMagick;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace GenSW.API.Tests;

public sealed class AnimalEvolutionApiTests
{
    [Fact]
    public async Task Authenticated_weight_image_candidate_tree_contracts_and_negative_cases()
    {
        await using var pg=await EphemeralPostgreSql.StartAsync();
        await using var factory=new AuthWebApplicationFactory(pg.ConnectionString);
        await factory.InitializeAsync();
        var volume=Path.Combine(Path.GetTempPath(),"gensw-api-images-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(volume);
        try
        {
            using var configured=factory.WithWebHostBuilder(builder=>builder.ConfigureAppConfiguration((_,config)=>config.AddInMemoryCollection(new Dictionary<string,string?>{["Images:PrivateRoot"]=volume})));
            await factory.SeedUserAsync("evolution");
            using var client=configured.CreateClient(new(){BaseAddress=new Uri("https://localhost")});
            using var login=await client.LoginAsync("evolution",AuthWebApplicationFactory.ValidPassword);
            client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",(await login.ReadAccessTokenAsync()).AccessToken);
            Guid animalId=default,parentId=default,varietyId=default;
            await factory.ExecuteDbContextAsync(async db=>
            {
                var now=DateTimeOffset.UtcNow; var species=Especie.Criar("API evolução",null,now);
                var animal=Animal.Criar("EV1","Filho",species.Id,null,null,SexoAnimal.Macho,new(2026,8,1),EscopoAnimal.Operacional,DateOnly.FromDateTime(now.UtcDateTime),now);
                var parent=Animal.Criar("EV2","Pai",species.Id,null,null,SexoAnimal.Macho,null,EscopoAnimal.Operacional,DateOnly.FromDateTime(now.UtcDateTime),now);
                var variety=Variedade.Criar(species.Id,"Catálogo",now); db.AddRange(species,animal,parent,variety); await db.SaveChangesAsync();
                animalId=animal.Id; parentId=parent.Id; varietyId=variety.Id;
            });
            var root=$"/api/v1/animais/{animalId}";
            using var anonymous=configured.CreateClient(new(){BaseAddress=new Uri("https://localhost")});
            foreach(var suffix in new[]{"/pesagens","/imagens","/imagens/preferencial","/arvore","/progenitores-elegiveis?tipoFiliacao=1"})
                Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.GetAsync(root+suffix)).StatusCode);
            using var weight=await client.PostAsJsonAsync(root+"/pesagens",new{dataMedicao="2026-09-01",pesoGramas=123.45m,tipoMarco=3,idadeReferenciaDias=30});
            Assert.Equal(HttpStatusCode.Created,weight.StatusCode);
            var body=await weight.Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal(31,body.GetProperty("idadeDiasNaMedicao").GetInt32());
            Assert.Equal(HttpStatusCode.OK,(await client.GetAsync(weight.Headers.Location)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync($"/api/v1/animais/{parentId}/pesagens/{body.GetProperty("id").GetGuid()}")).StatusCode);
            foreach(var invalid in new[]{"?pageSize=101","?tipoMarco=8","?dataInicial=2026-09-02&dataFinal=2026-09-01"})
            {
                var response=await client.GetAsync(root+"/pesagens"+invalid); Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);
                Assert.Equal("dados_invalidos",(await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
            }
            var candidates=await client.GetFromJsonAsync<JsonElement>(root+"/progenitores-elegiveis?tipoFiliacao=1&search=EV2"); Assert.Equal(1,candidates.GetProperty("totalItems").GetInt32());
            var link=await client.PostAsJsonAsync(root+"/filiacoes",new{progenitorId=parentId,tipoFiliacao=1}); Assert.Equal(HttpStatusCode.Created,link.StatusCode);
            var cycle=await client.PostAsJsonAsync($"/api/v1/animais/{parentId}/filiacoes",new{progenitorId=animalId,tipoFiliacao=1}); Assert.Equal(HttpStatusCode.Conflict,cycle.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest,(await client.GetAsync(root+"/arvore?ascendentes=5")).StatusCode);
            var tree=await client.GetFromJsonAsync<JsonElement>(root+"/arvore"); Assert.Equal(2,tree.GetProperty("nos").GetArrayLength()); Assert.Single(tree.GetProperty("arestas").EnumerateArray());
            Assert.Equal(HttpStatusCode.OK,(await client.GetAsync(root+"/pedigree?generations=8")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest,(await client.GetAsync(root+"/pedigree?generations=9")).StatusCode);
            using var png=new MagickImage(MagickColors.Green,16,16);
            using var multipart=new MultipartFormDataContent(); multipart.Add(new ByteArrayContent(png.ToByteArray(MagickFormat.Png)),"arquivo","../../untrusted.svg");
            using var uploaded=await client.PostAsync(root+"/imagens",multipart); Assert.Equal(HttpStatusCode.Created,uploaded.StatusCode);
            var photo=await uploaded.Content.ReadFromJsonAsync<JsonElement>(); var photoId=photo.GetProperty("id").GetGuid();
            Assert.False(photo.TryGetProperty("arquivoKey",out _));
            var path=photo.GetProperty("thumbnailPath").GetString()!;
            Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.GetAsync(path)).StatusCode);
            using var content=await client.GetAsync(path); Assert.Equal(HttpStatusCode.OK,content.StatusCode);
            Assert.Equal("image/png",content.Content.Headers.ContentType!.MediaType); Assert.True(content.Headers.CacheControl!.NoStore); Assert.True(content.Headers.CacheControl.Private);
            Assert.Equal("nosniff",content.Headers.GetValues("X-Content-Type-Options").Single());
            Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync($"/api/v1/variedades/{varietyId}/imagens/{photoId}/conteudo")).StatusCode);
            await client.PutAsJsonAsync(root+$"/imagens/{photoId}/ativo",new{ativo=false}); Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync(path)).StatusCode);
            using var bad=new MultipartFormDataContent(); bad.Add(new ByteArrayContent("<svg/>"u8.ToArray()),"arquivo","fake.png");
            Assert.Equal(HttpStatusCode.UnsupportedMediaType,(await client.PostAsync(root+"/imagens",bad)).StatusCode);
            using var large=new MultipartFormDataContent(); large.Add(new ByteArrayContent(new byte[5*1024*1024+1]),"arquivo","large.png");
            Assert.Equal(HttpStatusCode.RequestEntityTooLarge,(await client.PostAsync(root+"/imagens",large)).StatusCode);
        }
        finally { Directory.Delete(volume,true); }
    }
}
