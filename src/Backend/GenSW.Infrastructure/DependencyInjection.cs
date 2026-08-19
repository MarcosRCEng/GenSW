using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GenSW.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("GenSW");

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddDbContext<GenSWDbContext>(options => options.UseNpgsql(connectionString));
        }

        return services;
    }
}
