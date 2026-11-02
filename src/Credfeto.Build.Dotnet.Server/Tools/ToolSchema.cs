using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Credfeto.Build.Dotnet.Server.Tools;

/// <summary>Builds a tool's JSON input schema by hand, so no reflection is involved.</summary>
public static class ToolSchema
{
    public static JsonElement Create(IReadOnlyList<SchemaProperty> properties)
    {
        ArrayBufferWriter<byte> buffer = new();

        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString(propertyName: "type", value: "object");
            writer.WriteStartObject("properties");

            foreach (SchemaProperty property in properties)
            {
                WriteProperty(writer: writer, property: property);
            }

            writer.WriteEndObject();

            string[] required = [.. properties.Where(static property => property.Required).Select(static property => property.Name)];

            if (required.Length != 0)
            {
                writer.WriteStartArray("required");

                foreach (string name in required)
                {
                    writer.WriteStringValue(name);
                }

                writer.WriteEndArray();
            }

            writer.WriteBoolean(propertyName: "additionalProperties", value: false);
            writer.WriteEndObject();
        }

        using JsonDocument document = JsonDocument.Parse(buffer.WrittenMemory);

        return document.RootElement.Clone();
    }

    private static void WriteProperty(Utf8JsonWriter writer, SchemaProperty property)
    {
        writer.WriteStartObject(property.Name);
        writer.WriteString(propertyName: "type", value: "string");
        writer.WriteString(propertyName: "description", value: property.Description);

        if (property.Values is not null)
        {
            writer.WriteStartArray("enum");

            foreach (string value in property.Values)
            {
                writer.WriteStringValue(value);
            }

            writer.WriteEndArray();
        }

        if (property.Pattern is not null)
        {
            writer.WriteString(propertyName: "pattern", value: property.Pattern);
        }

        if (property.DefaultValue is not null)
        {
            writer.WriteString(propertyName: "default", value: property.DefaultValue);
        }

        writer.WriteEndObject();
    }
}
