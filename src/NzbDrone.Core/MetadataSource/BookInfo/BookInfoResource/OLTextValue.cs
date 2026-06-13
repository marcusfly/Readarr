using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NzbDrone.Core.MetadataSource.BookInfo
{
    // OL description/bio fields are inconsistently either a plain string
    // or an object {"type": "/type/text", "value": "..."}
    [JsonConverter(typeof(OLTextValueConverter))]
    public class OLTextValue
    {
        public string Value { get; set; }
    }

    internal sealed class OLTextValueConverter : JsonConverter<OLTextValue>
    {
        public override OLTextValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                return new OLTextValue { Value = reader.GetString() };
            }

            if (reader.TokenType == JsonTokenType.StartObject)
            {
                using var doc = JsonDocument.ParseValue(ref reader);
                var val = doc.RootElement.TryGetProperty("value", out var v) ? v.GetString() : null;
                return new OLTextValue { Value = val };
            }

            // skip unknown shapes
            reader.Skip();
            return null;
        }

        public override void Write(Utf8JsonWriter writer, OLTextValue value, JsonSerializerOptions options)
            => writer.WriteStringValue(value?.Value);
    }
}
