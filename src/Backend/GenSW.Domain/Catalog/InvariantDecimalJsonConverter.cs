using System.Text.Json;
using System.Text.Json.Serialization;

namespace GenSW.Domain.Catalog;

public sealed class InvariantDecimalJsonConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String ? CatalogRules.Decimal(reader.GetString()) : throw new JsonException("Decimal exige string invariante.");
    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) => writer.WriteStringValue(CatalogRules.Format(value));
}
