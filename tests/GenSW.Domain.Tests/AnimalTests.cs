using GenSW.Domain.Animals;
using Xunit;

namespace GenSW.Domain.Tests;

public sealed class AnimalTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 9, 8);
    private static readonly Guid EspecieId = Guid.NewGuid();
    private static readonly Guid RacaId = Guid.NewGuid();
    private static readonly Guid VariedadeId = Guid.NewGuid();

    [Fact]
    public void Criar_builds_minimum_valid_active_animal()
    {
        var animal = Criar();

        Assert.NotEqual(Guid.Empty, animal.Id);
        Assert.Equal("AN-000001", animal.CodigoInterno);
        Assert.Null(animal.Nome);
        Assert.Equal(EspecieId, animal.EspecieId);
        Assert.Null(animal.RacaId);
        Assert.Null(animal.VariedadeId);
        Assert.Equal(SexoAnimal.Indeterminado, animal.Sexo);
        Assert.Null(animal.DataNascimento);
        Assert.Equal(EscopoAnimal.Operacional, animal.Escopo);
        Assert.True(animal.Ativo);
        Assert.Equal(CreatedAt, animal.CreatedAtUtc);
        Assert.Equal(CreatedAt, animal.UpdatedAtUtc);
    }

    [Theory]
    [InlineData(SexoAnimal.Macho)]
    [InlineData(SexoAnimal.Femea)]
    [InlineData(SexoAnimal.Indeterminado)]
    public void Criar_accepts_each_defined_sexo(SexoAnimal sexo)
        => Assert.Equal(sexo, Criar(sexo: sexo).Sexo);

    [Theory]
    [InlineData(EscopoAnimal.Operacional)]
    [InlineData(EscopoAnimal.Referencia)]
    public void Criar_accepts_each_defined_escopo(EscopoAnimal escopo)
        => Assert.Equal(escopo, Criar(escopo: escopo).Escopo);

    [Fact]
    public void Criar_rejects_undefined_enums()
    {
        Assert.Throws<ArgumentException>(() => Criar(sexo: (SexoAnimal)0));
        Assert.Throws<ArgumentException>(() => Criar(escopo: (EscopoAnimal)3));
    }

    [Fact]
    public void Criar_rejects_empty_required_or_supplied_optional_ids()
    {
        Assert.Throws<ArgumentException>(() => Criar(especieId: Guid.Empty));
        Assert.Throws<ArgumentException>(() => Criar(racaId: Guid.Empty));
        Assert.Throws<ArgumentException>(() => Criar(variedadeId: Guid.Empty));
    }

    [Fact]
    public void Criar_normalizes_codigo_and_nome_and_preserves_optional_null_name()
    {
        var normalized = Criar(codigoInterno: "  AN-  001 ", nome: "  Bela\t do\n campo ");
        var blankName = Criar(nome: " \t ");

        Assert.Equal("AN- 001", normalized.CodigoInterno);
        Assert.Equal("Bela do campo", normalized.Nome);
        Assert.Null(blankName.Nome);
    }

    [Fact]
    public void Criar_validates_codigo_and_nome_boundaries()
    {
        Assert.Equal("C", Criar(codigoInterno: "C").CodigoInterno);
        Assert.Equal(new string('C', 64), Criar(codigoInterno: new string('C', 64)).CodigoInterno);
        Assert.Equal(new string('N', 200), Criar(nome: new string('N', 200)).Nome);
        Assert.ThrowsAny<ArgumentException>(() => Criar(codigoInterno: " "));
        Assert.Throws<ArgumentException>(() => Criar(codigoInterno: new string('C', 65)));
        Assert.Throws<ArgumentException>(() => Criar(nome: new string('N', 201)));
    }

    [Fact]
    public void Criar_rejects_birth_date_after_injected_utc_today()
    {
        Assert.Equal(Today, Criar(dataNascimento: Today).DataNascimento);
        Assert.Throws<ArgumentException>(() => Criar(dataNascimento: Today.AddDays(1)));
    }

    [Fact]
    public void AlterarCadastro_updates_all_editable_registration_fields()
    {
        var animal = Criar();
        var changedAt = CreatedAt.AddMinutes(1);
        var novaEspecieId = Guid.NewGuid();

        animal.AlterarCadastro(
            " AN-002 ", " Lua ", novaEspecieId, RacaId, VariedadeId,
            SexoAnimal.Femea, Today, EscopoAnimal.Referencia, Today, changedAt);

        Assert.Equal("AN-002", animal.CodigoInterno);
        Assert.Equal("Lua", animal.Nome);
        Assert.Equal(novaEspecieId, animal.EspecieId);
        Assert.Equal(RacaId, animal.RacaId);
        Assert.Equal(VariedadeId, animal.VariedadeId);
        Assert.Equal(SexoAnimal.Femea, animal.Sexo);
        Assert.Equal(Today, animal.DataNascimento);
        Assert.Equal(EscopoAnimal.Referencia, animal.Escopo);
        Assert.Equal(changedAt, animal.UpdatedAtUtc);
    }

    [Fact]
    public void AlterarCadastro_keeps_inactive_animal_editable_and_preserves_timestamp_when_snapshot_is_unchanged()
    {
        var animal = Criar();
        var inactiveAt = CreatedAt.AddMinutes(1);
        animal.Inativar(inactiveAt);

        animal.AlterarCadastro(" AN-000001 ", " ", EspecieId, null, null,
            SexoAnimal.Indeterminado, null, EscopoAnimal.Operacional, Today, inactiveAt.AddMinutes(1));

        Assert.False(animal.Ativo);
        Assert.Equal(inactiveAt, animal.UpdatedAtUtc);
    }

    [Fact]
    public void Lifecycle_is_idempotent()
    {
        var animal = Criar();
        var inactiveAt = CreatedAt.AddMinutes(1);
        animal.Inativar(inactiveAt);
        animal.Inativar(inactiveAt.AddMinutes(1));
        Assert.Equal(inactiveAt, animal.UpdatedAtUtc);

        var reactivatedAt = inactiveAt.AddMinutes(2);
        animal.Reativar(reactivatedAt);
        animal.Reativar(reactivatedAt.AddMinutes(1));
        Assert.True(animal.Ativo);
        Assert.Equal(reactivatedAt, animal.UpdatedAtUtc);
    }

    [Fact]
    public void Aggregate_does_not_anticipate_age_or_classification_navigations()
    {
        var properties = typeof(Animal).GetProperties();

        Assert.DoesNotContain(properties, property => property.Name is "Idade" or "Especie" or "Raca" or "Variedade");
    }

    private static Animal Criar(
        string codigoInterno = "AN-000001", string? nome = null, Guid? especieId = null,
        Guid? racaId = null, Guid? variedadeId = null, SexoAnimal sexo = SexoAnimal.Indeterminado,
        DateOnly? dataNascimento = null, EscopoAnimal escopo = EscopoAnimal.Operacional)
        => Animal.Criar(codigoInterno, nome, especieId ?? EspecieId, racaId, variedadeId,
            sexo, dataNascimento, escopo, Today, CreatedAt);
}
