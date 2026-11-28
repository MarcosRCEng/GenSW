using System.Globalization;
using GenSW.Application.Accounts;
using GenSW.Infrastructure.Catalog;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace GenSW.Infrastructure.Accounts;

public sealed class AccountResources(IConfiguration configuration)
{
    internal string RuntimeConnection(CatalogBinding binding) =>
        Connection("AccountRuntime", binding.DatabaseSecretRef, binding.DatabaseName);

    internal string MaintenanceConnection(CatalogBinding binding) =>
        Connection("AccountMaintenance", binding.MaintenanceSecretRef, binding.DatabaseName);

    internal string Volume(CatalogBinding binding) =>
        PrivateVolume.CanonicalPath(configuration[$"AccountVolumes:{binding.VolumeRef}"]);

    internal string? ConfiguredVolume(string reference) => configuration[$"AccountVolumes:{reference}"];

    private string Connection(string section, string reference, string databaseName)
    {
        if (string.IsNullOrWhiteSpace(reference) || string.IsNullOrWhiteSpace(databaseName))
            throw new AccountUnavailableException();

        var value = configuration[$"{section}:{reference}"];
        if (string.IsNullOrWhiteSpace(value))
            throw new AccountUnavailableException();

        var connection = new NpgsqlConnectionStringBuilder(value);
        if (connection.Database != databaseName || string.IsNullOrWhiteSpace(connection.Host)
            || string.IsNullOrWhiteSpace(connection.Username) || !string.IsNullOrEmpty(connection.Options)
            || connection.NoResetOnClose || connection.Multiplexing)
            throw new AccountUnavailableException();

        // Only the account-scoped connection owns the transaction and its lease.
        connection.Enlist = false;
        connection.SearchPath = "public";
        connection.Timeout = Math.Clamp(connection.Timeout, 1, 5);
        connection.CommandTimeout = Math.Clamp(connection.CommandTimeout, 1, 30);
        return connection.ConnectionString;
    }
}

internal sealed class AccountConnectionInterceptor(ErpAccountKey key) : DbConnectionInterceptor
{
    public override void ConnectionOpened(System.Data.Common.DbConnection connection, ConnectionEndEventData eventData)
    {
        using var command = CreateCommand((NpgsqlConnection)connection);
        command.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(System.Data.Common.DbConnection connection,
        ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        await using var command = CreateCommand((NpgsqlConnection)connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private NpgsqlCommand CreateCommand(NpgsqlConnection connection)
    {
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT set_config('gensw.binding', @binding, false),
                   set_config('gensw.revision', @revision, false),
                   set_config('gensw.generation', @generation, false)
            """;
        command.Parameters.AddWithValue("binding", key.BindingId.ToString("D"));
        command.Parameters.AddWithValue("revision", key.BindingRevision.ToString(CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("generation", key.AccessGeneration.ToString(CultureInfo.InvariantCulture));
        return command;
    }
}
