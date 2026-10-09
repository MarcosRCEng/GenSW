using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using GenSW.Domain.Catalog;

namespace GenSW.Domain.Inventory;

public sealed class InventoryConflictException(string code, string message) : Exception(message)
{ public string Code { get; } = code; }
public sealed record QuantizationAcceptance(string Calculado, string Normalizado, string Residuo, string Motivo);
public sealed record PhysicalQuantity(string Declarada, string UnidadeDeclarada, string Calculada,
    string Normalizada, string Residuo, string Unidade, string? Fator, string? Sentido,
    string? Proveniencia, bool ExigeAceite);
public static class InventoryRules
{
    public const string Algorithm = "gensw-inventory/1.0";
    public static DateOnly Today(TimeProvider clock) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")).DateTime);
    public static void Expected(int actual, int expected)
    { if (actual != expected) throw new InventoryConflictException("versao_desatualizada", "O registro mudou. Recarregue e confira antes de tentar novamente."); }
    public static void Id(Guid id) { if (id == Guid.Empty) throw new ArgumentException("Referência obrigatória."); }
    public static void Date(DateOnly? date, DateOnly today) { if (date > today) throw new ArgumentException("Data observada não pode ser futura."); }
    public static void Dates(DateOnly? origin, DateOnly? manufacture, DateOnly? collection, DateOnly? expiry, DateOnly today)
    {
        Date(origin, today); Date(manufacture, today); Date(collection, today);
        if (expiry is { } end && new[] { origin, manufacture, collection }.Any(x => x.HasValue && x.Value > end)) throw new ArgumentException("Validade não pode anteceder origem/fabricação/coleta declaradas.");
    }
    public static bool Expired(DateOnly? expiry, DateOnly today) => expiry < today;
    public static void Balance(decimal value, string unit)
    {
        if (value < 0) throw new InventoryConflictException("saldo_insuficiente", "Quantidade excede o saldo físico disponível.");
        if (value > CatalogRules.Maximum) throw new ArgumentException("Saldo excede o limite físico.");
        if (decimal.Round(value, 6, MidpointRounding.ToEven) != value || (unit == "un" && decimal.Truncate(value) != value)) throw new ArgumentException("Quantidade não respeita a resolução física.");
    }
    private static decimal UnitFactor(string unit) => unit is "g" or "mL" ? .001m : 1m;
    public static PhysicalQuantity Normalize(string quantity, string declaredUnit, string canonicalUnit, ConversaoItem? conversion, Guid itemId)
    {
        var value = CatalogRules.Quantity(quantity, declaredUnit);
        if (canonicalUnit is not ("kg" or "L" or "un")) throw new ArgumentException("Unidade canônica inválida.");
        decimal calculated; string? direction = null;
        if (CatalogRules.Dimension(declaredUnit) == CatalogRules.Dimension(canonicalUnit)) calculated = CatalogRules.Canonical(value, declaredUnit);
        else
        {
            if (conversion is null || conversion.ItemId != itemId) throw new ArgumentException("Escolha uma conversão documental aplicável do mesmo item.");
            if (CatalogRules.Dimension(declaredUnit) == CatalogRules.Dimension(conversion.Origem) && CatalogRules.Dimension(canonicalUnit) == CatalogRules.Dimension(conversion.Destino))
            {
                calculated = checked(CatalogRules.Canonical(value, declaredUnit) / UnitFactor(conversion.Origem) * conversion.Fator * UnitFactor(conversion.Destino)); direction = "Direto";
            }
            else if (CatalogRules.Dimension(declaredUnit) == CatalogRules.Dimension(conversion.Destino) && CatalogRules.Dimension(canonicalUnit) == CatalogRules.Dimension(conversion.Origem))
            {
                calculated = checked(CatalogRules.Canonical(value, declaredUnit) / UnitFactor(conversion.Destino) / conversion.Fator * UnitFactor(conversion.Origem)); direction = "Inverso";
            }
            else throw new ArgumentException("Par de conversão incompatível com a quantidade e a unidade do lote.");
        }
        if (calculated <= 0 || calculated > CatalogRules.Maximum) throw new ArgumentException("Quantidade canônica fora do limite físico.");
        if (canonicalUnit == "un" && decimal.Truncate(calculated) != calculated) throw new ArgumentException("Conversão resulta em contagem fracionária; informe a contagem inteira real.");
        var normalized = decimal.Round(calculated, 6, MidpointRounding.ToEven);
        if (normalized <= 0) throw new ArgumentException("Quantidade resulta em zero na resolução do estoque; corrija o apontamento.");
        return new(quantity, declaredUnit, CatalogRules.Format(calculated), CatalogRules.Format(normalized), CatalogRules.Format(calculated - normalized), canonicalUnit,
            direction is null ? null : CatalogRules.Format(conversion!.Fator), direction, direction is null ? null : conversion!.Proveniencia, calculated != normalized);
    }
    public static void Accept(PhysicalQuantity quantity, QuantizationAcceptance? acceptance)
    {
        if (!quantity.ExigeAceite) { if (acceptance is not null) throw new ArgumentException("Aceite de quantização não corresponde ao apontamento."); return; }
        if (acceptance is null || PreciseDecimal(acceptance.Calculado) != PreciseDecimal(quantity.Calculada) || PreciseDecimal(acceptance.Normalizado) != PreciseDecimal(quantity.Normalizada) || PreciseDecimal(acceptance.Residuo) != PreciseDecimal(quantity.Residuo))
            throw new ArgumentException("Confira a prévia e aceite explicitamente o valor e resíduo atuais.");
        CatalogRules.Text(acceptance.Motivo, 2000, "Motivo do aceite");
    }
    public static decimal PreciseDecimal(string? value)
    {
        if (value is null || value.Length > 64 || !System.Text.RegularExpressions.Regex.IsMatch(value, @"^-?(0|[1-9][0-9]*)(\.[0-9]+)?$") ||
            !decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var result)) throw new ArgumentException("Decimal derivado invariante inválido.");
        // Parsing an excessive fractional tail can round in System.Decimal. Reject that loss of precision.
        var meaningful = value.Contains('.') ? value.TrimEnd('0').TrimEnd('.') : value;
        if (meaningful is "-0") meaningful = "0";
        if (CatalogRules.Format(result) != meaningful) throw new ArgumentException("Valor derivado excede a precisão decimal disponível.");
        return result;
    }
    public static IReadOnlyList<string> Unavailable(Item item, LoteMaterial lot, LocalEstoque local, DateOnly today, bool internalUse = false)
    {
        var reasons = new List<string>();
        if (!item.Ativo) reasons.Add("Item inativo");
        if (internalUse && !item.UsoInterno) reasons.Add("Item não permite consumo interno");
        if (!local.Ativo) reasons.Add("Local inativo");
        if (local.Finalidade != "Ordinario") reasons.Add("Local de segregação");
        if (!lot.Ativo) reasons.Add("Lote inativo");
        if (lot.Situacao != "Liberado") reasons.Add("Lote " + lot.Situacao.ToLowerInvariant());
        if (Expired(lot.Validade, today)) reasons.Add("Lote vencido");
        return reasons;
    }
}
public static class InventoryJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { Converters = { new DecimalStrings(), new LongStrings() } };
    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);
    public static T Read<T>(string value) => JsonSerializer.Deserialize<T>(value, Options)!;
    private sealed class DecimalStrings : JsonConverter<decimal>
    {
        public override decimal Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) => decimal.Parse(reader.GetString()!, CultureInfo.InvariantCulture);
        public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) => writer.WriteStringValue(CatalogRules.Format(value));
    }
    private sealed class LongStrings : JsonConverter<long>
    {
        public override long Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) => long.Parse(reader.GetString()!, CultureInfo.InvariantCulture);
        public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
    }
}
