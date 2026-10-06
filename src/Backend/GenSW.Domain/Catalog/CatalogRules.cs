using System.Globalization;
using System.Text.RegularExpressions;

namespace GenSW.Domain.Catalog;

public static partial class CatalogRules
{
    public const decimal Maximum = 99999999999999.999999m;
    public static string Text(string? value, int max, string field)
    {
        var text = value?.Trim() ?? "";
        if (text.Length < 1 || text.Length > max) throw new ArgumentException($"{field}: informe entre 1 e {max} caracteres.");
        return text;
    }
    public static string? Optional(string? value, int max = 2000) => string.IsNullOrWhiteSpace(value) ? null : Text(value, max, "Texto");
    public static decimal Decimal(string? value, bool positive = false)
    {
        if (value is null || !DecimalPattern().IsMatch(value) ||
            !decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number) ||
            number > Maximum || (positive && number <= 0))
            throw new ArgumentException("Informe decimal invariante não negativo, até 14 inteiros e 6 casas decimais.");
        return number;
    }
    public static string Format(decimal value) => value.ToString("0.############################", CultureInfo.InvariantCulture);
    public static string Unit(string unit) => unit is "kg" or "g" or "L" or "mL" or "un" ? unit : throw new ArgumentException("Unidade inválida.");
    public static string Dimension(string unit) => Unit(unit) switch { "kg" or "g" => "massa", "L" or "mL" => "volume", _ => "contagem" };
    public static decimal Canonical(decimal value, string unit) => Unit(unit) is "g" or "mL" ? value / 1000m : value;
    public static decimal Quantity(string value, string unit)
    {
        var number = Decimal(value, true);
        Unit(unit);
        if (unit == "un" && decimal.Truncate(number) != number) throw new ArgumentException("Contagem deve ser inteira; ajuste explicitamente a quantidade.");
        return number;
    }
    public static void Expected(int actual, int expected)
    {
        if (actual != expected) throw new CatalogConflictException("versao_desatualizada", "O registro mudou. Recarregue antes de tentar novamente.");
    }
    [GeneratedRegex(@"^(0|[1-9][0-9]{0,13})(\.[0-9]{1,6})?$")]
    private static partial Regex DecimalPattern();
}

public sealed class CatalogConflictException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
