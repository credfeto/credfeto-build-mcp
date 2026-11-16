using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Credfeto.Build.Node.Server.Detection;

// Expands an npm workspaces entry: a literal directory or a single-level "dir/*" glob, relative to its install root.
// Anything else (deeper globs, negations, paths leaving the root, node_modules) is not a workspace for this server
public static class WorkspacePattern
{
    public const string IGNORED_DIRECTORY = "node_modules";

    private const string GLOB = "*";
    private const string PARENT = "..";

    private static readonly char[] Separators = ['/', '\\'];

    // The directory to watch for this entry (the glob's parent, or the literal directory), and the package directories
    // it matches today, all relative to the install root with forward slashes; null when the entry is not supported
    public static (string WatchDirectory, IReadOnlyList<string> Directories)? Expand(string installRoot, string pattern)
    {
        string[] segments = pattern.Split(separator: Separators, options: StringSplitOptions.RemoveEmptyEntries);
        segments = [.. segments.SkipWhile(static segment => StringComparer.Ordinal.Equals(x: segment, y: "."))];

        if (segments.Length == 0 || pattern is ['!', ..] || Path.IsPathRooted(pattern))
        {
            return null;
        }

        bool isGlob = StringComparer.Ordinal.Equals(x: segments[^1], y: GLOB);
        string[] directory = isGlob ? segments[..^1] : segments;

        if (directory.Any(IsUnsupportedSegment))
        {
            return null;
        }

        string relative = string.Join(separator: '/', value: directory);

        if (!isGlob)
        {
            return (relative, [relative]);
        }

        return (relative, Children(installRoot: installRoot, relative: relative));
    }

    private static bool IsUnsupportedSegment(string segment)
    {
        return segment.Contains(value: GLOB, comparisonType: StringComparison.Ordinal)
            || StringComparer.Ordinal.Equals(x: segment, y: PARENT)
            || StringComparer.Ordinal.Equals(x: segment, y: ".")
            || StringComparer.Ordinal.Equals(x: segment, y: IGNORED_DIRECTORY);
    }

    private static IReadOnlyList<string> Children(string installRoot, string relative)
    {
        string parent = Path.Combine(path1: installRoot, path2: relative);

        if (!Directory.Exists(parent))
        {
            return [];
        }

        return
        [
            .. Directory
                .EnumerateDirectories(parent)
                .Select(Path.GetFileName)
                .OfType<string>()
                .Where(static name => !StringComparer.Ordinal.Equals(x: name, y: IGNORED_DIRECTORY))
                .Order(StringComparer.Ordinal)
                .Select(name => relative.Length == 0 ? name : relative + "/" + name),
        ];
    }
}
