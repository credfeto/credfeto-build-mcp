using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Overrides;

namespace Credfeto.Build.Node.Server.Detection;

public static class NodeDetector
{
    public const string NPM_LOCK_FILE = LockFiles.NPM;

    private const int DEFAULT_TIMEOUT_MINUTES = 30;
    private const int MAX_TIMEOUT_MINUTES = 240;
    private const string SOURCE_DIRECTORY = "src";

    // The install roots the CI npm action looks in: the repository root and src/
    private static readonly string[] InstallRootCandidates = [NodePackage.ROOT_DIRECTORY, SOURCE_DIRECTORY];

    // Watched whatever is found: where a package.json can appear, and the overrides file's directory
    public static IReadOnlyList<string> BaseWatchedDirectories { get; } = ["", SOURCE_DIRECTORY, ".ai"];

    public static async ValueTask<NodeDetection> DetectAsync(string repositoryRoot, CancellationToken cancellationToken)
    {
        OverridesLoadResult loaded = await OverridesLoader.LoadAsync(
            repositoryRoot: repositoryRoot,
            cancellationToken: cancellationToken
        );
        NodeOverrides? overrides = loaded.Overrides.Node;

        if (overrides?.Enabled == false)
        {
            return NotDetected(
                repositoryRoot: repositoryRoot,
                reason: "Disabled by " + OverridesLoader.RELATIVE_PATH,
                error: loaded.NodeErrors,
                watched: BaseWatchedDirectories
            );
        }

        if (!Directory.Exists(repositoryRoot))
        {
            return NotDetected(
                repositoryRoot: repositoryRoot,
                reason: "The repository root does not exist",
                error: loaded.NodeErrors,
                watched: BaseWatchedDirectories
            );
        }

        PackageScan scan = new(repositoryRoot);

        foreach (string candidate in InstallRootCandidates)
        {
            await scan.AddInstallRootAsync(relativeDirectory: candidate, cancellationToken: cancellationToken);
        }

        return Describe(scan: scan, overrides: overrides, loadErrors: loaded.NodeErrors);
    }

    private static NodeDetection Describe(PackageScan scan, NodeOverrides? overrides, string? loadErrors)
    {
        IReadOnlyList<string> watched =
        [
            .. BaseWatchedDirectories.Concat(scan.WatchedDirectories).Distinct(StringComparer.Ordinal),
        ];

        if (scan.Problem is not null || scan.InstallRoots.Count == 0)
        {
            return NotDetected(
                repositoryRoot: scan.RepositoryRoot,
                reason: scan.Problem ?? "No package.json at the repository root or in src/",
                error: loadErrors,
                watched: watched
            );
        }

        (IReadOnlyList<NodePackage> packages, string? packagesError) = Select(
            packages: scan.Packages,
            overrides: overrides
        );
        (string? coverageFile, string? coverageFileError) = ValidCoverageFile(overrides);
        string? error = Combine(first: Combine(first: loadErrors, second: packagesError), second: coverageFileError);

        if (packages.Count == 0)
        {
            return NotDetected(
                repositoryRoot: scan.RepositoryRoot,
                reason: "No packages are left once " + OverridesLoader.RELATIVE_PATH + " is applied",
                error: error,
                watched: watched
            );
        }

        NodeRepository repository = new(
            RepositoryRoot: scan.RepositoryRoot,
            InstallRoots: scan.InstallRoots,
            Packages: packages,
            Timeout: TimeSpan.FromMinutes(
                Math.Clamp(
                    value: overrides?.TimeoutMinutes ?? DEFAULT_TIMEOUT_MINUTES,
                    min: 1,
                    max: MAX_TIMEOUT_MINUTES
                )
            )
        )
        {
            TestScript = overrides?.TestScript,
            CoverageScript = overrides?.CoverageScript,
            CoverageFile = coverageFile,
        };

        return new(
            RepositoryRoot: scan.RepositoryRoot,
            Repository: repository,
            Reason: "npm packages found",
            OverridesError: error,
            WatchedDirectories: watched
        );
    }

    // The overrides' packages narrow the detected ones and never add any; a name that matches nothing is reported
    private static (IReadOnlyList<NodePackage> Packages, string? Error) Select(
        IReadOnlyList<NodePackage> packages,
        NodeOverrides? overrides
    )
    {
        if (overrides?.Packages is not { Count: > 0 } selected)
        {
            return (packages, null);
        }

        NodePackage[] kept =
        [
            .. packages.Where(package => selected.Contains(value: package.Name, comparer: StringComparer.Ordinal)),
        ];
        string[] unknown =
        [
            .. selected.Where(name =>
                !packages.Any(package => StringComparer.Ordinal.Equals(x: package.Name, y: name))
            ),
        ];

        return unknown.Length == 0
            ? (kept, null)
            : (
                kept,
                "node.packages names packages that were not found: " + string.Join(separator: ", ", value: unknown)
            );
    }

    // A coverage file outside the package would let the overrides point the report reader anywhere, so it is refused
    // and the default used; the value is not repeated in the error
    private static (string? File, string? Error) ValidCoverageFile(NodeOverrides? overrides)
    {
        if (overrides?.CoverageFile is not { } file)
        {
            return (null, null);
        }

        return IsInsidePackage(file)
            ? (file, null)
            : (null, "node.coverageFile must be a relative path inside the package, so the default is used");
    }

    // Relative, with no empty, . or .. segment and no backslash, so the path cannot leave the package's directory
    private static bool IsInsidePackage(string file)
    {
        return !string.IsNullOrWhiteSpace(file)
            && !Path.IsPathRooted(file)
            && !file.Contains(value: '\\', comparisonType: StringComparison.Ordinal)
            && file.Split('/').All(static segment => segment.Length != 0 && segment is not "." and not "..");
    }

    private static NodeDetection NotDetected(
        string repositoryRoot,
        string reason,
        string? error,
        IReadOnlyList<string> watched
    )
    {
        return new(
            RepositoryRoot: repositoryRoot,
            Repository: null,
            Reason: reason,
            OverridesError: error,
            WatchedDirectories: watched
        );
    }

    private static string? Combine(string? first, string? second)
    {
        if (first is null)
        {
            return second;
        }

        return second is null ? first : first + "; " + second;
    }
}
