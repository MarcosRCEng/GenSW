using GenSW.Application.Breeds;
using GenSW.Application.Species;
using GenSW.Application.Varieties;
using GenSW.Domain.Animals;
using GenSW.Domain.Breeds;
using GenSW.Domain.Species;
using GenSW.Domain.Varieties;

namespace GenSW.Application.Animals;

internal sealed class AnimalClassificationValidator(
    IEspecieRepository especies,
    IRacaRepository racas,
    IVariedadeRepository variedades)
{
    public async Task ValidateCreateAsync(
        Guid especieId, Guid? racaId, Guid? variedadeId,
        CancellationToken cancellationToken = default)
    {
        var especie = await GetActiveEspecieAsync(especieId, cancellationToken);
        await ValidateNewRacaAsync(racaId, especie, cancellationToken);
        await ValidateNewVariedadeAsync(variedadeId, especie, cancellationToken);
    }

    public async Task ValidateUpdateAsync(
        Animal current, Guid especieId, Guid? racaId, Guid? variedadeId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(current);

        if (current.EspecieId != especieId)
        {
            var especie = await GetActiveEspecieAsync(especieId, cancellationToken);
            await ValidateNewRacaAsync(racaId, especie, cancellationToken);
            await ValidateNewVariedadeAsync(variedadeId, especie, cancellationToken);
            return;
        }

        if (current.RacaId != racaId)
        {
            await ValidateNewRacaAsync(racaId, especieId, cancellationToken);
        }

        if (current.VariedadeId != variedadeId)
        {
            await ValidateNewVariedadeAsync(variedadeId, especieId, cancellationToken);
        }
    }

    private async Task<Especie> GetActiveEspecieAsync(Guid especieId, CancellationToken cancellationToken)
    {
        var especie = await especies.GetByIdReadOnlyAsync(especieId, cancellationToken) ?? throw new EspecieNotFoundException(especieId);
        if (!especie.Ativo)
        {
            throw new ArgumentException("The animal must be linked to an active species.", nameof(especieId));
        }

        return especie;
    }

    private async Task ValidateNewRacaAsync(Guid? racaId, Especie especie, CancellationToken cancellationToken)
        => await ValidateNewRacaAsync(racaId, especie.Id, cancellationToken);

    private async Task ValidateNewRacaAsync(Guid? racaId, Guid especieId, CancellationToken cancellationToken)
    {
        if (racaId is null)
        {
            return;
        }

        var raca = await racas.GetByIdReadOnlyAsync(racaId.Value, cancellationToken) ?? throw new RacaNotFoundException(racaId.Value);
        if (!raca.Ativo || raca.EspecieId != especieId)
        {
            throw new ArgumentException("Breed must be active and belong to the animal species.", nameof(racaId));
        }
    }

    private async Task ValidateNewVariedadeAsync(Guid? variedadeId, Especie especie, CancellationToken cancellationToken)
        => await ValidateNewVariedadeAsync(variedadeId, especie.Id, cancellationToken);

    private async Task ValidateNewVariedadeAsync(Guid? variedadeId, Guid especieId, CancellationToken cancellationToken)
    {
        if (variedadeId is null)
        {
            return;
        }

        var variedade = await variedades.GetByIdReadOnlyAsync(variedadeId.Value, cancellationToken) ?? throw new VariedadeNotFoundException(variedadeId.Value);
        if (!variedade.Ativo || variedade.EspecieId != especieId)
        {
            throw new ArgumentException("Variety must be active and belong to the animal species.", nameof(variedadeId));
        }
    }
}
