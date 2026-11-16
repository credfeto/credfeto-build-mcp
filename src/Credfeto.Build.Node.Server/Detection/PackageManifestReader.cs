using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.Build.Node.Server.Detection;

public static class PackageManifestReader
{
    public const string FILE_NAME = "package.json";

    private const int MAX_MANIFEST_LENGTH = 1024 * 1024;
    private const int MAX_SCRIPT_NAMES = 256;
    private const int MAX_DEPENDENCIES = 2048;
    private const string PACKAGES_PROPERTY = "packages";
    private const string SCRIPTS_PROPERTY = "scripts";

    private static readonly (string Property, DependencySection Section)[] DependencySections =
    [
        ("dependencies", DependencySection.Dependencies),
        ("devDependencies", DependencySection.DevDependencies),
        ("optionalDependencies", DependencySection.OptionalDependencies),
    ];

    // Reads the package.json in the directory; Problem says why it could not be used, with its repository-relative path
    public static async ValueTask<(PackageManifest? Manifest, string? Problem)> ReadAsync(
        string path,
        string displayPath,
        CancellationToken cancellationToken
    )
    {
        FileInfo file = new(path);

        if (file.Length > MAX_MANIFEST_LENGTH)
        {
            return (null, displayPath + " is larger than 1 MiB");
        }

        byte[] content = await File.ReadAllBytesAsync(path: path, cancellationToken: cancellationToken);

        try
        {
            using JsonDocument document = JsonDocument.Parse(content);

            return document.RootElement.ValueKind == JsonValueKind.Object
                ? (Parse(document.RootElement), null)
                : (null, displayPath + " is not a JSON object");
        }
        catch (JsonException)
        {
            return (null, displayPath + " is not valid JSON");
        }
    }

    private static PackageManifest Parse(JsonElement root)
    {
        IReadOnlyList<NodeDependency> dependencies = Dependencies(root);

        return new(
            Name: StringProperty(element: root, name: "name"),
            Scripts: Scripts(root),
            Workspaces: Workspaces(root),
            PackageManager: StringProperty(element: root, name: "packageManager")
        )
        {
            ScriptNames = ScriptNames(root),
            Dependencies = dependencies,
            TestRunner = TestRunnerInference.Infer(testCommand: TestCommand(root), dependencies: dependencies),
        };
    }

    // The command the unit-scope test runs: test:noe2e when there is one, as the pre-commit hook chooses
    private static string? TestCommand(JsonElement root)
    {
        if (!TryGetScripts(root: root, scripts: out JsonElement scripts))
        {
            return null;
        }

        return StringProperty(element: scripts, name: NodeScripts.TEST_NO_E2E)
            ?? StringProperty(element: scripts, name: NodeScripts.TEST);
    }

    private static IReadOnlyList<string> ScriptNames(JsonElement root)
    {
        if (!TryGetScripts(root: root, scripts: out JsonElement scripts))
        {
            return [];
        }

        return
        [
            .. scripts
                .EnumerateObject()
                .Where(static script => script.Value.ValueKind == JsonValueKind.String)
                .Select(static script => script.Name)
                .Take(MAX_SCRIPT_NAMES),
        ];
    }

    private static IReadOnlyList<NodeDependency> Dependencies(JsonElement root)
    {
        List<NodeDependency> dependencies = [];

        foreach ((string property, DependencySection section) in DependencySections)
        {
            if (
                root.TryGetProperty(propertyName: property, value: out JsonElement declared)
                && declared.ValueKind == JsonValueKind.Object
            )
            {
                dependencies.AddRange(
                    declared
                        .EnumerateObject()
                        .Select(dependency => new NodeDependency(Name: dependency.Name, Section: section))
                );
            }
        }

        return [.. dependencies.Take(MAX_DEPENDENCIES)];
    }

    private static bool TryGetScripts(JsonElement root, out JsonElement scripts)
    {
        return root.TryGetProperty(propertyName: SCRIPTS_PROPERTY, value: out scripts)
            && scripts.ValueKind == JsonValueKind.Object;
    }

    private static string? StringProperty(JsonElement element, string name)
    {
        return
            element.TryGetProperty(propertyName: name, value: out JsonElement value)
            && value.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()
            : null;
    }

    private static IReadOnlyList<string> Scripts(JsonElement root)
    {
        if (!TryGetScripts(root: root, scripts: out JsonElement scripts))
        {
            return [];
        }

        return
        [
            .. NodeScripts.Known.Where(name =>
                scripts.TryGetProperty(propertyName: name, value: out JsonElement script)
                && script.ValueKind == JsonValueKind.String
            ),
        ];
    }

    // npm workspaces: an array of patterns, or { "packages": [...] }
    private static IReadOnlyList<string> Workspaces(JsonElement root)
    {
        if (!root.TryGetProperty(propertyName: "workspaces", value: out JsonElement workspaces))
        {
            return [];
        }

        if (
            workspaces.ValueKind == JsonValueKind.Object
            && workspaces.TryGetProperty(propertyName: PACKAGES_PROPERTY, value: out JsonElement packages)
        )
        {
            workspaces = packages;
        }

        if (workspaces.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return
        [
            .. workspaces
                .EnumerateArray()
                .Where(static pattern => pattern.ValueKind == JsonValueKind.String)
                .Select(static pattern => pattern.GetString() ?? string.Empty)
                .Where(static pattern => !string.IsNullOrWhiteSpace(pattern))
                .Distinct(StringComparer.Ordinal),
        ];
    }
}
