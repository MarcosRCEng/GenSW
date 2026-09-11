using GenSW.Application.Animals;
using GenSW.Domain.Animals;
using Xunit;

namespace GenSW.Application.Tests;

public sealed class AnimalContractsTests
{
    [Fact]
    public void Contracts_preserve_all_constructor_values()
    {
        var especie = new AnimalEspecieResumo(Guid.NewGuid(), "Cão", true);
        var raca = new AnimalRacaResumo(Guid.NewGuid(), "Pastor", true);
        var variedade = new AnimalVariedadeResumo(Guid.NewGuid(), "Curto", false);
        var id = Guid.NewGuid();
        var createdAt = new DateTimeOffset(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);
        var command = new CreateAnimalCommand(null, null, especie.Id, raca.Id, variedade.Id,
            SexoAnimal.Macho, new DateOnly(2020, 1, 2), EscopoAnimal.Referencia);
        var update = new UpdateAnimalCommand("AN-000001", "Bela", especie.Id, raca.Id, variedade.Id,
            SexoAnimal.Femea, null, EscopoAnimal.Operacional);
        var readModel = new AnimalReadModel(id, "AN-000001", "Bela", especie.Id, raca.Id, variedade.Id,
            SexoAnimal.Femea, null, EscopoAnimal.Operacional, true, createdAt, createdAt, especie, raca, variedade);
        var page = new AnimalListPage([readModel], 1);
        var result = new AnimalResult(id, "AN-000001", "Bela", especie.Id, raca.Id, variedade.Id,
            SexoAnimal.Femea, null, EscopoAnimal.Operacional, true, createdAt, createdAt, especie, raca, variedade);
        var paged = new PagedAnimalResult([result], 1, 25, 1, 1);

        Assert.Null(command.CodigoInterno);
        Assert.Equal("AN-000001", update.CodigoInterno);
        Assert.Equal(id, Assert.Single(page.Items).Id);
        Assert.Equal("Cão", Assert.Single(paged.Items).Especie.NomeComum);
    }

    [Fact]
    public void Create_command_preserves_null_distinct_from_empty_codigo_interno()
    {
        var nullCode = new CreateAnimalCommand(null, null, Guid.NewGuid(), null, null,
            SexoAnimal.Indeterminado, null, EscopoAnimal.Operacional);
        var emptyCode = new CreateAnimalCommand("", null, Guid.NewGuid(), null, null,
            SexoAnimal.Indeterminado, null, EscopoAnimal.Operacional);

        Assert.Null(nullCode.CodigoInterno);
        Assert.Equal("", emptyCode.CodigoInterno);
    }

    [Fact]
    public void Animal_list_query_has_approved_defaults()
    {
        var query = new AnimalListQuery();

        Assert.Equal(1, query.Page);
        Assert.Equal(25, query.PageSize);
        Assert.Equal(AnimalSortField.CodigoInterno, query.SortBy);
        Assert.False(query.SortDescending);
    }

    [Fact]
    public void Animal_sort_fields_are_six_distinct_values()
    {
        var values = Enum.GetValues<AnimalSortField>();

        Assert.Equal(6, values.Length);
        Assert.Equal(values.Length, values.Distinct().Count());
        Assert.Contains(AnimalSortField.CodigoInterno, values);
        Assert.Contains(AnimalSortField.Nome, values);
        Assert.Contains(AnimalSortField.Sexo, values);
        Assert.Contains(AnimalSortField.Escopo, values);
        Assert.Contains(AnimalSortField.Ativo, values);
        Assert.Contains(AnimalSortField.CreatedAtUtc, values);
    }

    [Theory]
    [InlineData(AnimalDuplicateConflictSource.PreCheck)]
    [InlineData(AnimalDuplicateConflictSource.PersistedNamedCodigoInternoUniqueConstraint)]
    public void Duplicate_exception_preserves_explicit_structured_provenance(AnimalDuplicateConflictSource source)
    {
        var inner = new InvalidOperationException("unrelated database message");
        var exception = new AnimalDuplicateException("AN-000001", source, inner);

        Assert.Equal("AN-000001", exception.CodigoInterno);
        Assert.Equal(source, exception.ConflictSource);
        Assert.Same(inner, exception.InnerException);
    }

    [Fact]
    public void Animal_specific_exceptions_preserve_their_identity()
    {
        Assert.IsType<AnimalNotFoundException>(new AnimalNotFoundException(Guid.NewGuid()));
        Assert.IsType<AnimalAutomaticCodeCollisionLimitExceededException>(new AnimalAutomaticCodeCollisionLimitExceededException());
        Assert.IsType<AnimalCodeSequenceExhaustedException>(new AnimalCodeSequenceExhaustedException());
    }
}
