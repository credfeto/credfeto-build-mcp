using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tomlyn;
using Tomlyn.Model;

namespace Credfeto.Build.Python.Server.Detection;

// Reads the few pyproject.toml keys the server uses, through Tomlyn's untyped model (source-generated, so no
// reflection under Native AOT). Problems are fixed text with the file's repository-relative path, never its content
public static class PyprojectReader
{
    public const string FILE_NAME = "pyproject.toml";

    private const int MAX_FILE_LENGTH = 1024 * 1024;
    private const int MAX_ENTRIES = 2048;
    private const int MAX_GROUP_DEPTH = 8;
    private const string PROJECT_TABLE = "project";
    private const string INCLUDE_GROUP = "include-group";

    public static async ValueTask<(PyprojectManifest? Manifest, string? Problem)> ReadAsync(
        string path,
        string displayPath,
        CancellationToken cancellationToken
    )
    {
        if (new FileInfo(path).Length > MAX_FILE_LENGTH)
        {
            return (null, displayPath + " is larger than 1 MiB");
        }

        string text = await File.ReadAllTextAsync(path: path, cancellationToken: cancellationToken);

        return Parse(text: text, displayPath: displayPath);
    }

    public static (PyprojectManifest? Manifest, string? Problem) Parse(string text, string displayPath)
    {
        TomlTable? document;

        try
        {
            document = TomlSerializer.Deserialize(text, PythonTomlContext.Default.TomlTable);
        }
        catch (TomlException)
        {
            return (null, displayPath + " is not valid TOML");
        }

        return document is null ? (null, displayPath + " is not valid TOML") : (Read(document), null);
    }

    private static PyprojectManifest Read(TomlTable document)
    {
        TomlTable? project = Table(table: document, key: PROJECT_TABLE);
        TomlTable? tool = Table(table: document, key: "tool");

        return new(
            HasProjectTable: project is not null,
            Name: project is null ? null : Text(table: project, key: "name"),
            RequiresPython: project is null ? null : Text(table: project, key: "requires-python")
        )
        {
            Dependencies = project is null ? [] : Strings(Value(table: project, key: "dependencies")),
            OptionalDependencies = project is null
                ? Empty()
                : Lists(Table(table: project, key: "optional-dependencies")),
            DependencyGroups = Groups(Table(table: document, key: "dependency-groups")),
            IsPoetry = tool is not null && Table(table: tool, key: "poetry") is not null,
        };
    }

    private static Dictionary<string, IReadOnlyList<string>> Empty()
    {
        return [with(StringComparer.Ordinal)];
    }

    private static Dictionary<string, IReadOnlyList<string>> Lists(TomlTable? table)
    {
        Dictionary<string, IReadOnlyList<string>> lists = Empty();

        if (table is null)
        {
            return lists;
        }

        foreach (string name in table.Keys.Take(MAX_ENTRIES))
        {
            lists[name] = Strings(Value(table: table, key: name));
        }

        return lists;
    }

    // PEP 735 groups: each entry is a requirement or an { include-group = "name" } table, expanded here
    private static Dictionary<string, IReadOnlyList<string>> Groups(TomlTable? table)
    {
        Dictionary<string, IReadOnlyList<string>> groups = Empty();

        if (table is null)
        {
            return groups;
        }

        foreach (string name in table.Keys.Take(MAX_ENTRIES))
        {
            groups[name] = [.. ExpandGroup(table: table, name: name, depth: 0).Distinct(StringComparer.Ordinal)];
        }

        return groups;
    }

    private static IEnumerable<string> ExpandGroup(TomlTable table, string name, int depth)
    {
        if (depth > MAX_GROUP_DEPTH || Value(table: table, key: name) is not TomlArray entries)
        {
            return [];
        }

        return entries
            .Take(MAX_ENTRIES)
            .SelectMany(entry =>
                entry switch
                {
                    string requirement => [requirement],
                    TomlTable include when Text(table: include, key: INCLUDE_GROUP) is { } included => ExpandGroup(
                        table: table,
                        name: included,
                        depth: depth + 1
                    ),
                    _ => [],
                }
            );
    }

    private static TomlTable? Table(TomlTable table, string key)
    {
        return Value(table: table, key: key) as TomlTable;
    }

    private static object? Value(TomlTable table, string key)
    {
        return table.TryGetValue(key: key, value: out object? value) ? value : null;
    }

    private static string? Text(TomlTable table, string key)
    {
        return Value(table: table, key: key) is string text && !string.IsNullOrWhiteSpace(text) ? text : null;
    }

    private static IReadOnlyList<string> Strings(object? value)
    {
        return value is TomlArray array ? [.. array.OfType<string>().Take(MAX_ENTRIES)] : [];
    }
}
