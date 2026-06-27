using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NzbDrone.Core.MetadataSource.BookInfo
{
    // GET /works/{OLID}.json
    public class OLWorkResource
    {
        [JsonPropertyName("key")]
        public string Key { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("description")]
        public OLTextValue Description { get; set; }

        [JsonPropertyName("covers")]
        public List<long> Covers { get; set; }

        [JsonPropertyName("subjects")]
        public List<string> Subjects { get; set; }

        [JsonPropertyName("authors")]
        public List<OLWorkAuthorRef> Authors { get; set; }

        [JsonPropertyName("first_publish_date")]
        public string FirstPublishDate { get; set; }

        [JsonPropertyName("links")]
        public List<OLLinkResource> Links { get; set; }

        [JsonPropertyName("series")]
        [JsonConverter(typeof(OLSeriesListConverter))]
        public List<string> SeriesList { get; set; }
    }

    public class OLSeriesListConverter : JsonConverter<List<string>>
    {
        public override List<string> Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return new List<string>();
            }

            if (reader.TokenType != JsonTokenType.StartArray)
            {
                throw new JsonException("Expected an array for Open Library series.");
            }

            var result = new List<string>();

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                {
                    return result;
                }

                if (reader.TokenType == JsonTokenType.String)
                {
                    var value = reader.GetString();

                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        result.Add(value);
                    }

                    continue;
                }

                if (reader.TokenType == JsonTokenType.StartObject)
                {
                    using var document = JsonDocument.ParseValue(ref reader);
                    var root = document.RootElement;

                    if (TryGetSeriesText(root, "title", out var title) ||
                        TryGetSeriesText(root, "name", out title) ||
                        TryGetSeriesText(root, "value", out title))
                    {
                        result.Add(title);
                    }

                    continue;
                }

                reader.Skip();
            }

            throw new JsonException("Unexpected end of Open Library series array.");
        }

        public override void Write(Utf8JsonWriter writer, List<string> value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value, options);
        }

        private static bool TryGetSeriesText(JsonElement element, string propertyName, out string value)
        {
            value = null;

            if (!element.TryGetProperty(propertyName, out var property))
            {
                return false;
            }

            if (property.ValueKind == JsonValueKind.String)
            {
                value = property.GetString();
                return !string.IsNullOrWhiteSpace(value);
            }

            return false;
        }
    }

    public class OLWorkAuthorRef
    {
        [JsonPropertyName("author")]
        public OLKeyRef Author { get; set; }

        [JsonPropertyName("type")]
        public OLKeyRef Type { get; set; }
    }

    [JsonConverter(typeof(OLKeyRefConverter))]
    public class OLKeyRef
    {
        [JsonPropertyName("key")]
        public string Key { get; set; }
    }

    public class OLKeyRefConverter : JsonConverter<OLKeyRef>
    {
        public override OLKeyRef Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                var key = reader.GetString();

                return string.IsNullOrWhiteSpace(key) ? null : new OLKeyRef { Key = key };
            }

            if (reader.TokenType == JsonTokenType.StartObject)
            {
                using var document = JsonDocument.ParseValue(ref reader);
                var root = document.RootElement;

                if (TryGetKey(root, "key", out var key) ||
                    TryGetKey(root, "value", out key))
                {
                    return new OLKeyRef { Key = key };
                }

                return null;
            }

            reader.Skip();
            return null;
        }

        public override void Write(Utf8JsonWriter writer, OLKeyRef value, JsonSerializerOptions options)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.Key))
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteStartObject();
            writer.WriteString("key", value.Key);
            writer.WriteEndObject();
        }

        private static bool TryGetKey(JsonElement element, string propertyName, out string value)
        {
            value = null;

            if (!element.TryGetProperty(propertyName, out var property))
            {
                return false;
            }

            if (property.ValueKind == JsonValueKind.String)
            {
                value = property.GetString();
                return !string.IsNullOrWhiteSpace(value);
            }

            if (property.ValueKind == JsonValueKind.Object &&
                property.TryGetProperty("key", out var nestedKey) &&
                nestedKey.ValueKind == JsonValueKind.String)
            {
                value = nestedKey.GetString();
                return !string.IsNullOrWhiteSpace(value);
            }

            return false;
        }
    }

    public class OLPaginationLinks
    {
        [JsonPropertyName("self")]
        public string Self { get; set; }

        [JsonPropertyName("next")]
        public string Next { get; set; }
    }
}
