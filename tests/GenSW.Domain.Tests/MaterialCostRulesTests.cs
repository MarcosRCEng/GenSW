using GenSW.Domain.MaterialCosts;
using Xunit;

namespace GenSW.Domain.Tests;

public sealed class MaterialCostRulesTests
{
    private static readonly Guid A = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid B = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid C = Guid.Parse("00000000-0000-0000-0000-000000000003");
    [Theory]
    [InlineData("", "BRL")]
    [InlineData("0,01", "BRL")]
    [InlineData("0.001", "BRL")]
    [InlineData("1.00", "USD")]
    [InlineData("10000000000000000.00", "BRL")]
    [InlineData("-1", "BRL")]
    [InlineData("1e2", "BRL")]
    public void Monetary_input_rejects_implicit_conversion(string value, string currency) => Assert.Throws<ArgumentException>(() => MaterialCostRules.Amount(value, currency));
    [Fact]
    public void Maximum_is_exact_and_intermediate_products_are_unbounded()
    {
        var total = MaterialCostRules.Amount("9999999999999999.99");
        var shares = MaterialCostRules.Distribute(total, [new(A, 33.333333m), new(B, 33.333333m), new(C, 33.333334m)]);
        Assert.Equal(total, shares.Sum(x => x.Valor));
        Assert.Equal(3333333399999999.99m, shares.Single(x => x.Id == C).Valor);
    }
    [Fact]
    public void Fraction_retains_residual_until_last_with_known_zero()
    {
        var first = MaterialCostRules.Split(3, .01m, 1);
        var second = MaterialCostRules.Split(first.QuantidadeRestante, first.ValorRestante, 1);
        var last = MaterialCostRules.Split(second.QuantidadeRestante, second.ValorRestante, 1);
        Assert.Equal(new[] { 0m, 0m, .01m }, new[] { first.ValorRetirado, second.ValorRetirado, last.ValorRetirado });
        Assert.Equal(0m, last.ValorRestante);
    }
    [Theory]
    [InlineData("0.01", "50", "50", "0", "0.01", "0", "0")]
    [InlineData("0.01", "33.333333", "33.333333", "33.333334", "0", "0", "0.01")]
    [InlineData("0.02", "50", "25", "25", "0.01", "0.01", "0")]
    [InlineData("10.01", "50", "30", "20", "5.01", "3", "2")]
    public void Accepted_oracles_are_exact_for_all_destination_permutations(string total, string a, string b, string c, string expectedA, string expectedB, string expectedC)
    {
        var shares = new[] { new CostShare(A, MaterialCostRules.Percent(a)), new CostShare(B, MaterialCostRules.Percent(b)), new CostShare(C, MaterialCostRules.Percent(c)) };
        var expectations = new Dictionary<Guid, decimal> { [A] = MaterialCostRules.Amount(expectedA), [B] = MaterialCostRules.Amount(expectedB), [C] = MaterialCostRules.Amount(expectedC) };
        foreach (var order in new[] { shares, shares.Reverse().ToArray(), new[] { shares[1], shares[2], shares[0] } })
        {
            var allocated = MaterialCostRules.Distribute(MaterialCostRules.Amount(total), order);
            Assert.Equal(MaterialCostRules.Amount(total), allocated.Sum(x => x.Valor));
            Assert.All(allocated, value => Assert.Equal(expectations[value.Id], value.Valor));
        }
    }
    [Fact]
    public void Allocation_is_by_component_and_inherits_unknown_coverage()
    {
        var item = Guid.NewGuid(); var layer = Guid.NewGuid();
        CostComponent[] components = [new(Guid.NewGuid(), layer, item, "kg", 1, .01m, []), new(Guid.NewGuid(), Guid.NewGuid(), item, "kg", 1, .01m, [])];
        CostDestination[] destinations = [new(A, "Saida", 3, "un", 50), new(B, "Saida", 3, "un", 50)];
        var result = MaterialCostRules.Allocate(components, destinations);
        Assert.Equal(.02m, result.Destinos.Single(x => x.Id == A).ValorTotal);
        Assert.Equal(0m, result.Destinos.Single(x => x.Id == B).ValorTotal);
        var partial = MaterialCostRules.Allocate([components[0] with { ValorConhecido = 4m }, new(Guid.NewGuid(), Guid.NewGuid(), item, "kg", 1, null, ["Documento ausente"])], destinations);
        Assert.Null(partial.ValorTotal); Assert.Equal("Parcial", partial.Cobertura);
        Assert.All(partial.Destinos, destination => { Assert.Equal(2m, destination.ValorConhecido); Assert.Null(destination.ValorTotal); Assert.Null(destination.UnitarioCompleto); Assert.Contains("Documento ausente", destination.Lacunas); });
    }
    [Fact]
    public void Unknown_is_distinct_from_documented_zero()
    {
        var id = Guid.NewGuid();
        var unknown = new CostComponent(id, id, id, "kg", 1, null, ["Sem documento"]);
        Assert.Equal("Desconhecido", MaterialCostRules.Coverage([unknown]));
        Assert.Equal("Completo", MaterialCostRules.Coverage([unknown with { ValorConhecido = 0, Lacunas = [] }]));
    }
    [Fact]
    public void Unit_price_rounding_does_not_reconstruct_total()
    {
        var unit = MaterialCostRules.UnitCost(.01m, 3);
        Assert.Equal(.003333m, unit.Valor); Assert.True(unit.Arredondado);
        Assert.NotEqual(.01m, unit.Valor * 3);
        Assert.Equal(2.505m, MaterialCostRules.UnitCost(5.01m, 2).Valor);
        Assert.Throws<ArgumentException>(() => MaterialCostRules.UnitCost(1, 0));
    }
    [Fact]
    public void Zero_output_cannot_absorb_percentage_or_have_unit_price()
    {
        var component = new CostComponent(A, A, A, "kg", 1, 4m, []);
        Assert.Throws<ArgumentException>(() => MaterialCostRules.Allocate([component], [new(A, "Saida", 0, "kg", 10), new(B, "Perda", 1, "kg", 90)]));
        var valid = MaterialCostRules.Allocate([component], [new(A, "Saida", 0, "kg", 0), new(B, "Perda", 1, "kg", 100)]);
        Assert.Null(valid.Destinos.Single(x => x.Id == A).UnitarioCompleto);
        Assert.Null(valid.Destinos.Single(x => x.Id == A).ValorTotal);
        Assert.Equal("NaoAplicavel", valid.Destinos.Single(x => x.Id == A).Cobertura);
        Assert.Null(valid.Destinos.Single(x => x.Id == B).UnitarioCompleto);
        Assert.Equal(4m, valid.Destinos.Single(x => x.Id == B).ValorTotal);
    }
    [Theory]
    [InlineData("99.999999")]
    [InlineData("100.000001")]
    [InlineData("-1")]
    [InlineData("50.0000001")]
    public void Invalid_percentage_never_normalizes_to_hundred(string percent) => Assert.Throws<ArgumentException>(() => MaterialCostRules.Distribute(1, [new(A, MaterialCostRules.Percent(percent))]));
    [Fact]
    public void Duplicate_destination_is_rejected() => Assert.Throws<ArgumentException>(() => MaterialCostRules.Distribute(1, [new(A, 50), new(A, 50)]));
    [Fact]
    public void Canonical_subunits_are_exact_and_count_is_integral()
    {
        Assert.Equal(new CanonicalCostQuantity(.123456m, "kg"), MaterialCostRules.Quantity("123.456", "g"));
        Assert.Equal(new CanonicalCostQuantity(.123456m, "L"), MaterialCostRules.Quantity("123.456", "mL"));
        Assert.Throws<ArgumentException>(() => MaterialCostRules.Quantity("1.1", "un"));
        Assert.Throws<ArgumentException>(() => MaterialCostRules.Quantity("0.000001", "g"));
    }
    [Fact]
    public void Cent_conservation_holds_across_fragmentation_and_permutations()
    {
        for (var cents = 0; cents < 251; cents++)
        {
            var remaining = cents / 100m; var withdrawals = 0m;
            for (var quantity = 7; quantity > 0; quantity--) { var split = MaterialCostRules.Split(quantity, remaining, 1); withdrawals += split.ValorRetirado; remaining = split.ValorRestante; }
            Assert.Equal(cents / 100m, withdrawals); Assert.Equal(0m, remaining);
            foreach (var a in new[] { 0m, 10m, 33.333333m, 50m, 100m })
            {
                var split = MaterialCostRules.Distribute(cents / 100m, [new(A, a), new(B, 100 - a)]);
                Assert.Equal(cents / 100m, split.Sum(x => x.Valor));
                Assert.All(split, value => Assert.True(value.Valor >= 0));
            }
        }
    }
}
