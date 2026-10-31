using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using GenSW.Application.EggBridge;
using GenSW.Domain.EggBridge;
using GenSW.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GenSW.API.Tests;

public sealed partial class EggBridgeApiTests
{
    [Fact]
    public async Task F03_09_sql_and_application_hashes_agree_for_unicode_long_values_null_and_empty()
    {
        var eggs = await Eggs(1); var hashes = new List<string>();
        foreach (var observation in new string?[] { null, "", "Ação 🥚 — çã\n\"linha\"; 空", string.Concat(Enumerable.Repeat("Á🥚\n", 180)) })
        {
            await Factory.ExecuteDbContextAsync(db => observation is null
                ? db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"ProducoesOvos\" SET \"Observacao\"=NULL WHERE \"Id\"={eggs[0].Id}")
                : db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"ProducoesOvos\" SET \"Observacao\"={observation} WHERE \"Id\"={eggs[0].Id}"));
            var result = await Factory.ExecuteDbContextAsync(async db =>
            {
                var source = await db.ProducoesOvos.AsNoTracking().SingleAsync(x => x.Id == eggs[0].Id);
                var sql = await db.Database.SqlQuery<string>($"SELECT \"EggCanonicalSourceHash\"(source_row) AS \"Value\" FROM \"ProducoesOvos\" source_row WHERE \"Id\"={source.Id}").SingleAsync();
                return (Application: EggBridgeService.SourceHash(source), Sql: sql);
            });
            Assert.Equal(result.Application, result.Sql); hashes.Add(result.Application);
        }
        Assert.Equal(4, hashes.Distinct().Count()); // Null, empty, Unicode and long text remain distinct facts.
    }

    [Theory]
    [InlineData("SemanticJson")]
    [InlineData("RawJsonHash")]
    [InlineData("LowercaseHash")]
    [InlineData("MissingNullObservation")]
    public async Task F03_18_sql_hash_checks_canonical_content_without_requiring_json_byte_format(string variant)
    {
        var (client, user) = await Session(true); using (client)
        {
            var m = await Stock(client, user.UserId, "2"); var eggs = await Eggs(2);
            await Factory.ExecuteDbContextAsync(db => variant == "MissingNullObservation"
                ? db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"ProducoesOvos\" SET \"Observacao\"=NULL WHERE \"Id\"={eggs[1].Id}")
                : db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"ProducoesOvos\" SET \"Observacao\"={"Ação 🥚; 空\nsegunda linha"} WHERE \"Id\"={eggs[1].Id}"));
            var move = await Factory.ExecuteDbContextAsync(db => db.MovimentosEstoque.SingleAsync(x => x.LoteId == m.Lot.Id));
            var scope = await Scope(client, m, user.UserId, eggs, "RecebidoComEquivalencia");
            var proof = await Success<ProvaEntradaOvos>(await Post(client, "provas", new EggProofCommand(move.Id, "2", user.UserId, Evidence, Evidence)));
            var template = await Confirm(client, await Preview(client, Association(scope.Id, user.UserId, proof.Id, [eggs[0]], 1)));
            var before = await Snapshot();
            async Task<Guid> Insert()
            {
                return await Factory.ExecuteDbContextAsync(async db =>
                {
                    await using var tx = await db.Database.BeginTransactionAsync();
                    var source = await db.ProducoesOvos.AsNoTracking().SingleAsync(x => x.Id == eggs[1].Id);
                    var original = EggBridgeService.SourceSnapshot(source);
                    using var doc = JsonDocument.Parse(original);
                    var reordered = doc.RootElement.EnumerateObject().Reverse().Where(x => variant != "MissingNullObservation" || x.Name != "observacao")
                        .ToDictionary(x => x.Name, x => x.Value.Clone());
                    var snapshot = JsonSerializer.Serialize(reordered, new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
                    Assert.NotEqual(original, snapshot);
                    var hash = EggBridgeService.SourceHash(source);
                    if (variant == "RawJsonHash") hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(snapshot)));
                    if (variant == "LowercaseHash") hash = hash.ToLowerInvariant();
                    var group = template.Grupo with { Id = Guid.NewGuid(), CreatedAtUtc = DateTimeOffset.UtcNow };
                    var bridge = new PonteProducaoOvo(Guid.NewGuid(), source.Id, group.Id, snapshot, hash, source.UpdatedAtUtc, EggBridgeService.Algorithm);
                    db.AddRange(group, bridge, new AlocacaoProvaOvos(Guid.NewGuid(), bridge.Id, proof.Id, move.Id, 2),
                        new ComandoPonteOvos(Guid.NewGuid(), "Associacao", Unique(), new string('c', 64), user.UserId, group.Id, true, 201, Root + "grupos/" + group.Id, InventoryJson.Write(new { Grupo = group }), DateTimeOffset.UtcNow));
                    await db.SaveChangesAsync(); await tx.CommitAsync(); return group.Id;
                });
            }
            if (variant == "SemanticJson")
            {
                var id = await Insert();
                var detail = await Success<EggBridgeDetail>(await client.GetAsync(Root + "grupos/" + id));
                Assert.True(detail.Conciliacao.Coerente); Assert.Null(Assert.Single(detail.OrigensAtuais).Impedimento);
                Assert.Equal("2", (await Balance(client, m))!.Quantidade);
            }
            else
            {
                var error = await Assert.ThrowsAnyAsync<Exception>(Insert);
                Assert.Equal("23514", PostgresFailure(error)?.SqlState); Assert.Equal(before, await Snapshot());
            }
        }
    }
}
