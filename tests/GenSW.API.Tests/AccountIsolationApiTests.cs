using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using GenSW.Application.Accounts;
using GenSW.Infrastructure.Accounts;
using GenSW.Infrastructure.Catalog;
using GenSW.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace GenSW.API.Tests;

public sealed class AccountIsolationApiTests
{
    private static readonly Guid AnimalId = Guid.Parse("00000000-0000-4000-8000-000000000581");
    private static readonly Guid OtherAnimalId = Guid.Parse("00000000-0000-4000-8000-000000000582");
    private static readonly Guid SpeciesId = Guid.Parse("00000000-0000-4000-8000-000000000583");
    private static readonly Guid ImageId = Guid.Parse("00000000-0000-4000-8000-000000000584");
    private static readonly Guid OtherImageId = Guid.Parse("00000000-0000-4000-8000-000000000585");
    private static readonly Guid ActorId = Guid.Parse("00000000-0000-4000-8000-000000000586");
    private const string OriginalKey = "00000000000040008000000000000587.png";
    private const string ThumbnailKey = "00000000000040008000000000000588.png";
    private const string CatalogName = "gensw_581_catalog";
    private const string Origin = "https://localhost:5173";

    [Fact]
    public async Task Two_accounts_keep_sessions_data_pngs_permissions_and_revocation_isolated()
    {
        await using var server = await EphemeralPostgreSql.StartAsync();
        using var volumes = new SyntheticVolumes();
        var a = new SyntheticAccount("a", volumes.Root);
        var b = new SyntheticAccount("b", volumes.Root);
        await ProvisionAsync(server, a, b);

        var settings = Settings(server, a, b);
        using var app = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            foreach (var (key, value) in settings)
                builder.UseSetting(key, value);
        });



        using var client = app.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false,
        });
        var unaffected = await LoginAsync(client, b);
        using (var incompatible = await LoginResponseAsync(client, a.Login))
            Assert.Equal(HttpStatusCode.ServiceUnavailable, incompatible.StatusCode);

        await using (var migrationScope = app.Services.CreateAsyncScope())
        await using (var scriptContext = Erp(Connection(server, a.DbName)))
        {
            var services = migrationScope.ServiceProvider;
            var maintenance = new ErpSchemaMaintenance(services.GetRequiredService<CatalogDbContext>(),
                services.GetRequiredService<CatalogBindings>(), services.GetRequiredService<AccountLifecycle>(),
                services.GetRequiredService<AccountRevocations>(), services.GetRequiredService<IAccountDestinationVerifier>());
            var known = scriptContext.Database.GetMigrations().ToArray();
            var approval = new ApprovedErpMigration(known[^2], known[^1],
                ErpSchemaMaintenance.Fingerprint(ErpSchemaMaintenance.CreateReviewSql(scriptContext, known[^2], known[^1])));
            Assert.Equal(ErpMigrationStatus.Pending, await maintenance.RunAsync(a.Account, a.Binding,
                approval with { SqlSha256 = "unreviewed" }, Connection(server, a.DbName), default));
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/ready")).StatusCode);
            await AssertAnimalAsync(client, unaffected.Token, b.Name);
            Assert.Equal(ErpMigrationStatus.Completed, await maintenance.RunAsync(a.Account, a.Binding,
                approval, Connection(server, a.DbName), default));
            await AssertAnimalAsync(client, unaffected.Token, b.Name);
        }

        await RunUiIfRequestedAsync(settings, a, b);

        foreach (var (login, expected) in new[]
        {
            ("missing-581", HttpStatusCode.Unauthorized),
            ("pending-581", HttpStatusCode.Unauthorized),
            ("inactive-581", HttpStatusCode.Unauthorized),
            ("unbound-581", HttpStatusCode.ServiceUnavailable),
        })
        {
            using var rejected = await LoginResponseAsync(client, login);
            Assert.Equal(expected, rejected.StatusCode);
        }

        var sessions = await Task.WhenAll(LoginAsync(client, a), LoginAsync(client, b));
        var (sessionA, sessionB) = (sessions[0], sessions[1]);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/ready")).StatusCode);

        await Task.WhenAll(
            AssertAnimalAsync(client, sessionA.Token, a.Name),
            AssertAnimalAsync(client, sessionB.Token, b.Name));
        var updates = await Task.WhenAll(
            UpdateAsync(client, sessionA.Token, "Atualizado A"),
            UpdateAsync(client, sessionB.Token, "Atualizado B"));
        Assert.All(updates, result => Assert.Equal(HttpStatusCode.OK, result.StatusCode));
        await Task.WhenAll(
            AssertAnimalAsync(client, sessionA.Token, "Atualizado A"),
            AssertAnimalAsync(client, sessionB.Token, "Atualizado B"));
        Assert.Equal(HttpStatusCode.NotFound,
            (await SendAsync(client, HttpMethod.Get, $"/api/v1/animais/{OtherAnimalId}", sessionA.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await SendAsync(client, HttpMethod.Get, $"/api/v1/animais/{AnimalId}", "invalid")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(client, HttpMethod.Get,
            $"/api/v1/platform/accounts/{a.Account}/diagnostics", sessionA.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(client, HttpMethod.Post,
            $"/api/v1/estoque/locais/{AnimalId}/finalidade", sessionA.Token)).StatusCode);
        var operatorSession = await LoginAsync(client, a, platform: true);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Get,
            $"/api/v1/platform/accounts/{a.Account}/diagnostics", operatorSession.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await SendAsync(client, HttpMethod.Get, $"/api/v1/animais/{AnimalId}", operatorSession.Token)).StatusCode);

        foreach (var version in new[] { "visualizacao", "miniatura" })
        {
            var pngA = await AssertPngAsync(client, sessionA.Token, a, version);
            var pngB = await AssertPngAsync(client, sessionB.Token, b, version);
            Assert.False(pngA.SequenceEqual(pngB));
        }
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(client, HttpMethod.Get,
            $"/api/v1/animais/{AnimalId}/imagens/{OtherImageId}/conteudo", sessionA.Token)).StatusCode);
        await AssertCannotConnectAsync(Connection(server, b.DbName, a.RuntimeRole));
        await AssertCannotConnectAsync(Connection(server, a.DbName, b.RuntimeRole));
        await AssertCannotConnectAsync(Connection(server, CatalogName, a.RuntimeRole));

        var concurrent = await Task.WhenAll(
            RefreshAsync(client, sessionA), RefreshAsync(client, sessionA), RefreshAsync(client, sessionB));
        Assert.Equal(new[] { 200, 401 }, concurrent.Take(2).Select(x => (int)x.StatusCode).Order());
        Assert.Equal(HttpStatusCode.OK, concurrent[2].StatusCode);
        var refreshedB = (await concurrent[2].Content.ReadFromJsonAsync<TokenResult>())!;
        var newA = (await concurrent.Single(x => x.StatusCode == HttpStatusCode.OK && x != concurrent[2])
            .Content.ReadFromJsonAsync<TokenResult>())!;
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(client, HttpMethod.Get,
            $"/api/v1/animais/{AnimalId}", newA.AccessToken)).StatusCode);
        await AssertAnimalAsync(client, refreshedB.AccessToken, "Atualizado B");

        await using (var cluster = new NpgsqlConnection(Connection(server, "postgres")))
        {
            await cluster.OpenAsync();
            var probeA = await LoginAsync(client, a);
            await ExecuteAsync(cluster, $"ALTER DATABASE {a.DbName} ALLOW_CONNECTIONS false");
            try
            {
                await ExecuteAsync(cluster,
                    "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = @name", ("name", a.DbName));
                Assert.Equal(HttpStatusCode.ServiceUnavailable, (await SendAsync(client, HttpMethod.Get,
                    $"/api/v1/animais/{AnimalId}", probeA.Token)).StatusCode);
                await AssertAnimalAsync(client, refreshedB.AccessToken, "Atualizado B");
                _ = await AssertPngAsync(client, refreshedB.AccessToken, b, "miniatura");
                await LoginAsync(client, b);
            }
            finally { await ExecuteAsync(cluster, $"ALTER DATABASE {a.DbName} ALLOW_CONNECTIONS true"); }

            await ExecuteAsync(cluster, $"ALTER DATABASE {CatalogName} ALLOW_CONNECTIONS false");
            try
            {
                await ExecuteAsync(cluster,
                    "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = @name", ("name", CatalogName));
                Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/api/v1/ready")).StatusCode);
                Assert.Equal(HttpStatusCode.ServiceUnavailable, (await SendAsync(client, HttpMethod.Get,
                    $"/api/v1/animais/{AnimalId}", refreshedB.AccessToken)).StatusCode);
                using var loginWithoutCatalog = await LoginResponseAsync(client, b.Login);
                Assert.Equal(HttpStatusCode.ServiceUnavailable, loginWithoutCatalog.StatusCode);
            }
            finally { await ExecuteAsync(cluster, $"ALTER DATABASE {CatalogName} ALLOW_CONNECTIONS true"); }
        }

        var beforeFence = await LoginAsync(client, a);
        using var alreadyOpenVolume = PrivateVolume.Hold(a.Root, a.Volume, FileShare.Read);
        await using var activeWriter = new NpgsqlConnection(Connection(server, a.DbName, a.RuntimeRole));
        await activeWriter.OpenAsync();
        await AdmitAsync(activeWriter, a);
        await using var writerTransaction = await activeWriter.BeginTransactionAsync();
        await ExecuteAsync(activeWriter, """UPDATE "Animais" SET "Nome" = 'Confirmado antes da barreira'""");

        await using var scope = app.Services.CreateAsyncScope();
        var lifecycle = scope.ServiceProvider.GetRequiredService<AccountLifecycle>();
        var disabling = lifecycle.DeactivateAsync(a.Account, default);
        await WaitForAsync(async () =>
        {
            await using var catalog = Catalog(Connection(server, CatalogName));
            return await catalog.Accounts.AnyAsync(x => x.Id == a.Account && x.State == CatalogAccountState.Deactivating);
        });
        Assert.False(disabling.IsCompleted);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(client, HttpMethod.Get,
            $"/api/v1/animais/{AnimalId}", beforeFence.Token)).StatusCode);
        await writerTransaction.CommitAsync();
        Assert.False(disabling.IsCompleted);
        alreadyOpenVolume.Dispose();
        Assert.Equal(AccountDeactivation.Inactive, await disabling);
        Assert.Equal("Confirmado antes da barreira", await ScalarAsync(activeWriter, """SELECT "Nome" FROM "Animais" LIMIT 1"""));
        var rejection = await Assert.ThrowsAsync<PostgresException>(() =>
            ExecuteAsync(activeWriter, """UPDATE "Animais" SET "Nome" = 'Nunca confirmado'"""));
        Assert.Equal("GS001", rejection.SqlState);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(client, beforeFence)).StatusCode);
        await AssertAnimalAsync(client, refreshedB.AccessToken, "Atualizado B");

        await using (var destination = new NpgsqlConnection(Connection(server, a.DbName)))
        {
            await destination.OpenAsync();
            await ExecuteAsync(destination, "UPDATE gensw_scope.destination SET enabled = true, generation = 4");
        }
        await using (var reactivationScope = app.Services.CreateAsyncScope())
        {
            await reactivationScope.ServiceProvider.GetRequiredService<AccountLifecycle>()
                .ActivateAsync(a.Account, a.Binding, 1, 4, default);
        }
        var reactivated = await LoginAsync(client, a);
        await AssertAnimalAsync(client, reactivated.Token, "Confirmado antes da barreira");
        using var newAnimal = await SendAsync(client, HttpMethod.Post, "/api/v1/animais", reactivated.Token,
            content: JsonContent.Create(new { nome = "Código gerado após reativação", especieId = SpeciesId, sexo = 1, escopo = 1 }));
        Assert.Equal(HttpStatusCode.Created, newAnimal.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(client, HttpMethod.Get,
            $"/api/v1/animais/{AnimalId}", beforeFence.Token)).StatusCode);

        // A missing volume keeps a separately blocked account pending until the marker and files return.
        var bOffline = b.Root + "-offline";
        Directory.Move(b.Root, bOffline);
        try
        {
            await using var retryScope = app.Services.CreateAsyncScope();
            Assert.Equal(AccountDeactivation.Pending,
                await retryScope.ServiceProvider.GetRequiredService<AccountLifecycle>().DeactivateAsync(b.Account, default));
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/ready")).StatusCode);
        }
        finally { Directory.Move(bOffline, b.Root); }
        await using var reconciled = app.Services.CreateAsyncScope();
        Assert.Equal(AccountDeactivation.Inactive,
            await reconciled.ServiceProvider.GetRequiredService<AccountLifecycle>().DeactivateAsync(b.Account, default));
    }

    private sealed record TokenResult(string AccessToken, Guid SessionId);
    private sealed record Session(string Token, Guid Id, string Cookie);

    private static async Task<Session> LoginAsync(HttpClient client, SyntheticAccount account, bool platform = false)
    {
        using var response = await LoginResponseAsync(client, account.Login, platform);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<TokenResult>())!;
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie")).Split(';')[0];
        return new Session(result.AccessToken, result.SessionId, cookie);
    }

    private static Task<HttpResponseMessage> LoginResponseAsync(HttpClient client, string login, bool platform = false) =>
        SendAsync(client, HttpMethod.Post, platform ? "/api/v1/platform/auth/login" : "/api/v1/auth/login",
            content: JsonContent.Create(new { userName = login, password = AuthWebApplicationFactory.ValidPassword }));

    private static Task<HttpResponseMessage> RefreshAsync(HttpClient client, Session session) =>
        SendAsync(client, HttpMethod.Post, "/api/v1/auth/refresh", session: session);

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path,
        string? token = null, Session? session = null, HttpContent? content = null)
    {
        using var request = new HttpRequestMessage(method, path) { Content = content };
        request.Headers.Add("Origin", Origin);
        request.Headers.Add("X-Account-Id", OtherAnimalId.ToString("D"));
        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (session is not null)
        {
            request.Headers.Add("Cookie", session.Cookie);
            request.Headers.Add("X-GenSW-Session", session.Id.ToString("D"));
        }
        return await client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> UpdateAsync(HttpClient client, string token, string name) =>
        SendAsync(client, HttpMethod.Put, $"/api/v1/animais/{AnimalId}", token,
            content: JsonContent.Create(new { codigoInterno = "SYN-581", nome = name, especieId = SpeciesId, sexo = 1, escopo = 1 }));

    private static async Task AssertAnimalAsync(HttpClient client, string token, string name)
    {
        using var response = await SendAsync(client, HttpMethod.Get, $"/api/v1/animais/{AnimalId}?accountId={OtherAnimalId}", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(name, body.GetProperty("nome").GetString());
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    private static async Task<byte[]> AssertPngAsync(HttpClient client, string token, SyntheticAccount account, string version)
    {
        using var response = await SendAsync(client, HttpMethod.Get,
            $"/api/v1/animais/{AnimalId}/imagens/{ImageId}/conteudo?versao={version}", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.CacheControl?.Private);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(await File.ReadAllBytesAsync(Path.Combine(account.Root,
            version == "miniatura" ? ThumbnailKey : OriginalKey)), bytes);
        return bytes;
    }

    private static async Task AssertCannotConnectAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        var error = await Assert.ThrowsAsync<PostgresException>(() => connection.OpenAsync());
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
    }

    private static async Task WaitForAsync(Func<Task<bool>> predicate)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        while (!await predicate())
            await Task.Delay(30, timeout.Token);
    }

    private static string Connection(EphemeralPostgreSql server, string database, string? role = null)
    {
        var cfg = new NpgsqlConnectionStringBuilder(server.ConnectionString) { Database = database };
        if (role is not null) cfg.Username = role;
        return cfg.ConnectionString;
    }

    private static CatalogDbContext Catalog(string connection) =>
        new(new DbContextOptionsBuilder<CatalogDbContext>().UseNpgsql(connection).Options);

    private static GenSWDbContext Erp(string connection) =>
        new(new DbContextOptionsBuilder<GenSWDbContext>().UseNpgsql(connection).Options);

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<object?> ScalarAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync();
    }

    private static Task AdmitAsync(NpgsqlConnection connection, SyntheticAccount a) =>
        ExecuteAsync(connection, """
            SELECT set_config('gensw.binding', @binding, false),
                   set_config('gensw.revision', '1', false), set_config('gensw.generation', '2', false)
            """, ("binding", a.Binding.ToString("D")));

    private static async Task VerifyEmptyAsync(DbContext context, string expectedName)
    {
        await context.Database.OpenConnectionAsync();
        var connection = (NpgsqlConnection)context.Database.GetDbConnection();
        Assert.Equal(expectedName, await ScalarAsync(connection, "SELECT current_database()"));
        Assert.Empty(await context.Database.GetAppliedMigrationsAsync());
        Assert.NotEmpty(context.GetService<IMigrator>().GenerateScript());
    }

    private static async Task ProvisionAsync(EphemeralPostgreSql server, SyntheticAccount a, SyntheticAccount b)
    {
        await using var setup = new NpgsqlConnection(Connection(server, "postgres"));
        await setup.OpenAsync();
        await ExecuteAsync(setup, """
            REVOKE CONNECT ON DATABASE postgres, template1, gensw_auth_tests FROM PUBLIC
            """);
        await ExecuteAsync(setup, "CREATE ROLE gensw_581_catalog_app LOGIN");
        await ExecuteAsync(setup, $"CREATE DATABASE {CatalogName}");
        await ExecuteAsync(setup, $"REVOKE ALL ON DATABASE {CatalogName} FROM PUBLIC; GRANT CONNECT ON DATABASE {CatalogName} TO gensw_581_catalog_app");
        await using var catalog = Catalog(Connection(server, CatalogName));
        await VerifyEmptyAsync(catalog, CatalogName);
        await catalog.Database.MigrateAsync();
        var catConnection = (NpgsqlConnection)catalog.Database.GetDbConnection();
        await ExecuteAsync(catConnection, """
            REVOKE CREATE ON SCHEMA public FROM PUBLIC;
            GRANT USAGE ON SCHEMA public TO gensw_581_catalog_app;
            GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO gensw_581_catalog_app;
            """);
        var helpdesk = new IdentityRole<Guid>("ManageAccounts")
        {
            Id = Guid.NewGuid(),
            NormalizedName = "MANAGEACCOUNTS",
        };
        // Use the actual platform role name, independent of the local ERP roles.
        helpdesk.Name = GenSW.Infrastructure.Authentication.PlatformAuthentication.ManageAccountsRole;
        helpdesk.NormalizedName = helpdesk.Name.ToUpperInvariant();
        catalog.Roles.Add(helpdesk);

        foreach (var account in new[] { a, b })
        {
            await ExecuteAsync(setup,
                $"CREATE ROLE {account.RuntimeRole} LOGIN; CREATE ROLE {account.MaintenanceRole} LOGIN");
            await ExecuteAsync(setup, $"CREATE DATABASE {account.DbName}");
            await ExecuteAsync(setup, $"""
                REVOKE ALL ON DATABASE {account.DbName} FROM PUBLIC;
                GRANT CONNECT ON DATABASE {account.DbName} TO {account.RuntimeRole}, {account.MaintenanceRole};
                """);
            await using var db = Erp(Connection(server, account.DbName));
            await VerifyEmptyAsync(db, account.DbName);
            await db.GetService<IMigrator>().MigrateAsync(account == a ? db.Database.GetMigrations().ToArray()[^2] : null);
            var migration = (await db.Database.GetAppliedMigrationsAsync()).Last();

            Directory.CreateDirectory(account.Root);
            await File.WriteAllTextAsync(Path.Combine(account.Root, PrivateVolume.MarkerName), account.Volume.ToString("D"));
            await File.WriteAllBytesAsync(Path.Combine(account.Root, OriginalKey), MakePng(account.Suffix == "a", 8));
            await File.WriteAllBytesAsync(Path.Combine(account.Root, ThumbnailKey), MakePng(account.Suffix == "a", 4));
            var connection = (NpgsqlConnection)db.Database.GetDbConnection();
            await ExecuteAsync(connection, $"""
                REVOKE CREATE ON SCHEMA public FROM PUBLIC;
                GRANT USAGE ON SCHEMA public, gensw_scope TO {account.RuntimeRole};
                GRANT USAGE ON SCHEMA gensw_scope TO {account.MaintenanceRole};
                GRANT SELECT ON ALL TABLES IN SCHEMA public TO {account.RuntimeRole};
                GRANT USAGE ON SEQUENCE public."AnimalCodigoInternoSequence" TO {account.RuntimeRole};
                GRANT SELECT, INSERT, UPDATE, DELETE ON public."Animais", public."Especies",
                    public."ImagensAnimal" TO {account.RuntimeRole};
                GRANT EXECUTE ON FUNCTION gensw_scope.identity(), gensw_scope.verify_generation() TO {account.RuntimeRole};
                GRANT EXECUTE ON FUNCTION gensw_scope.advance_fence(uuid, uuid, uuid, bigint, bigint, boolean)
                    TO {account.MaintenanceRole};
                INSERT INTO gensw_scope.destination (database_id,account_id,binding_id,binding_revision,generation,enabled)
                    VALUES (@database,@account,@binding,1,1,true);
                INSERT INTO "Pessoas" ("Id","TipoPessoa","Nome","Ativo","CreatedAtUtc","UpdatedAtUtc")
                    VALUES (@pessoa,1,@name,true,now(),now());
                INSERT INTO "AspNetUsers" ("Id","PessoaId","IsActive","CreatedAtUtc","UpdatedAtUtc",
                    "EmailConfirmed","PhoneNumberConfirmed","TwoFactorEnabled","LockoutEnabled","AccessFailedCount")
                    VALUES (@actor,@pessoa,true,now(),now(),false,false,false,false,0);
                INSERT INTO "Especies" ("Id","NomeComum","Ovipara","Ativo","CreatedAtUtc","UpdatedAtUtc")
                    VALUES (@species,'Ave sintética',true,true,now(),now());
                INSERT INTO "Animais" ("Id","CodigoInterno","Nome","EspecieId","Sexo","Escopo","Ativo","CreatedAtUtc","UpdatedAtUtc")
                    VALUES (@animal,'SYN-581',@name,@species,1,1,true,now(),now());
                INSERT INTO "ImagensAnimal" ("Id","AnimalId","ArquivoKey","MiniaturaKey","Legenda","Ordem","Ativa",
                    "Representativa","Mime","Largura","Altura","TamanhoBytes","CreatedAtUtc","UpdatedAtUtc")
                    VALUES (@image,@animal,@original,@thumbnail,@name,0,true,true,'image/png',8,8,128,now(),now());
                """, ("database", account.Database), ("account", account.Account), ("binding", account.Binding),
                ("pessoa", Guid.NewGuid()), ("name", account.Name), ("actor", ActorId), ("species", SpeciesId),
                ("animal", AnimalId), ("image", ImageId), ("original", OriginalKey), ("thumbnail", ThumbnailKey));
            if (account == b)
                await ExecuteAsync(connection, """
                    INSERT INTO "Animais" ("Id","CodigoInterno","Nome","EspecieId","Sexo","Escopo","Ativo","CreatedAtUtc","UpdatedAtUtc")
                        VALUES (@other,'SYN-B-ONLY','Exclusivo B',@species,1,1,true,now(),now());
                    INSERT INTO "ImagensAnimal" ("Id","AnimalId","ArquivoKey","MiniaturaKey","Ordem","Ativa",
                        "Representativa","Mime","Largura","Altura","TamanhoBytes","CreatedAtUtc","UpdatedAtUtc")
                        VALUES (@image,@animal,@original,@thumbnail,1,true,false,'image/png',8,8,128,now(),now())
                    """, ("other", OtherAnimalId), ("species", SpeciesId), ("image", OtherImageId),
                    ("animal", AnimalId), ("original", OriginalKey), ("thumbnail", ThumbnailKey));

            var user = new CatalogUser
            {
                Id = Guid.NewGuid(), UserName = account.Login, NormalizedUserName = account.Login.ToUpperInvariant(),
                IsActive = true, DisplayName = account.Name, SecurityStamp = Guid.NewGuid().ToString("N"),
            };
            user.PasswordHash = new PasswordHasher<CatalogUser>().HashPassword(user, AuthWebApplicationFactory.ValidPassword);
            catalog.Users.Add(user);
            catalog.Accounts.Add(new CatalogAccount
            {
                Id = account.Account, SubjectId = user.Id, State = CatalogAccountState.Active, Confirmed = true,
            });
            catalog.Bindings.Add(new CatalogBinding
            {
                Id = account.Binding, AccountId = account.Account, Revision = 1, DatabaseId = account.Database,
                DatabaseName = account.DbName, DatabaseSecretRef = account.Suffix, VolumeId = account.Volume,
                VolumeRef = account.Suffix, MaintenanceSecretRef = account.Suffix, ErpActorId = ActorId,
                ExpectedMigration = migration, VerifiedMigration = migration, Ready = true, Published = true,
            });
            if (account == a)
                catalog.UserRoles.Add(new IdentityUserRole<Guid> { UserId = user.Id, RoleId = helpdesk.Id });
        }
        foreach (var (login, state) in new[]
        {
            ("pending-581", CatalogAccountState.Pending),
            ("inactive-581", CatalogAccountState.Inactive),
            ("unbound-581", CatalogAccountState.Active),
        })
        {
            var user = new CatalogUser
            {
                Id = Guid.NewGuid(), UserName = login, NormalizedUserName = login.ToUpperInvariant(),
                DisplayName = login, IsActive = true, SecurityStamp = Guid.NewGuid().ToString("N"),
            };
            user.PasswordHash = new PasswordHasher<CatalogUser>().HashPassword(user, AuthWebApplicationFactory.ValidPassword);
            catalog.Users.Add(user);
            catalog.Accounts.Add(new CatalogAccount
            {
                Id = Guid.NewGuid(), SubjectId = user.Id, State = state, Confirmed = state != CatalogAccountState.Pending,
            });
        }
        await catalog.SaveChangesAsync();
    }

    private static byte[] MakePng(bool forA, int size)
    {
        using var image = new ImageMagick.MagickImage(forA ? ImageMagick.MagickColors.Red : ImageMagick.MagickColors.Blue,
            (uint)size, (uint)size);
        return image.ToByteArray(ImageMagick.MagickFormat.Png);
    }

    private static Dictionary<string, string> Settings(EphemeralPostgreSql server, SyntheticAccount a, SyntheticAccount b)
    {
        var settings = new Dictionary<string, string>
        {
            ["ConnectionStrings:GenSWCatalog"] = Connection(server, CatalogName, "gensw_581_catalog_app"),
            ["Authentication:Jwt:Issuer"] = AuthWebApplicationFactory.JwtIssuer,
            ["Authentication:Jwt:Audience"] = AuthWebApplicationFactory.JwtAudience,
            ["Authentication:Jwt:SigningKey"] = AuthWebApplicationFactory.JwtSigningKey,
            ["Cors:AllowedOrigins:0"] = Origin,
            ["RateLimiting:Login:PermitLimit"] = "1000",
        };
        foreach (var account in new[] { a, b })
        {
            settings[$"AccountRuntime:{account.Suffix}"] = Connection(server, account.DbName, account.RuntimeRole);
            settings[$"AccountMaintenance:{account.Suffix}"] = Connection(server, account.DbName, account.MaintenanceRole);
            settings[$"AccountVolumes:{account.Suffix}"] = account.Root;
        }
        return settings;
    }

    private static async Task RunUiIfRequestedAsync(Dictionary<string, string> settings, SyntheticAccount a, SyntheticAccount b)
    {
        var apiDll = Environment.GetEnvironmentVariable("GENSW_581_UI_API_DLL");
        var manifest = Environment.GetEnvironmentVariable("GENSW_581_UI_MANIFEST");
        if (apiDll is null || manifest is null) return;

        var portProbe = new TcpListener(IPAddress.Loopback, 0);
        portProbe.Start();
        var port = ((IPEndPoint)portProbe.LocalEndpoint).Port;
        portProbe.Stop();
        var apiUrl = $"https://localhost:{port}";
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add(apiDll);
        start.ArgumentList.Add("--urls");
        start.ArgumentList.Add(apiUrl);
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Testing";
        foreach (var (key, value) in settings)
            start.Environment[key.Replace(":", "__")] = value;

        using var process = Process.Start(start) ?? throw new InvalidOperationException("Synthetic API did not start.");
        try
        {
            using var client = new HttpClient(new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
            });
            await WaitForAsync(async () =>
            {
                try { return (await client.GetAsync(apiUrl + "/api/v1/ready")).IsSuccessStatusCode; }
                catch (HttpRequestException) { return false; }
            });
            var password = AuthWebApplicationFactory.ValidPassword;
            await File.WriteAllTextAsync(manifest, JsonSerializer.Serialize(new
            {
                apiUrl, animalId = AnimalId, imageId = ImageId,
                accountA = new { login = a.Login, password, name = a.Name },
                accountB = new { login = b.Login, password, name = b.Name },
            }));
            using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            while (!File.Exists(manifest + ".done"))
                await Task.Delay(150, deadline.Token);
        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            File.Delete(manifest);
        }
    }

    private sealed class SyntheticVolumes : IDisposable
    {
        public string Root { get; } = Directory.CreateTempSubdirectory("gensw-581-volume-").FullName;
        public void Dispose() => Directory.Delete(Root, true);
    }

    private sealed record SyntheticAccount(string Suffix, string VolumeParent)
    {
        public Guid Account { get; } = Guid.NewGuid();
        public Guid Binding { get; } = Guid.NewGuid();
        public Guid Database { get; } = Guid.NewGuid();
        public Guid Volume { get; } = Guid.NewGuid();
        public string Login => $"ensaio-581-{Suffix}";
        public string DbName => $"gensw_581_erp_{Suffix}";
        public string RuntimeRole => $"gensw_581_runtime_{Suffix}";
        public string MaintenanceRole => $"gensw_581_maintenance_{Suffix}";
        public string Root => Path.Combine(VolumeParent, Suffix);
        public string Name => $"Animal sintético {Suffix.ToUpperInvariant()}";
    }
}
