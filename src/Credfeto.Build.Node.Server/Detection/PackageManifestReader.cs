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
    private const string PACKAGES_PROPERTY = "packages";

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
        return new(
            Name: StringProperty(element: root, name: "name"),
            Scripts: Scripts(root),
            Workspaces: Workspaces(root),
            PackageManager: StringProperty(element: root, name: "packageManager")
        );
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
        if (
            !root.TryGetProperty(propertyName: "scripts", value: out JsonElement scripts)
            || scripts.ValueKind != JsonValueKind.Object
        )
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
