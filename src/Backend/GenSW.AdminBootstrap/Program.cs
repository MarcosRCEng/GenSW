using GenSW.Infrastructure;
using GenSW.Infrastructure.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddIdentityPersistence(builder.Configuration);
builder.Services.AddScoped<InitialAdminBootstrapper>();

var name = builder.Configuration["InitialAdminBootstrap:Name"];
var userName = builder.Configuration["InitialAdminBootstrap:Username"];
var password = builder.Configuration["InitialAdminBootstrap:Password"];

if (string.IsNullOrWhiteSpace(name) ||
    string.IsNullOrWhiteSpace(userName) ||
    string.IsNullOrWhiteSpace(password))
{
    Console.Error.WriteLine("Initial platform manager bootstrap configuration is incomplete.");
    return 1;
}

using var host = builder.Build();
using var scope = host.Services.CreateScope();
var bootstrapper = scope.ServiceProvider.GetRequiredService<InitialAdminBootstrapper>();

try
{
    await bootstrapper.BootstrapAsync(name, userName, password);
    Console.WriteLine("Initial platform manager grant verified.");
    return 0;
}
catch (InitialAdminBootstrapRejectedException)
{
    Console.Error.WriteLine("Initial platform manager bootstrap was rejected.");
    return 2;
}
catch (InitialAdminBootstrapException)
{
    Console.Error.WriteLine("Initial platform manager bootstrap failed.");
    return 3;
}
