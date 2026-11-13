using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GenSW.Domain.Optimization;

[JsonConverter(typeof(RationalJsonConverter))]
public readonly struct Rational : IComparable<Rational>, IEquatable<Rational>
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private readonly BigInteger denominator;

    public Rational(BigInteger numerator, BigInteger denominator)
    {
        if (denominator.IsZero)
            throw new DivideByZeroException();

        var divisor = BigInteger.GreatestCommonDivisor(numerator, denominator) * denominator.Sign;
        Numerator = numerator / divisor;
        this.denominator = denominator / divisor;
    }

    public BigInteger Numerator { get; }
    public BigInteger Denominator => denominator.IsZero ? BigInteger.One : denominator;
    public bool IsZero => Numerator.IsZero;

    public static Rational FromDecimal(decimal value) => Parse(value.ToString(Invariant));

    public static Rational FromDouble(double value)
    {
        if (!double.IsFinite(value))
            throw new ArgumentOutOfRangeException(nameof(value));

        return Parse(value.ToString("R", Invariant));
    }

    public static Rational Parse(string value)
    {
        var ratio = value.Split('/');
        if (ratio.Length == 2)
            return new Rational(BigInteger.Parse(ratio[0], Invariant), BigInteger.Parse(ratio[1], Invariant));

        var parts = value.ToUpperInvariant().Split('E');
        var exponent = parts.Length == 1 ? 0 : int.Parse(parts[1], Invariant);
        var point = parts[0].IndexOf('.');
        var scale = checked((point < 0 ? 0 : parts[0].Length - point - 1) - exponent);
        if (parts.Length > 2 || scale is < -1000 or > 1000)
            throw new FormatException("Invalid rational scale.");

        var numerator = BigInteger.Parse(parts[0].Replace(".", ""), Invariant);
        return scale >= 0
            ? new Rational(numerator, BigInteger.Pow(10, scale))
            : new Rational(numerator * BigInteger.Pow(10, -scale), BigInteger.One);
    }

    public double ToDouble() => (double)Numerator / (double)Denominator;

    public BigInteger Floor()
    {
        var quotient = BigInteger.DivRem(Numerator, Denominator, out var remainder);
        return remainder.Sign < 0 ? quotient - BigInteger.One : quotient;
    }

    public string ToDecimalString(int fractionalDigits = 12)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(fractionalDigits);
        var factor = BigInteger.Pow(10, fractionalDigits);
        var scaled = BigInteger.DivRem(BigInteger.Abs(Numerator) * factor, Denominator, out var remainder);
        var comparison = (2 * remainder).CompareTo(Denominator);
        if (comparison > 0 || (comparison == 0 && !scaled.IsEven))
            scaled++;

        var integer = BigInteger.DivRem(scaled, factor, out var fraction);
        var sign = Numerator.Sign < 0 && !scaled.IsZero ? "-" : "";
        if (fraction.IsZero)
            return sign + integer.ToString(Invariant);

        return sign + integer.ToString(Invariant) + "." + fraction.ToString(Invariant).PadLeft(fractionalDigits, '0').TrimEnd('0');
    }

    public Rational Abs() => Numerator.Sign < 0 ? -this : this;
    public int CompareTo(Rational other) => (Numerator * other.Denominator).CompareTo(other.Numerator * Denominator);
    public bool Equals(Rational other) => Numerator == other.Numerator && Denominator == other.Denominator;
    public override bool Equals(object? obj) => obj is Rational other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Numerator, Denominator);
    public override string ToString() => Denominator.IsOne
        ? Numerator.ToString(Invariant)
        : $"{Numerator.ToString(Invariant)}/{Denominator.ToString(Invariant)}";

    public static implicit operator Rational(int value) => new(value, BigInteger.One);
    public static Rational operator +(Rational left, Rational right) => new(
        left.Numerator * right.Denominator + right.Numerator * left.Denominator, left.Denominator * right.Denominator);
    public static Rational operator -(Rational left, Rational right) => left + -right;
    public static Rational operator -(Rational value) => new(-value.Numerator, value.Denominator);
    public static Rational operator *(Rational left, Rational right) => new(left.Numerator * right.Numerator, left.Denominator * right.Denominator);
    public static Rational operator /(Rational left, Rational right) => new(left.Numerator * right.Denominator, left.Denominator * right.Numerator);
    public static bool operator <(Rational left, Rational right) => left.CompareTo(right) < 0;
    public static bool operator >(Rational left, Rational right) => left.CompareTo(right) > 0;
    public static bool operator <=(Rational left, Rational right) => left.CompareTo(right) <= 0;
    public static bool operator >=(Rational left, Rational right) => left.CompareTo(right) >= 0;
    public static bool operator ==(Rational left, Rational right) => left.Equals(right);
    public static bool operator !=(Rational left, Rational right) => !left.Equals(right);
}

public sealed class RationalJsonConverter : JsonConverter<Rational>
{
    public override Rational Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        Rational.Parse(reader.GetString() ?? throw new JsonException("Rational value required."));

    public override void Write(Utf8JsonWriter writer, Rational value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToString());
}
