using System.Net;
using System.Net.Sockets;
using Npgsql;
using Xunit;

namespace GenSW.Infrastructure.Tests;

public sealed class EphemeralPostgreSqlStartupTests
{
    [Fact]
    public async Task Occupied_initial_port_is_replaced_and_the_returned_connection_targets_the_started_cluster()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var occupiedPort = ((IPEndPoint)listener.LocalEndpoint).Port;
            await using var cluster = await GenSW.API.Tests.EphemeralPostgreSql.StartAsync(occupiedPort);
            Assert.NotEqual(occupiedPort, cluster.Port);
            await using var connection = new NpgsqlConnection(cluster.ConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand("SELECT 1", connection);
            Assert.Equal(1, await command.ExecuteScalarAsync());
        }
        finally
        {
            listener.Stop();
        }
    }
}
