using GenSW.Domain.Catalog;
using GenSW.Domain.Inventory;
using Xunit;

namespace GenSW.Domain.Tests;

public sealed class InventoryTests
{
    private static readonly Guid ItemId = Guid.NewGuid();
    private static readonly Guid Actor = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);
    private static ConversaoItem Conversion(string from, string to, string factor) => ConversaoItem.Create(ItemId, 1,
        new(from, to, factor, "Fixture", "Medição sintética", new(2026, 1, 1), "Contexto de teste", "Amostra", "Medido"), Actor, Now);
    [Theory]
    [InlineData("1", "g", "kg", "0.001")]
    [InlineData("1250", "mL", "L", "1.25")]
    [InlineData("2", "un", "un", "2")]
    public void Exact_units_preserve_canonical_quantity(string q, string unit, string canonical, string expected)
    {
        var result = InventoryRules.Normalize(q, unit, canonical, null, ItemId);
        Assert.Equal(expected, result.Normalizada); Assert.Equal("0", result.Residuo); Assert.False(result.ExigeAceite);
        InventoryRules.Accept(result, null);
    }
    [Fact]
    public void Half_micro_to_even_requires_current_explicit_acceptance()
    {
        var q = InventoryRules.Normalize("0.0015", "g", "kg", null, ItemId);
        Assert.Equal("0.0000015", q.Calculada); Assert.Equal("0.000002", q.Normalizada); Assert.Equal("-0.0000005", q.Residuo);
        Assert.Throws<ArgumentException>(() => InventoryRules.Accept(q, null));
        Assert.Throws<ArgumentException>(() => InventoryRules.Accept(q, new(q.Calculada, "0.000001", q.Residuo, "Teste")));
        InventoryRules.Accept(q, new(q.Calculada, q.Normalizada, q.Residuo, "Resíduo conferido"));
        InventoryRules.Accept(q, new("0.0000015000", "0.0000020000", "-0.0000005000", "Mesmo resíduo conferido"));
    }
    [Fact]
    public void Small_positive_quantity_cannot_create_a_zero_movement() => Assert.Throws<ArgumentException>(() => InventoryRules.Normalize("0.0005", "g", "kg", null, ItemId));
    [Fact]
    public void Excess_derived_fraction_is_not_silently_rounded_by_decimal_parser()
    { Assert.Throws<ArgumentException>(() => InventoryRules.PreciseDecimal("1.00000000000000000000000000001")); Assert.Equal(1m, InventoryRules.PreciseDecimal("1.00000000000000000000000000000")); }
    [Fact]
    public void Inverse_divides_original_factor_and_preserves_exact_count()
    {
        var result = InventoryRules.Normalize("3", "kg", "un", Conversion("un", "kg", "3"), ItemId);
        Assert.Equal("1", result.Normalizada); Assert.Equal("Inverso", result.Sentido); Assert.False(result.ExigeAceite);
    }
    [Theory]
    [InlineData("2", "un", "L", "un", "mL", "250", "0.5")]
    [InlineData("1000", "mL", "kg", "L", "g", "800", "0.8")]
    [InlineData("400", "g", "L", "L", "kg", "0.8", "0.5")]
    public void Contextual_pairs_normalize_both_endpoints(string q, string from, string canonical, string cFrom, string cTo, string factor, string expected)
    { Assert.Equal(expected, InventoryRules.Normalize(q, from, canonical, Conversion(cFrom, cTo, factor), ItemId).Normalizada); }
    [Fact]
    public void Fractional_count_and_unrelated_conversion_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => InventoryRules.Normalize("1", "kg", "un", Conversion("un", "kg", "3"), ItemId));
        Assert.Throws<ArgumentException>(() => InventoryRules.Normalize("2", "L", "un", Conversion("L", "kg", "3"), ItemId));
        Assert.Throws<ArgumentException>(() => InventoryRules.Normalize("2", "L", "kg", Conversion("L", "kg", "3"), Guid.NewGuid()));
    }
    [Theory]
    [InlineData("1.0000001")]
    [InlineData("-1")]
    [InlineData("1e2")]
    [InlineData("1,1")]
    [InlineData("100000000000000")]
    public void Input_contract_rejects_excess_precision_and_format(string value) => Assert.Throws<ArgumentException>(() => InventoryRules.Normalize(value, "kg", "kg", null, ItemId));
    [Fact]
    public void Converted_quantity_over_limit_is_rejected() => Assert.Throws<ArgumentException>(() => InventoryRules.Normalize("99999999999999", "L", "kg", Conversion("L", "kg", "2"), ItemId));
    [Fact]
    public void Balance_never_accepts_negative_fractional_count_or_obsolete_revision()
    {
        var p = PosicaoEstoque.Create(Guid.NewGuid(), Guid.NewGuid(), "un"); p.SetBalance(5m, 0);
        Assert.Throws<InventoryConflictException>(() => p.SetBalance(-1m, 1));
        Assert.Throws<ArgumentException>(() => p.SetBalance(1.5m, 1));
        Assert.Throws<InventoryConflictException>(() => p.SetBalance(4m, 0));
        Assert.Equal(5m, p.Quantidade); Assert.Equal(1, p.Revisao); p.SetBalance(0m, 1); Assert.Equal(2, p.Revisao);
    }
    private static LoteMaterial Lot(DateOnly? expiry = null) => LoteMaterial.Create(ItemId, "LOTE", "kg",
        new("", "Fixture", "Fonte", Actor, new(2026, 1, 1), null, null), expiry, expiry is null ? null : "Data declarada", expiry is null ? null : Actor,
        null, null, null, false, Now, new(2026, 10, 7));
    [Fact]
    public void Expiry_is_inclusive_and_unknown_does_not_mean_expired()
    {
        Assert.False(InventoryRules.Expired(new(2026, 10, 7), new(2026, 10, 7)));
        Assert.True(InventoryRules.Expired(new(2026, 10, 7), new(2026, 10, 8)));
        Assert.False(InventoryRules.Expired(null, new(2026, 10, 8))); Assert.Equal("Bloqueado", Lot(new(2026, 10, 6)).Situacao);
    }
    [Fact]
    public void Block_release_and_terminal_closure_keep_identity()
    {
        var lot = Lot(); var id = lot.Id; lot.SetState("Bloqueado", 1, Now, new(2026, 10, 7)); lot.SetActive(false, 2, Now);
        Assert.Equal("Bloqueado", lot.Situacao); lot.SetActive(true, 3, Now); Assert.Equal("Bloqueado", lot.Situacao);
        lot.SetState("Liberado", 4, Now, new(2026, 10, 7)); lot.SetState("Encerrado", 5, Now, new(2026, 10, 7));
        Assert.Throws<InventoryConflictException>(() => lot.SetState("Liberado", 6, Now, new(2026, 10, 7))); Assert.Equal(id, lot.Id);
    }
    private sealed class Clock(DateTimeOffset time) : TimeProvider { public override DateTimeOffset GetUtcNow() => time; }
    [Fact]
    public void Operational_day_changes_at_sao_paulo_midnight()
    {
        Assert.Equal(new DateOnly(2026, 10, 7), InventoryRules.Today(new Clock(new(2026, 10, 8, 2, 59, 59, TimeSpan.Zero))));
        Assert.Equal(new DateOnly(2026, 10, 8), InventoryRules.Today(new Clock(new(2026, 10, 8, 3, 0, 0, TimeSpan.Zero))));
    }
}
