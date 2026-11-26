using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Credfeto.Build.Server.Tools.Exceptions;

namespace Credfeto.Build.Server.Tools;

public sealed partial class ToolArguments
{
    public const string JOB_ID_PATTERN = "^[0-9a-f]{32}$";

    private readonly Dictionary<string, JsonElement> _values;

    public ToolArguments(
        IEnumerable<KeyValuePair<string, JsonElement>>? arguments,
        IReadOnlyList<SchemaProperty> schema
    )
    {
        this._values = [with(StringComparer.Ordinal)];

        foreach ((string name, JsonElement value) in arguments ?? [])
        {
            if (!schema.Any(property => StringComparer.Ordinal.Equals(x: property.Name, y: name)))
            {
                throw new ToolArgumentException("Unknown parameter; this tool takes: " + Names(schema));
            }

            if (value.ValueKind != JsonValueKind.Null)
            {
                this._values[name] = value;
            }
        }

        SchemaProperty? missing = schema.FirstOrDefault(property =>
            property.Required && !this._values.ContainsKey(property.Name)
        );

        if (missing is not null)
        {
            throw new ToolArgumentException("Missing required parameter " + missing.Name);
        }
    }

    public string? GetChoice(SchemaProperty property)
    {
        return this.GetChoice(property: property, allowed: property.Values);
    }

    public string? GetChoice(SchemaProperty property, IReadOnlyList<string>? allowed)
    {
        string? value = this.GetString(property.Name);

        if (value is null)
        {
            return property.DefaultValue;
        }

        if (allowed is not null && !allowed.Contains(value: value, comparer: StringComparer.Ordinal))
        {
            throw new ToolArgumentException(
                allowed.Count == 0
                    ? "Invalid value for " + property.Name + "; there is nothing to choose from now"
                    : "Invalid value for "
                        + property.Name
                        + "; expected one of: "
                        + string.Join(separator: ", ", values: allowed)
            );
        }

        return value;
    }

    public string GetJobId(SchemaProperty property)
    {
        string? value = this.GetString(property.Name);

        return IsWholeMatch(value: value, pattern: JobId())
            ? value
            : throw new ToolArgumentException(
                "Invalid value for " + property.Name + "; expected a job id from a previous call"
            );
    }

    public bool GetFlag(SchemaProperty property)
    {
        if (!this._values.TryGetValue(key: property.Name, value: out JsonElement value))
        {
            return StringComparer.Ordinal.Equals(x: property.DefaultValue, y: SchemaProperty.TRUE);
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw new ToolArgumentException("Invalid value for " + property.Name + "; expected true or false"),
        };
    }

    public string GetMatching(SchemaProperty property, Regex pattern)
    {
        string? value = this.GetString(property.Name);

        return IsWholeMatch(value: value, pattern: pattern)
            ? value
            : throw new ToolArgumentException(
                "Invalid value for " + property.Name + "; expected a value matching " + property.Pattern
            );
    }

    // Absent is an empty list; every item must wholly match the pattern
    public IReadOnlyList<string> GetStrings(SchemaProperty property, Regex pattern)
    {
        if (!this._values.TryGetValue(key: property.Name, value: out JsonElement value))
        {
            return [];
        }

        // Anything but an array reads as one item that cannot match, so every bad shape gets the same answer
        string?[] items =
            value.ValueKind == JsonValueKind.Array ? [.. value.EnumerateArray().Select(ItemText)] : [null];

        return items.All(item => IsWholeMatch(value: item, pattern: pattern))
            ? [.. items.OfType<string>()]
            : throw new ToolArgumentException(
                "Invalid value for " + property.Name + "; expected a list of values matching " + property.Pattern
            );
    }

    private static string? ItemText(JsonElement item)
    {
        return item.ValueKind == JsonValueKind.String ? item.GetString() : null;
    }

    public int GetInteger(SchemaProperty property)
    {
        if (!this._values.TryGetValue(key: property.Name, value: out JsonElement value))
        {
            return int.Parse(s: property.DefaultValue ?? "0", provider: CultureInfo.InvariantCulture);
        }

        return
            value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out int number)
            && number >= property.Minimum
            && number <= property.Maximum
            ? number
            : throw new ToolArgumentException(
                "Invalid value for "
                    + property.Name
                    + "; expected a whole number from "
                    + property.Minimum.ToString(CultureInfo.InvariantCulture)
                    + " to "
                    + property.Maximum.ToString(CultureInfo.InvariantCulture)
            );
    }

    private string? GetString(string name)
    {
        if (!this._values.TryGetValue(key: name, value: out JsonElement value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : throw new ToolArgumentException("Invalid value for " + name + "; expected a string");
    }

    // A .NET $ also matches before a final newline, so the match must span the whole value
    private static bool IsWholeMatch([NotNullWhen(true)] string? value, Regex pattern)
    {
        return value is not null && pattern.Match(value) is { Success: true } match && match.Length == value.Length;
    }

    private static string Names(IReadOnlyList<SchemaProperty> schema)
    {
        return schema.Count == 0
            ? "no parameters"
            : string.Join(separator: ", ", values: schema.Select(static property => property.Name));
    }

    [GeneratedRegex(pattern: JOB_ID_PATTERN, options: RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex JobId();
}
