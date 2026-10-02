using GenSW.Application.Animals;
using GenSW.Domain.Animals;
using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GenSW.Infrastructure.Animals;

public sealed class AnimalMutationGuard(GenSWDbContext context) : IAnimalMutationGuard
{
    public async Task<IAnimalMutationScope> BeginAsync(Guid id, CancellationToken ct) =>
        await AnimalMutationScope.BeginAsync(context, id, true, ct);

    public async Task ValidateAsync(Animal animal, UpdateAnimalCommand command, CancellationToken ct)
    {
        if (animal.EspecieId != command.EspecieId || animal.RacaId != command.RacaId || animal.Sexo != command.Sexo)
        {
            var childConflict = await (from f in context.FiliacoesAnimal
                join parent in context.Animais on f.ProgenitorId equals parent.Id
                where f.Ativa && f.AnimalId == animal.Id &&
                    (parent.EspecieId != command.EspecieId || (command.RacaId != null && parent.RacaId != command.RacaId) ||
                     (f.TipoFiliacao == TipoFiliacaoAnimal.Pai ? parent.Sexo != SexoAnimal.Macho : parent.Sexo != SexoAnimal.Femea))
                select f.Id).AnyAsync(ct);
            var parentConflict = await (from f in context.FiliacoesAnimal
                join child in context.Animais on f.AnimalId equals child.Id
                where f.Ativa && f.ProgenitorId == animal.Id &&
                    (child.EspecieId != command.EspecieId || (child.RacaId != null && child.RacaId != command.RacaId) ||
                     (f.TipoFiliacao == TipoFiliacaoAnimal.Pai ? command.Sexo != SexoAnimal.Macho : command.Sexo != SexoAnimal.Femea))
                select f.Id).AnyAsync(ct);
            if (childConflict || parentConflict)
                throw new AnimalEvolutionException(409, "cadastro_incompativel_filiacao", "Alteração incompatível com filiação vigente. Corrija explicitamente os vínculos envolvidos.");
        }
        if (animal.DataNascimento != command.DataNascimento && command.DataNascimento is { } birth &&
            await context.PesagensAnimal.AnyAsync(x => x.AnimalId == animal.Id &&
                (x.DataMedicao < birth || (x.TipoMarco == TipoMarcoPesagem.Nascimento && x.DataMedicao != birth)), ct))
            throw new AnimalEvolutionException(409, "nascimento_incompativel_pesagens", "Corrija explicitamente as pesagens incompatíveis antes de alterar o nascimento.");
    }
}
