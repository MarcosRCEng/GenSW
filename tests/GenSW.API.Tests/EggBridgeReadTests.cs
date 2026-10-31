using System.Net;
using System.Text.Json;
using GenSW.Application.EggBridge;
using GenSW.Domain.EggBridge;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GenSW.API.Tests;

public sealed partial class EggBridgeApiTests
{
    [Fact]
    public async Task F03_02_20_23_authenticated_reads_page_actual_candidates_and_expose_history_and_approved_references()
    {
        var (admin, user) = await Session(true); var (common, _) = await Session(); using (admin) using (common)
        {
            var m = await Stock(admin, user.UserId); var eggs = await Eggs(3); var scope = await Scope(admin, m, user.UserId, eggs);
            var query = Root + $"candidatos?animalId={eggs[0].AnimalId}&pageSize=1";
            var first = await Success<JsonElement>(await common.GetAsync(query + "&page=1"), HttpStatusCode.OK);
            var second = await Success<JsonElement>(await common.GetAsync(query + "&page=2"), HttpStatusCode.OK);
            var third = await Success<JsonElement>(await common.GetAsync(query + "&page=3"), HttpStatusCode.OK);
            Assert.Equal(3, first.GetProperty("totalItems").GetInt32()); Assert.Equal(3, first.GetProperty("totalPages").GetInt32());
            static JsonElement Row(JsonElement page) => Assert.Single(page.GetProperty("items").EnumerateArray());
            static Guid Id(JsonElement row) => row.GetProperty("origem").GetProperty("id").GetGuid();
            Assert.Equal(3, new[] { Id(Row(first)), Id(Row(second)), Id(Row(third)) }.Distinct().Count());
            Assert.Equal(JsonValueKind.Null, Row(first).GetProperty("ponteId").ValueKind);
            var selected = eggs.Single(x => x.Id == Id(Row(first)));
            var detail = await Confirm(common, await Preview(common, Entry(m, scope.Id, user.UserId, selected)));
            var linked = await Success<JsonElement>(await common.GetAsync(query + "&estado=ComPonte"), HttpStatusCode.OK);
            Assert.Equal(detail.Grupo.Id, Row(linked).GetProperty("grupoId").GetGuid());
            var free = await Success<JsonElement>(await common.GetAsync(query + "&estado=SemPonte"), HttpStatusCode.OK);
            Assert.Equal(2, free.GetProperty("totalItems").GetInt32());
            var groups = await Success<EggBridgePage<GrupoPonteOvos>>(await common.GetAsync(Root + $"grupos?itemId={m.Item.Id}&pageSize=1"), HttpStatusCode.OK);
            Assert.Equal(detail.Grupo.Id, Assert.Single(groups.Items).Id);
            var historical = await Success<EggBridgeDetail>(await common.GetAsync(Root + "grupos/" + detail.Grupo.Id), HttpStatusCode.OK);
            Assert.Equal(detail.Grupo.Id, historical.Grupo.Id);
            var scopes = await Success<EggBridgePage<RecorteConciliacaoOvos>>(await common.GetAsync(Root + $"recortes?itemId={m.Item.Id}&pageSize=1"), HttpStatusCode.OK);
            Assert.Equal(scope.Id, Assert.Single(scopes.Items).Id);
            var scopeDetail = await Success<RecorteConciliacaoOvos>(await common.GetAsync(Root + "recortes/" + scope.Id), HttpStatusCode.OK);
            Assert.Equal(scope.Id, scopeDetail.Id);

            var manual = await Stock(admin, user.UserId, "5"); var manualEggs = await Eggs(1);
            await Scope(admin, manual, user.UserId, manualEggs, "RecebidoComEquivalencia");
            var move = await Factory.ExecuteDbContextAsync(db => db.MovimentosEstoque.SingleAsync(x => x.LoteId == manual.Lot.Id));
            var proof = await Success<ProvaEntradaOvos>(await Post(admin, "provas", new EggProofCommand(move.Id, "5", user.UserId, Evidence, Evidence)));
            var proofs = await Success<EggBridgePage<ProvaEntradaOvos>>(await common.GetAsync(Root + $"provas?itemId={manual.Item.Id}&pageSize=1"), HttpStatusCode.OK);
            Assert.Equal(proof.Id, Assert.Single(proofs.Items).Id);
            Assert.Equal(proof.Id, (await Success<ProvaEntradaOvos>(await common.GetAsync(Root + "provas/" + proof.Id), HttpStatusCode.OK)).Id);
            await Error(await common.GetAsync(Root + "candidatos?pageSize=101"), HttpStatusCode.BadRequest);
        }
    }
}
