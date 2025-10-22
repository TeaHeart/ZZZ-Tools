namespace ZZZScanner.Converters;

using System.Drawing;
using System.Text.Json;
using System.Text.Json.Serialization;

public class PointJsonConverter : JsonConverter<Point>
{
    public override Point Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            reader.Read();
            var x = reader.GetInt32();
            reader.Read();
            var y = reader.GetInt32();
            reader.Read();
            return new Point(x, y);
        }

        throw new JsonException($"无法转换为 {typeof(Point)}");
    }

    public override void Write(Utf8JsonWriter writer, Point value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(value.X);
        writer.WriteNumberValue(value.Y);
        writer.WriteEndArray();
    }
}
