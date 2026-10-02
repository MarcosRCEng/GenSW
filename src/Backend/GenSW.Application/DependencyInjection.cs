using GenSW.Application.People;
using GenSW.Application.Species;
using GenSW.Application.Breeds;
using GenSW.Application.Varieties;
using GenSW.Application.Animals;
using GenSW.Application.Animals.Identificacoes;
using GenSW.Application.Animals.Registros;
using GenSW.Application.Animals.Filiacoes;
using GenSW.Application.Animals.Cruzamentos;
using GenSW.Application.Animals.CiclosReprodutivos;
using GenSW.Application.Animals.Proles;
using GenSW.Application.Animals.ProducaoOvos;
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
            serviceProvider.GetRequiredService<TimeProvider>(),
            serviceProvider.GetRequiredService<IAnimalMutationGuard>()));
        services.AddScoped<GenSW.Application.Animals.Pesagens.PesagemService>();
        services.AddScoped<GenSW.Application.Images.ImageService>();
        services.AddScoped<IIdentificacaoAnimalService, IdentificacaoAnimalService>();
        services.AddScoped<IRegistroAnimalService, RegistroAnimalService>();
        services.AddScoped<IFiliacaoAnimalService, FiliacaoAnimalService>();
        services.AddScoped<ICruzamentoService, CruzamentoService>();
        services.AddScoped<ICicloReprodutivoService, CicloReprodutivoService>();
        services.AddScoped<IProleService, ProleService>();
        services.AddScoped<IProducaoOvoService, ProducaoOvoService>();

        return services;
    }
}
