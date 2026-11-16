using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Credfeto.Build.Node.Server.Detection;

// Which package manager an install root uses, from its lock files and package.json's packageManager field. Only npm
// is supported: installs always go through npm, so an npm lock file wins even next to another manager's
public static class LockFiles
{
    public const string NPM = "package-lock.json";
    public const string NPM_SHRINKWRAP = "npm-shrinkwrap.json";

    private const string NPM_PACKAGE_MANAGER_PREFIX = "npm@";

    // npm-shrinkwrap.json takes precedence over package-lock.json, as it does for npm
    private static readonly string[] NpmLockFiles = [NPM_SHRINKWRAP, NPM];

    private static readonly (string File, string Manager)[] Unsupported =
    [
        ("bun.lock", "bun"),
        ("bun.lockb", "bun"),
        ("pnpm-lock.yaml", "pnpm"),
        ("yarn.lock", "yarn"),
    ];

    public static IReadOnlyList<string> FileNames { get; } =
    [NPM, NPM_SHRINKWRAP, .. Unsupported.Select(static entry => entry.File)];

    // The npm lock file's name, or the reason the install root is not supported
    public static (string? LockFile, string? Problem) Find(
        string directory,
        string displayDirectory,
        PackageManifest manifest
    )
    {
        string? npmLockFile = NpmLockFiles.FirstOrDefault(file =>
            File.Exists(Path.Combine(path1: directory, path2: file))
        );

        if (npmLockFile is not null)
        {
            return (npmLockFile, null);
        }

        int other = Array.FindIndex(array: Unsupported, match: entry =>
            File.Exists(Path.Combine(path1: directory, path2: entry.File))
        );

        if (other >= 0)
        {
            (string file, string manager) = Unsupported[other];

            return (null, Where(displayDirectory) + " uses " + manager + " (" + file + "); only npm is supported");
        }

        if (
            manifest.PackageManager is { } packageManager
            && !packageManager.StartsWith(value: NPM_PACKAGE_MANAGER_PREFIX, comparisonType: StringComparison.Ordinal)
        )
        {
            // The field's value is repository content, so it is not repeated in the reason
            return (
                null,
                Where(displayDirectory) + " names another package manager in packageManager; only npm is supported"
            );
        }

        return (null, Where(displayDirectory) + " has no package-lock.json (npm ci needs one)");
    }

    private static string Where(string displayDirectory)
    {
        return StringComparer.Ordinal.Equals(x: displayDirectory, y: NodePackage.ROOT_DIRECTORY)
            ? "The repository root"
            : displayDirectory + "/";
    }
}
