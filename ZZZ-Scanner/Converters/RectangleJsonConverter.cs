namespace ZZZScanner.Converters;

using System.Drawing;
using System.Text.Json;
using System.Text.Json.Serialization;

public class RectangleJsonConverter : JsonConverter<Rectangle>
{
    public override Rectangle Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            reader.Read();
            var l = reader.GetInt32();
            reader.Read();
            var t = reader.GetInt32();
            reader.Read();
            var r = reader.GetInt32();
            reader.Read();
            var b = reader.GetInt32();
            reader.Read();
            return Rectangle.FromLTRB(l, t, r, b);
        }

        throw new JsonException($"无法转换为 {typeof(Rectangle)}");
    }

    public override void Write(Utf8JsonWriter writer, Rectangle value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(value.Left);
        writer.WriteNumberValue(value.Top);
        writer.WriteNumberValue(value.Right);
        writer.WriteNumberValue(value.Bottom);
        writer.WriteEndArray();
    }
}
