using GenSW.Application.People;
using GenSW.Application.Species;
using GenSW.Application.Breeds;
using GenSW.Application.Varieties;
using GenSW.Application.Animals;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GenSW.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IPessoaService, PessoaService>();
        services.AddScoped<IEspecieService, EspecieService>();
        services.AddScoped<IRacaService, RacaService>();
        services.AddScoped<IVariedadeService, VariedadeService>();
        services.AddScoped<AnimalClassificationValidator>();
        services.AddScoped<AnimalAutomaticCreator>();
        services.AddScoped<IAnimalService>(serviceProvider => new AnimalService(
            serviceProvider.GetRequiredService<IAnimalRepository>(),
            serviceProvider.GetRequiredService<AnimalClassificationValidator>(),
            serviceProvider.GetRequiredService<AnimalAutomaticCreator>(),
            serviceProvider.GetRequiredService<TimeProvider>()));

        return services;
    }
}
