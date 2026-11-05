using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;

namespace GenSW.Domain.MaterialCosts;

public sealed record CanonicalCostQuantity(decimal Quantidade, string Unidade);
public sealed record CostFragmentSplit(decimal QuantidadeRetirada, decimal ValorRetirado, decimal QuantidadeRestante, decimal ValorRestante);
public sealed record CostShare(Guid Id, decimal Percentual);
public sealed record CostShareResult(Guid Id, decimal Percentual, decimal Valor, int Ordem, string Resto);
public sealed record CostUnitPrice(decimal Valor, bool Arredondado, string ResiduoNumerador, string ResiduoDenominador);
public sealed record CostComponent(Guid Id, Guid CamadaId, Guid ItemId, string Unidade, decimal Quantidade,
    decimal? ValorConhecido, IReadOnlyList<string> Lacunas, Guid? EvidenciaId = null,
    string FracaoNumerador = "1", string FracaoDenominador = "1");
public sealed record CostDestination(Guid Id, string Tipo, decimal Quantidade, string Unidade, decimal Percentual);
public sealed record AllocatedCostDestination(Guid Id, string Tipo, decimal Percentual, decimal ValorConhecido,
    decimal? ValorTotal, string Cobertura, CostUnitPrice? UnitarioConhecido, CostUnitPrice? UnitarioCompleto,
    IReadOnlyList<CostComponent> Componentes, IReadOnlyList<string> Lacunas, IReadOnlyList<CostShareResult> Rateios);
public sealed record CostAllocationResult(decimal ValorConhecido, decimal? ValorTotal, string Cobertura,
    IReadOnlyList<AllocatedCostDestination> Destinos);

public static partial class MaterialCostRules
{
    public const string Algorithm = "gensw-material-costs/1.0";
    public const decimal MaximumAmount = 9999999999999999.99m;
    public const decimal MaximumQuantity = 99999999999999.999999m;
    private static readonly BigInteger PercentDenominator = 100000000;
    [GeneratedRegex(@"^(0|[1-9][0-9]*)(\.[0-9]+)?$", RegexOptions.CultureInvariant)]
    private static partial Regex NumberPattern();
    public static void Id(Guid id) { if (id == Guid.Empty) throw new ArgumentException("Identificador monetário obrigatório."); }
    public static string Currency(string currency) => currency == "BRL" ? currency : throw new ArgumentException("Somente BRL é permitido; não há câmbio implícito.");
    public static string CanonicalUnit(string unit) => unit is "kg" or "L" or "un" ? unit : throw new ArgumentException("Unidade canônica deve ser kg, L ou un.");
    private static decimal Parse(string? text, int scale, decimal maximum, string label)
    {
        if (text is null || text.Length > 40 || !NumberPattern().IsMatch(text) || text.Contains('.') && text.Length - text.IndexOf('.') - 1 > scale ||
            !decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value) || value > maximum)
            throw new ArgumentException($"{label}: informe string decimal invariante, até {scale} casas e dentro do limite.");
        return value;
    }
    public static decimal Amount(string text, string currency = "BRL") { Currency(currency); return Parse(text, 2, MaximumAmount, "Total BRL"); }
    public static decimal ReferenceUnitPrice(string text) => Parse(text, 6, MaximumQuantity, "Valor unitário");
    public static decimal Percent(string text) => Parse(text, 6, 100m, "Percentual");
    public static CanonicalCostQuantity Quantity(string text, string unit)
    {
        var quantity = Parse(text, 6, MaximumQuantity, "Quantidade");
        var canonical = unit switch { "g" => "kg", "mL" => "L", _ => CanonicalUnit(unit) };
        if (unit is "g" or "mL") quantity /= 1000m;
        ScaledQuantity(quantity, canonical);
        return new(quantity, canonical);
    }
    public static BigInteger ScaledQuantity(decimal value, string unit)
    {
        CanonicalUnit(unit);
        if (value < 0m || value > MaximumQuantity || decimal.Truncate(value * 1000000m) != value * 1000000m || unit == "un" && decimal.Truncate(value) != value)
            throw new ArgumentException("Quantidade fora da precisão canônica; un exige inteiro.");
        return new BigInteger(value * 1000000m);
    }
    public static BigInteger Cents(decimal value)
    {
        if (value < 0m || value > MaximumAmount || decimal.Truncate(value * 100m) != value * 100m) throw new ArgumentException("Total deve conservar centavos BRL dentro do limite.");
        return new BigInteger(value * 100m);
    }
    public static decimal FromCents(BigInteger cents)
    {
        if (cents < 0 || cents > new BigInteger(MaximumAmount * 100m)) throw new ArgumentException("Total BRL excede o limite.");
        return (decimal)cents / 100m;
    }
    public static string CoverageState(string state) => state is "Completo" or "Parcial" or "Desconhecido" ? state : throw new ArgumentException("Estado de cobertura inválido.");
    public static string Coverage(IReadOnlyList<CostComponent> components)
    {
        if (components.Count == 0 || components.All(x => x.ValorConhecido is null)) return "Desconhecido";
        return components.All(x => x.ValorConhecido is not null && x.Lacunas.Count == 0) ? "Completo" : "Parcial";
    }
    public static CostComponent Fraction(CostComponent component, BigInteger numerator, BigInteger denominator, decimal? amount)
    {
        if (numerator < 0 || denominator <= 0 || numerator > denominator ||
            !BigInteger.TryParse(component.FracaoNumerador, NumberStyles.None, CultureInfo.InvariantCulture, out var previousNumerator) ||
            !BigInteger.TryParse(component.FracaoDenominador, NumberStyles.None, CultureInfo.InvariantCulture, out var previousDenominator) ||
            previousNumerator < 0 || previousDenominator <= 0 || previousNumerator > previousDenominator)
            throw new ArgumentException("Fração de cobertura inválida.");
        var n = previousNumerator * numerator; var d = previousDenominator * denominator;
        var gcd = BigInteger.GreatestCommonDivisor(n, d);
        return component with { ValorConhecido = amount, FracaoNumerador = (n / gcd).ToString(CultureInfo.InvariantCulture), FracaoDenominador = (d / gcd).ToString(CultureInfo.InvariantCulture) };
    }
    public static CostFragmentSplit Split(decimal quantity, decimal knownAmount, decimal take)
    {
        var q = ScaledQuantity(quantity, "kg"); var t = ScaledQuantity(take, "kg"); var cents = Cents(knownAmount);
        if (q <= 0 || t < 0 || t > q) throw new ArgumentException("Fração deve pertencer à quantidade existente.");
        var allocated = t == q ? cents : cents * t / q;
        return new(take, FromCents(allocated), quantity - take, FromCents(cents - allocated));
    }
    public static IReadOnlyList<CostShareResult> Distribute(decimal knownAmount, IReadOnlyList<CostShare> shares)
    {
        ArgumentNullException.ThrowIfNull(shares);
        if (shares.Count == 0 || shares.Count > 1000 || shares.Any(x => x.Id == Guid.Empty) || shares.Select(x => x.Id).Distinct().Count() != shares.Count)
            throw new ArgumentException("Destinos devem ser identificados, únicos e limitados.");
        var scaled = shares.Select(x => (Share: x, Percent: new BigInteger(Percent(x.Percentual.ToString(CultureInfo.InvariantCulture)) * 1000000m))).ToArray();
        if (scaled.Aggregate(BigInteger.Zero, (sum, x) => sum + x.Percent) != PercentDenominator) throw new ArgumentException("Percentuais devem somar exatamente 100%.");
        var total = Cents(knownAmount);
        var values = scaled.Select(x => (x.Share, Floor: BigInteger.DivRem(total * x.Percent, PercentDenominator, out _), Remainder: total * x.Percent % PercentDenominator))
            .OrderByDescending(x => x.Remainder).ThenBy(x => x.Share.Id.ToString("D"), StringComparer.Ordinal).ToArray();
        var remaining = total - values.Aggregate(BigInteger.Zero, (sum, x) => sum + x.Floor);
        return values.Select((x, index) => new CostShareResult(x.Share.Id, x.Share.Percentual,
            FromCents(x.Floor + (index < remaining ? 1 : 0)), index + 1, x.Remainder.ToString(CultureInfo.InvariantCulture))).ToArray();
    }
    public static CostUnitPrice UnitCost(decimal total, decimal positiveQuantity)
    {
        var cents = Cents(total); var quantity = ScaledQuantity(positiveQuantity, "kg");
        if (quantity <= 0) throw new ArgumentException("Custo unitário exige saída positiva.");
        // (cents / 100) / (scaledQuantity / 1e6), expressed in millionths.
        var numerator = cents * 10000000000; var quotient = BigInteger.DivRem(numerator, quantity, out var remainder);
        var twice = remainder * 2;
        if (twice > quantity || twice == quantity && !quotient.IsEven) quotient++;
        if (quotient > new BigInteger(MaximumQuantity * 1000000m)) throw new ArgumentException("Custo unitário excede numeric(20,6).");
        return new((decimal)quotient / 1000000m, remainder != 0, (numerator - quotient * quantity).ToString(CultureInfo.InvariantCulture), quantity.ToString(CultureInfo.InvariantCulture));
    }
    public static CostAllocationResult Allocate(IReadOnlyList<CostComponent> components, IReadOnlyList<CostDestination> destinations)
    {
        ArgumentNullException.ThrowIfNull(components); ArgumentNullException.ThrowIfNull(destinations);
        if (components.Select(x => x.Id).Distinct().Count() != components.Count) throw new ArgumentException("Componentes duplicados.");
        foreach (var component in components) { Id(component.Id); Id(component.CamadaId); Id(component.ItemId); ScaledQuantity(component.Quantidade, component.Unidade); if (component.ValorConhecido is { } value) Cents(value); }
        foreach (var destination in destinations)
        {
            if (destination.Tipo is not ("Saida" or "Perda")) throw new ArgumentException("Destino deve ser saída ou perda.");
            ScaledQuantity(destination.Quantidade, destination.Unidade);
            if (destination.Tipo == "Saida" && destination.Quantidade == 0 && destination.Percentual != 0) throw new ArgumentException("Saída zero exige participação zero.");
        }
        var shares = destinations.Select(x => new CostShare(x.Id, x.Percentual)).ToArray();
        _ = Distribute(0m, shares);
        var breakdown = components.Where(x => x.ValorConhecido.HasValue).ToDictionary(x => x.Id, x => Distribute(x.ValorConhecido!.Value, shares));
        var known = FromCents(components.Aggregate(BigInteger.Zero, (sum, x) => sum + (x.ValorConhecido is { } value ? Cents(value) : 0)));
        var coverage = Coverage(components);
        var results = destinations.Select(destination =>
        {
            var inherited = destination.Percentual == 0 ? Array.Empty<CostComponent>() : components.Select(component => Fraction(component,
                new BigInteger(destination.Percentual * 1000000m), PercentDenominator,
                component.ValorConhecido is null ? null : breakdown[component.Id].Single(x => x.Id == destination.Id).Valor)).ToArray();
            var portion = FromCents(inherited.Aggregate(BigInteger.Zero, (sum, x) => sum + (x.ValorConhecido is { } value ? Cents(value) : 0)));
            var zeroOutput = destination.Tipo == "Saida" && destination.Quantidade == 0;
            var state = zeroOutput ? "NaoAplicavel" : destination.Percentual == 0 ? "Completo" : Coverage(inherited);
            var gaps = inherited.SelectMany(x => x.Lacunas).Distinct(StringComparer.Ordinal).ToArray();
            CostUnitPrice? unit = destination.Tipo == "Saida" && destination.Quantidade > 0 ? UnitCost(portion, destination.Quantidade) : null;
            return new AllocatedCostDestination(destination.Id, destination.Tipo, destination.Percentual, portion, state == "Completo" ? portion : null,
                state, unit, state == "Completo" ? unit : null, inherited, gaps,
                breakdown.Values.SelectMany(x => x.Where(y => y.Id == destination.Id)).ToArray());
        }).ToArray();
        return new(known, coverage == "Completo" ? known : null, coverage, results);
    }
}
