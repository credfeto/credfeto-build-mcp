using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Credfeto.Build.Dotnet.Server.Tools.Exceptions;

namespace Credfeto.Build.Dotnet.Server.Tools;

/// <summary>
///     The arguments of one tool call, checked against the tool's schema: unknown names, non-string values and
///     values outside an enum are refused. Rejected values are never echoed back.
/// </summary>
public sealed partial class ToolArguments
{
    public const string JOB_ID_PATTERN = "^[0-9a-f]{32}$";

    private readonly Dictionary<string, JsonElement> _values;

    public ToolArguments(IEnumerable<KeyValuePair<string, JsonElement>>? arguments, IReadOnlyList<SchemaProperty> schema)
    {
        this._values = new(StringComparer.Ordinal);

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

        SchemaProperty? missing = schema.FirstOrDefault(property => property.Required && !this._values.ContainsKey(property.Name));

        if (missing is not null)
        {
            throw new ToolArgumentException("Missing required parameter " + missing.Name);
        }
    }

    public string? GetChoice(SchemaProperty property)
    {
        return this.GetChoice(property: property, allowed: property.Values);
    }

    /// <summary>
    ///     Checks the value against <paramref name="allowed" /> rather than the enum in the published schema, so a
    ///     choice that depends on the repository is checked against what is there now.
    /// </summary>
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
                    : "Invalid value for " + property.Name + "; expected one of: " + string.Join(separator: ", ", values: allowed)
            );
        }

        return value;
    }

    public string GetJobId(SchemaProperty property)
    {
        string? value = this.GetString(property.Name);

        return value is not null && JobId().IsMatch(value)
            ? value
            : throw new ToolArgumentException("Invalid value for " + property.Name + "; expected a job id from a previous call");
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

    private static string Names(IReadOnlyList<SchemaProperty> schema)
    {
        return schema.Count == 0 ? "no parameters" : string.Join(separator: ", ", values: schema.Select(static property => property.Name));
    }

    [GeneratedRegex(pattern: JOB_ID_PATTERN, options: RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex JobId();
}
