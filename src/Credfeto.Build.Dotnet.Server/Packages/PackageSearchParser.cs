using System;
using System.Collections.Generic;
using System.Text.Json;
using Credfeto.Build.Dotnet.Server.Models;
using Credfeto.Build.Dotnet.Server.Tools.Exceptions;

namespace Credfeto.Build.Dotnet.Server.Packages;

// Reads dotnet package search --format json: searchResult[] of { sourceName, problems[], packages[] { id, version } }
public static class PackageSearchParser
{
    private const string UNREADABLE = "dotnet package search did not return the JSON it was asked for";

    public static PackageSearchResult Parse(string json, string packageId)
    {
        int start = json.IndexOf('{', StringComparison.Ordinal);

        if (start < 0)
        {
            throw new ToolUnavailableException(UNREADABLE);
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(json[start..]);

            return Read(root: document.RootElement, packageId: packageId);
        }
        catch (Exception exception)
            when (exception is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            throw new ToolUnavailableException(message: UNREADABLE, innerException: exception);
        }
    }

    private static PackageSearchResult Read(JsonElement root, string packageId)
    {
        List<SourceSearchResult> sources = [];
        List<PackageSourceProblem> problems = [];
        AddProblems(problems: problems, source: null, element: root);

        foreach (JsonElement result in Items(element: root, name: "searchResult"))
        {
            string source = result.GetProperty("sourceName").GetString() ?? string.Empty;
            AddProblems(problems: problems, source: source, element: result);
            sources.Add(new(Source: source, Versions: Versions(result: result, packageId: packageId)));
        }

        return new(Sources: sources, Problems: problems);
    }

    private static List<string> Versions(JsonElement result, string packageId)
    {
        List<string> versions = [];

        foreach (JsonElement package in Items(element: result, name: "packages"))
        {
            // --exact-match lists only this id, but the id check keeps any other entry out
            if (
                StringComparer.OrdinalIgnoreCase.Equals(x: package.GetProperty("id").GetString(), y: packageId)
                && package.GetProperty("version").GetString() is { Length: > 0 } version
            )
            {
                versions.Add(version);
            }
        }

        return versions;
    }

    private static void AddProblems(List<PackageSourceProblem> problems, string? source, JsonElement element)
    {
        foreach (JsonElement problem in Items(element: element, name: "problems"))
        {
            problems.Add(new(Source: source, Text: problem.GetProperty("text").GetString() ?? string.Empty));
        }
    }

    private static IEnumerable<JsonElement> Items(JsonElement element, string name)
    {
        if (
            element.TryGetProperty(propertyName: name, value: out JsonElement items)
            && items.ValueKind == JsonValueKind.Array
        )
        {
            return items.EnumerateArray();
        }

        return [];
    }
}
