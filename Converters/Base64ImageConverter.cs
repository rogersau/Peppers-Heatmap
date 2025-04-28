using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HeatMapAPI.Converters
{
    public class Base64ImageConverter : JsonConverter<byte[]>
    {
        public override byte[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // Handle deserialization if needed
            if (reader.TokenType == JsonTokenType.String)
            {
                string base64String = reader.GetString();
                if (base64String != null)
                {
                    // Remove the prefix if it exists
                    if (base64String.StartsWith("data:image/png;base64,"))
                    {
                        base64String = base64String.Substring("data:image/png;base64,".Length);
                    }
                    
                    return Convert.FromBase64String(base64String);
                }
            }
            
            return Array.Empty<byte>();
        }

        public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options)
        {
            if (value == null || value.Length == 0)
            {
                writer.WriteNullValue();
                return;
            }

            // Convert the byte array to a base64 string and add the data URL prefix
            string base64String = Convert.ToBase64String(value);
            writer.WriteStringValue("data:image/png;base64," + base64String);
        }
    }
}
