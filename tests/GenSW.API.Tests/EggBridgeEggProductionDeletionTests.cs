using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GenSW.Domain.EggBridge;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GenSW.API.Tests;

public sealed partial class EggBridgeApiTests
{
    [Fact]
    public async Task Egg_production_can_be_deleted_unless_reconciliation_or_bridge_references_it()
    {
        var (client, user) = await Session(true); using (client)
        {
            var eggs = await Eggs(3);
            string Path(int index) => $"/api/v1/animais/{eggs[index].AnimalId}/producoes-ovos/{eggs[index].Id}";
            using var anonymous = Factory.CreateHttpsClient();
            using (var response = await anonymous.DeleteAsync(Path(0))) Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            using (var response = await client.DeleteAsync($"/api/v1/animais/{Guid.NewGuid()}/producoes-ovos/{eggs[0].Id}")) Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            using (var response = await client.DeleteAsync(Path(0))) Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            using (var response = await client.DeleteAsync(Path(0))) Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            var list = await client.GetFromJsonAsync<JsonElement>($"/api/v1/animais/{eggs[0].AnimalId}/producoes-ovos");
            Assert.Equal(2, list.GetProperty("totalItems").GetInt32());
            Assert.Equal(2, list.GetProperty("metricas").GetProperty("totalLancamentos").GetInt32());

            var material = await Stock(client, user.UserId);
            var scope = await Scope(client, material, user.UserId, [eggs[1], eggs[2]]);
            using (var response = await client.DeleteAsync(Path(1)))
            {
                Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
                Assert.Equal("postura_vinculada", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
            }
            var preview = await Preview(client, Entry(material, scope.Id, user.UserId, eggs[2]));
            await Confirm(client, preview);
            using (var response = await client.DeleteAsync(Path(2))) Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal(2, await Factory.ExecuteDbContextAsync(db => db.ProducoesOvos.CountAsync(x => x.AnimalId == eggs[0].AnimalId)));
            Assert.Equal(1, await Factory.ExecuteDbContextAsync(db => db.Set<PonteProducaoOvo>().CountAsync(x => x.ProducaoOvoId == eggs[2].Id)));
        }
    }
}
