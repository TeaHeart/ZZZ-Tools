namespace ZZZScanner.Converters;

using System.Drawing;
using System.Text.Json;
using System.Text.Json.Serialization;

public class ColorJsonConverter : JsonConverter<Color>
{
    public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            reader.Read();
            var a = reader.GetInt32();
            reader.Read();
            var r = reader.GetInt32();
            reader.Read();
            var g = reader.GetInt32();
            reader.Read();
            var b = reader.GetInt32();
            reader.Read();
            return Color.FromArgb(a, r, g, b);
        }

        throw new JsonException($"无法转换为 {typeof(Color)}");
    }

    public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(value.A);
        writer.WriteNumberValue(value.R);
        writer.WriteNumberValue(value.G);
        writer.WriteNumberValue(value.B);
        writer.WriteEndArray();
    }
}