namespace ZZZScanner.Converters;

using System.Text.Json;
using System.Text.Json.Serialization;
using static ZZZScanner.Config;

public class StatValueRangeJsonConverter : JsonConverter<StatValueRange>
{
    public override StatValueRange Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            reader.Read();
            if (reader.TokenType == JsonTokenType.Number)
            {
                var start = reader.GetSingle();
                reader.Read();
                var step = reader.GetSingle();
                reader.Read();
                var stop = reader.GetSingle();
                reader.Read();
                return new StatValueRange(start, step, stop);
            }
            else
            {
                var start = reader.GetString();
                reader.Read();
                var step = reader.GetString();
                reader.Read();
                var stop = reader.GetString();
                reader.Read();
                return new StatValueRange(start, step, stop);
            }
        }

        throw new JsonException($"无法转换为 {typeof(StatValueRange)}");
    }

    public override void Write(Utf8JsonWriter writer, StatValueRange value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        if (value.IsPercent)
        {
            writer.WriteStringValue($"{value.Start}%");
            writer.WriteStringValue($"{value.Step}%");
            writer.WriteStringValue($"{value.Stop}%");
        }
        else
        {
            writer.WriteNumberValue(value.Start);
            writer.WriteNumberValue(value.Step);
            writer.WriteNumberValue(value.Stop);
        }
        writer.WriteEndArray();
    }
}
