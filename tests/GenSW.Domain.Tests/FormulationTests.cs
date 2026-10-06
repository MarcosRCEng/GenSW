using GenSW.Domain.Catalog;
using GenSW.Domain.Formulation;
using Xunit;

namespace GenSW.Domain.Tests;

public sealed class FormulationTests
{
    public static ProfileData Profile(string name, string pb = "100", string fiber = "20", string energy = "12", string moisture = "100") =>
        new(name, "Fixture sintética, não operacional", "Método sintético", "Material da fixture", name, "Exemplo sintético", null, null, null, null,
            [Value("PB", pb), Value("FB", fiber), Value("EM", energy, "MJ/kg", "Contexto sintético"), Value("UMIDADE", moisture)]);
    private static NutritionValue Value(string code, string? value, string unit = "g/kg", string context = "") =>
        new(code, value is null ? "Desconhecido" : "Conhecido", value, value is null ? null : "Declarado", "BN", unit, "Sintetico", context, Motivo: value is null ? "Dado ausente na fixture" : null);
    public static ResolvedIngredient Input(string name, string mass, ProfileData? profile) => new(Guid.NewGuid(), Guid.NewGuid(), name, "Alimentar", mass, "kg", mass, Guid.NewGuid(), 1, profile, null, null, null, null);
    private static NutritionGoal Goal(string code, string? min = null, string? max = null, string basis = "BN", bool estimates = true) =>
        new(code, "Sintetico", code == "EM" ? "Contexto sintético" : "", basis, code == "EM" ? "MJ/kg" : "g/kg", min, max, "InformadaUsuario", null, estimates);

    [Theory]
    [InlineData("60", "40", "220", "44", "10.4", "86")]
    [InlineData("30", "70", "310", "62", "9.2", "83")]
    public void F1_F2_reproduce_weighted_BN_MS_energy_and_goals(string a, string b, string protein, string fiber, string energy, string dry)
    {
        var result = NutritionEngine.Calculate([Input("A", a, Profile("A")), Input("B", b, Profile("B", "400", "80", "8", "200"))], [Goal("PB", "250"), Goal("FB", max: "50"), Goal("EM", "10")]);
        Assert.Equal("100", result.MassaKg); Assert.Equal(dry, result.MateriaSecaKg);
        Assert.Equal(protein, result.Componentes.Single(x => x.Componente == "PB").ValorBN);
        Assert.Equal(fiber, result.Componentes.Single(x => x.Componente == "FB").ValorBN);
        var e = result.Componentes.Single(x => x.Componente == "EM"); Assert.Equal(energy, e.ValorBN);
        var energyDecimal = decimal.Parse(energy, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(energyDecimal / .004184m, decimal.Parse(e.KcalBN!, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Contains(result.Metas, m => m.Estado == "NaoAtendida"); Assert.All(result.Componentes, x => Assert.Equal("100", x.CoberturaPercentual));
    }
    [Fact]
    public void Missing_protein_has_60_percent_coverage_and_never_passes_goal()
    {
        var b = Profile("B", "400") with { Valores = [Value("PB", null), Value("UMIDADE", "200")] };
        var result = NutritionEngine.Calculate([Input("A", "60", Profile("A")), Input("B", "40", b)], [Goal("PB", "50")]);
        var pb = result.Componentes.Single(x => x.Componente == "PB");
        Assert.Null(pb.ValorBN); Assert.Null(pb.ValorMS); Assert.Equal("60", pb.ContribuicaoConhecidaBN); Assert.Equal("60", pb.CoberturaPercentual);
        Assert.Equal("6000", pb.Contribuicoes[0].QuantidadeComponente); Assert.Null(pb.Contribuicoes[1].QuantidadeComponente);
        Assert.Equal("Indeterminada", result.Metas[0].Estado);
    }
    [Theory]
    [InlineData("Desconhecido")]
    [InlineData("NaoAplicavel")]
    public void Unknown_and_NA_do_not_become_zero(string state)
    {
        var data = Profile("A") with { Valores = [Value("PB", null) with { Estado = state }] };
        data.Validate(true);
        var result = NutritionEngine.Calculate([Input("A", "10", data)], [Goal("PB", max: "1000")]);
        Assert.Null(result.Componentes[0].ValorBN); Assert.Equal("Indeterminada", result.Metas[0].Estado);
        Assert.Throws<ArgumentException>(() => (data with { Valores = [data.Valores[0] with { Valor = "0" }] }).Validate(true));
    }
    [Fact]
    public void Explicit_zero_is_complete_and_measured_origin_is_preserved()
    {
        var result = NutritionEngine.Calculate([Input("A", "10", Profile("A") with { Valores = [Value("PB", "0") with { Origem = "Medido" }] })], [Goal("PB", max: "0")]);
        Assert.Equal("0", result.Componentes[0].ValorBN); Assert.Equal("Completo", result.Componentes[0].Estado); Assert.Equal("AtendidaNosDadosDisponiveis", result.Metas[0].Estado);
    }
    [Fact]
    public void Missing_moisture_blocks_MS_but_preserves_BN()
    {
        var p = Profile("A") with { Valores = [Value("PB", "100"), Value("UMIDADE", null)] };
        var r = NutritionEngine.Calculate([Input("A", "20", p)], [Goal("PB", "50", basis: "MS")]);
        Assert.Equal("100", r.Componentes.Single(x => x.Componente == "PB").ValorBN); Assert.Null(r.MateriaSecaKg); Assert.Equal("Indeterminada", r.Metas[0].Estado);
    }
    [Fact]
    public void MS_values_are_weighted_by_dry_mass_and_not_BN_mass()
    {
        var p = Profile("A") with { Valores = [Value("PB", "200") with { Base = "MS" }, Value("UMIDADE", "100")] };
        var r = NutritionEngine.Calculate([Input("A", "10", p)], []);
        var pb = r.Componentes.Single(x => x.Componente == "PB"); Assert.Equal("180", pb.ValorBN); Assert.Equal("200", pb.ValorMS);
    }
    [Fact]
    public void Complete_estimates_can_be_excluded_by_goal_policy()
    {
        var p = Profile("A") with { Valores = [Value("PB", "100") with { Origem = "Estimado", Hipotese = "Fixture" }] };
        var r = NutritionEngine.Calculate([Input("A", "10", p)], [Goal("PB", "50", estimates: false)]);
        Assert.Equal("Completo", r.Componentes[0].Estado); Assert.True(r.Componentes[0].Estimado); Assert.Equal("Indeterminada", r.Metas[0].Estado);
    }
    [Theory]
    [InlineData("Minimo")]
    [InlineData("Maximo")]
    [InlineData("Faixa")]
    [InlineData("AbaixoDeteccao")]
    public void Bounded_source_is_not_used_as_scalar(string qualifier)
    {
        var p = Profile("A") with { Valores = [Value("PB", "100") with { Qualificador = qualifier }] };
        Assert.Null(NutritionEngine.Calculate([Input("A", "10", p)], []).Componentes[0].ValorBN);
    }
    [Theory]
    [InlineData("BN")]
    [InlineData("MS")]
    public void Estimated_dry_matter_propagates_to_the_basis_that_uses_it(string nutrientBasis)
    {
        var p = Profile("A") with { Valores = [Value("PB", "100") with { Base = nutrientBasis }, Value("UMIDADE", "100") with { Origem = "Estimado", Hipotese = "Fixture" }] };
        var r = NutritionEngine.Calculate([Input("A", "10", p)], [Goal("PB", "50", basis: "MS", estimates: false), Goal("PB", "50", estimates: false)]);
        var pb = r.Componentes.Single(x => x.Componente == "PB");
        Assert.True(pb.EstimadoMS); Assert.Equal(nutrientBasis == "MS", pb.Estimado);
        Assert.Equal("Indeterminada", r.Metas[0].Estado);
        Assert.Equal(nutrientBasis == "MS" ? "Indeterminada" : "AtendidaNosDadosDisponiveis", r.Metas[1].Estado);
    }
    [Fact]
    public void Energy_modalities_and_contexts_do_not_mix()
    {
        var a = Profile("A"); var b = Profile("B") with { Valores = [Value("EB", "12", "MJ/kg", "Contexto sintético")] };
        var r = NutritionEngine.Calculate([Input("A", "60", a), Input("B", "40", b)], [Goal("EM", "1")]);
        Assert.Null(r.Componentes.Single(x => x.Componente == "EM").ValorBN); Assert.Equal("Indeterminada", r.Metas[0].Estado);
        var changedContext = Profile("B") with { Valores = [Value("EM", "12", "MJ/kg", "Outro contexto")] };
        Assert.Null(NutritionEngine.Calculate([Input("A", "60", a), Input("B", "40", changedContext)], []).Componentes.First(x => x.Componente == "EM").ValorBN);
    }
    [Fact]
    public void Missing_mass_keeps_contributions_but_coverage_indeterminate_and_packaging_excluded()
    {
        var inputs = new[] { Input("A", "10", Profile("A")), Input("B", "10", Profile("B")) with { MassaKg = null, Unidade = "L" }, Input("Frasco", "2", null) with { Papel = "Embalagem", Unidade = "un", MassaKg = null } };
        var result = NutritionEngine.Calculate(inputs, []); Assert.Null(result.MassaKg); Assert.Null(result.Componentes[0].CoberturaPercentual); Assert.Equal(2, result.Componentes[0].Contribuicoes.Count);
    }
    [Fact]
    public void Min_above_max_rejected_and_convex_impossibility_is_local()
    {
        Assert.Throws<ArgumentException>(() => NutritionEngine.ValidateGoals([Goal("PB", "100", "50")]));
        var r = NutritionEngine.Calculate([Input("A", "60", Profile("A")), Input("B", "40", Profile("B", "400"))], [Goal("PB", "450")]);
        Assert.True(r.Metas[0].ImpossibilidadeLocal); Assert.Equal("NaoAtendida", r.Metas[0].Estado);
    }
    [Theory]
    [InlineData("1.0000001")]
    [InlineData("-1")]
    [InlineData("1e2")]
    [InlineData("1,5")]
    [InlineData("100000000000000")]
    public void Decimal_precision_is_validated(string value) => Assert.Throws<ArgumentException>(() => CatalogRules.Decimal(value));
    [Fact]
    public void Count_is_integral_and_conversion_is_explicit()
    {
        Assert.Throws<ArgumentException>(() => CatalogRules.Quantity("0.5", "un"));
        var conversion = ConversaoItem.Create(Guid.NewGuid(), 1, new("L", "kg", "0.8", "Fixture", "Medição", new(2026, 1, 1), "Condição", "Amostra", "Medido"), Guid.NewGuid(), DateTimeOffset.UtcNow);
        Assert.Equal(8m, conversion.ToKg(10m, "L")); Assert.Null(conversion.ToKg(10m, "un"));
    }
    [Fact]
    public void Published_profile_and_unit_are_immutable_and_unknown_is_not_measured()
    {
        var p = NutritionProfile.Create(Guid.NewGuid(), 1, Profile("A"), Guid.NewGuid(), DateTimeOffset.UtcNow); p.Publish(1, DateTimeOffset.UtcNow);
        Assert.Throws<CatalogConflictException>(() => p.Update(Profile("B"), 2));
        var d = new ItemData("A", "A", null, null, "Alimentar", "kg", true, true, true, false); var item = Item.Create(d, DateTimeOffset.UtcNow); item.FixUnit();
        Assert.Throws<CatalogConflictException>(() => item.Update(d with { Unidade = "L" }, 1, DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(() => (Profile("A") with { Valores = [Value("PB", "1") with { Origem = null }] }).Validate(true));
    }
    [Fact]
    public void Moisture_dry_matter_and_mass_composition_are_bounded()
    {
        Assert.Throws<ArgumentException>(() => (Profile("A") with { Valores = [Value("UMIDADE", "100"), Value("MS", "800")] }).Validate(true));
        Assert.Throws<ArgumentException>(() => (Profile("A") with { Valores = [Value("PB", "1001")] }).Validate(true));
    }
    [Fact]
    public void Scaling_preserves_fixed_lines_rejects_fractional_count_and_percent_sum()
    {
        var input = new RecipeInput(Guid.NewGuid(), Guid.NewGuid(), "Alimentar", "100", "kg", "Variavel");
        var output = new RecipeOutput(Guid.NewGuid(), Guid.NewGuid(), "Resultado", "100", "kg", "Variavel", true);
        var d = new RecipeData("Processamento", "Quantidade", "100", "kg", [input, input with { Id = Guid.NewGuid(), Papel = "Consumivel", Quantidade = "1", Unidade = "un", Escala = "Fixa" }], [output], [], [], null, []);
        var scaled = d.Scale("200", "kg"); Assert.Equal("200", scaled.Entradas[0].Quantidade); Assert.Equal("1", scaled.Entradas[1].Quantidade);
        Assert.Throws<ArgumentException>(() => (d with { Entradas = [input with { Unidade = "un", Quantidade = "1" }] }).Scale("50", "kg"));
        Assert.Throws<ArgumentException>(() => (d with { Modo = "Percentual", Entradas = [input with { Quantidade = "97" }] }).Validate(true));
        Assert.Throws<ArgumentException>(() => d.Scale("10", "L"));
    }
    [Fact]
    public void Simple_mixture_cannot_hide_processing_losses()
    {
        var d = new RecipeData("MisturaSimples", "Quantidade", "100", "kg", [new(Guid.NewGuid(), Guid.NewGuid(), "Alimentar", "100", "kg", "Variavel")], [new(Guid.NewGuid(), Guid.NewGuid(), "Resultado", "90", "kg", "Variavel", true)], [new("10", "kg", "Secagem")], [], null, []);
        Assert.Throws<ArgumentException>(() => d.Validate(true));
    }
}
