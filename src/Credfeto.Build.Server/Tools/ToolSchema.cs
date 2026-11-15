using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace Credfeto.Build.Server.Tools;

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

            string[] required =
            [
                .. properties.Where(static property => property.Required).Select(static property => property.Name),
            ];

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
        writer.WriteString(propertyName: "type", value: TypeName(property));
        writer.WriteString(propertyName: "description", value: property.Description);

        if (property.WholeNumber)
        {
            WriteInteger(writer: writer, property: property);

            return;
        }

        if (property.Flag)
        {
            writer.WriteBoolean(
                propertyName: "default",
                value: StringComparer.Ordinal.Equals(x: property.DefaultValue, y: SchemaProperty.TRUE)
            );
            writer.WriteEndObject();

            return;
        }

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

    private static string TypeName(SchemaProperty property)
    {
        if (property.Flag)
        {
            return "boolean";
        }

        return property.WholeNumber ? "integer" : "string";
    }

    private static void WriteInteger(Utf8JsonWriter writer, SchemaProperty property)
    {
        writer.WriteNumber(propertyName: "minimum", value: property.Minimum);
        writer.WriteNumber(propertyName: "maximum", value: property.Maximum);

        if (property.DefaultValue is not null)
        {
            writer.WriteNumber(
                propertyName: "default",
                value: int.Parse(s: property.DefaultValue, provider: CultureInfo.InvariantCulture)
            );
        }

        writer.WriteEndObject();
    }
}
