using System.Net;
using System.Threading.RateLimiting;
using GenSW.API.Authentication;
using GenSW.API.Deployment;
using GenSW.Application;
using GenSW.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Database driver exceptions can contain destination names or query details.
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.None);
builder.Logging.AddFilter("Npgsql", LogLevel.None);

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>()?
    .Where(origin => !string.IsNullOrWhiteSpace(origin))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray() ?? [];

if (allowedOrigins.Contains("*", StringComparer.Ordinal))
{
    throw new InvalidOperationException(
        "Cors:AllowedOrigins cannot contain a wildcard when credentials are enabled.");
}

var loginPermitLimit = builder.Configuration.GetValue<int?>("RateLimiting:Login:PermitLimit") ?? 10;
var loginWindowSeconds = builder.Configuration.GetValue<int?>("RateLimiting:Login:WindowSeconds") ?? 60;

if (loginPermitLimit <= 0 || loginWindowSeconds <= 0)
{
    throw new InvalidOperationException(
        "RateLimiting:Login:PermitLimit and RateLimiting:Login:WindowSeconds must be positive.");
}

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
var protection = builder.Services.AddDataProtection().SetApplicationName("GenSW");
var keyRing = builder.Configuration["DataProtection:KeyRingDirectory"];
if (builder.Environment.IsEnvironment("Testing") && string.IsNullOrWhiteSpace(keyRing))
{
    builder.Services.AddSingleton<IDataProtectionProvider, EphemeralDataProtectionProvider>();
}
else
{
    if (string.IsNullOrWhiteSpace(keyRing) || !Path.IsPathFullyQualified(keyRing) || !Directory.Exists(keyRing))
        throw new InvalidOperationException("DataProtection:KeyRingDirectory must identify the managed key ring.");
    protection.PersistKeysToFileSystem(new DirectoryInfo(keyRing)).DisableAutomaticKeyGeneration();
}
builder.Services.AddHealthChecks()
    .AddCheck<CatalogReadinessCheck>("catalog", timeout: TimeSpan.FromSeconds(4));
builder.Services.AddControllers(options => { options.Filters.Add<GenSW.API.Controllers.AnimalEvolutionFilter>(); options.Filters.Add<GenSW.API.Controllers.FormulationFilter>(); options.Filters.Add<GenSW.API.Controllers.InventoryFilter>(); options.Filters.Add<GenSW.API.Controllers.ProductionFilter>(); options.Filters.Add<GenSW.API.Controllers.EggBridgeFilter>(); options.Filters.Add<GenSW.API.Controllers.MaterialCostsFilter>(); });
builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(options =>
{
    var original = options.InvalidModelStateResponseFactory;
    options.InvalidModelStateResponseFactory = context =>
    {
        var result = original(context);
        if (result is Microsoft.AspNetCore.Mvc.ObjectResult { Value: Microsoft.AspNetCore.Mvc.ProblemDetails problem })
            problem.Extensions["code"] = "dados_invalidos";
        return result;
    };
});
builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    options.AddPolicy(ApiPolicyNames.FrontendCors, policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins);
        }

        policy
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(ApiPolicyNames.LoginRateLimit, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = loginPermitLimit,
                Window = TimeSpan.FromSeconds(loginWindowSeconds),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true
            }));
});

var app = builder.Build();

var trustedProxy = builder.Configuration["ReverseProxy:TrustedAddress"];
if (!string.IsNullOrWhiteSpace(trustedProxy))
{
    if (!IPAddress.TryParse(trustedProxy, out var proxyAddress) ||
        proxyAddress.Equals(IPAddress.Any) || proxyAddress.Equals(IPAddress.IPv6Any))
    {
        throw new InvalidOperationException("ReverseProxy:TrustedAddress must be a single proxy IP address.");
    }

    var forwardedHeaders = new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
        ForwardLimit = 1,
        RequireHeaderSymmetry = true
    };
    forwardedHeaders.KnownNetworks.Clear();
    forwardedHeaders.KnownProxies.Clear();
    forwardedHeaders.KnownProxies.Add(proxyAddress);
    app.UseForwardedHeaders(forwardedHeaders);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseCors(ApiPolicyNames.FrontendCors);
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<ErpAccountMiddleware>();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/api/v1/ready").AllowAnonymous();

app.Run();

public partial class Program;
