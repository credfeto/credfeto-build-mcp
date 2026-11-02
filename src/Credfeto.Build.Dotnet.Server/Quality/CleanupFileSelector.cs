using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Repository;

namespace Credfeto.Build.Dotnet.Server.Quality;

/// <summary>
///     Chooses the .cs files for cleanup from git: the ones changed against the merge-base with the default
///     branch (committed, staged, unstaged and untracked), or every tracked one.
/// </summary>
public static class CleanupFileSelector
{
    public const string SCOPE_CHANGED = "changed";
    public const string SCOPE_ALL = "all";

    private const string SOURCE_EXTENSION = ".cs";

    private static readonly string[] DefaultBranchCandidates = ["origin/main", "origin/master", "main", "master"];
    private static readonly string[] IgnoredDirectories = ["bin", "obj", ".git"];

    public static async ValueTask<CleanupSelection> SelectAsync(string repositoryRoot, string scope, CancellationToken cancellationToken)
    {
        return StringComparer.Ordinal.Equals(x: scope, y: SCOPE_ALL)
            ? await SelectAllAsync(repositoryRoot: repositoryRoot, cancellationToken: cancellationToken)
            : await SelectChangedAsync(repositoryRoot: repositoryRoot, cancellationToken: cancellationToken);
    }

    private static async ValueTask<CleanupSelection> SelectAllAsync(string repositoryRoot, CancellationToken cancellationToken)
    {
        string? tracked = await GitCommand.TryRunAsync(repositoryRoot: repositoryRoot, arguments: ["ls-files", "-z", "--cached"], cancellationToken: cancellationToken);

        return tracked is null
            ? NotAGitRepository()
            : new(Files: Filter(repositoryRoot: repositoryRoot, relativePaths: GitCommand.SplitNul(tracked)), BaseReference: null, Error: null);
    }

    private static async ValueTask<CleanupSelection> SelectChangedAsync(string repositoryRoot, CancellationToken cancellationToken)
    {
        string? baseReference = await FindDefaultBranchAsync(repositoryRoot: repositoryRoot, cancellationToken: cancellationToken);

        if (baseReference is null)
        {
            return new(Files: [], BaseReference: null, Error: "The default branch could not be found (tried origin/HEAD, origin/main, origin/master, main and master)");
        }

        string? mergeBase = (await GitCommand.TryRunAsync(repositoryRoot: repositoryRoot, arguments: ["merge-base", "HEAD", baseReference], cancellationToken: cancellationToken))?.Trim();

        if (string.IsNullOrEmpty(mergeBase))
        {
            return new(Files: [], BaseReference: baseReference, Error: "HEAD has no merge-base with " + baseReference);
        }

        string? committed = await GitCommand.TryRunAsync(
            repositoryRoot: repositoryRoot,
            arguments: ["diff", "--name-only", "--relative", "-z", mergeBase + "...HEAD"],
            cancellationToken: cancellationToken
        );
        string? uncommitted = await GitCommand.TryRunAsync(
            repositoryRoot: repositoryRoot,
            arguments: ["diff", "--name-only", "--relative", "-z", "HEAD"],
            cancellationToken: cancellationToken
        );
        string? untracked = await GitCommand.TryRunAsync(
            repositoryRoot: repositoryRoot,
            arguments: ["ls-files", "-z", "--others", "--exclude-standard"],
            cancellationToken: cancellationToken
        );

        if (committed is null || uncommitted is null || untracked is null)
        {
            return NotAGitRepository();
        }

        string[] paths = [.. GitCommand.SplitNul(committed), .. GitCommand.SplitNul(uncommitted), .. GitCommand.SplitNul(untracked)];

        return new(Files: Filter(repositoryRoot: repositoryRoot, relativePaths: paths), BaseReference: baseReference, Error: null);
    }

    private static async ValueTask<string?> FindDefaultBranchAsync(string repositoryRoot, CancellationToken cancellationToken)
    {
        string? remoteHead = (await GitCommand.TryRunAsync(
            repositoryRoot: repositoryRoot,
            arguments: ["symbolic-ref", "--quiet", "--short", "refs/remotes/origin/HEAD"],
            cancellationToken: cancellationToken
        ))?.Trim();

        if (!string.IsNullOrEmpty(remoteHead))
        {
            return remoteHead;
        }

        foreach (string candidate in DefaultBranchCandidates)
        {
            string? found = await GitCommand.TryRunAsync(
                repositoryRoot: repositoryRoot,
                arguments: ["rev-parse", "--verify", "--quiet", candidate + "^{commit}"],
                cancellationToken: cancellationToken
            );

            if (found is not null)
            {
                return candidate;
            }
        }

        return null;
    }

    public static IReadOnlyList<string> Filter(string repositoryRoot, IEnumerable<string> relativePaths)
    {
        return
        [
            .. relativePaths
                .Where(static path => path.EndsWith(value: SOURCE_EXTENSION, comparisonType: StringComparison.OrdinalIgnoreCase) && !IsIgnored(path))
                .Select(path => Path.GetFullPath(path: path, basePath: repositoryRoot))
                .Where(path => RepositoryPaths.IsWithin(root: repositoryRoot, path: path) && File.Exists(path))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];
    }

    private static bool IsIgnored(string relativePath)
    {
        return RepositoryPaths
            .ToForwardSlashes(relativePath)
            .Split('/')
            .SkipLast(1)
            .Any(static segment => IgnoredDirectories.Contains(value: segment, comparer: StringComparer.Ordinal));
    }

    private static CleanupSelection NotAGitRepository()
    {
        return new(Files: [], BaseReference: null, Error: "git could not list the repository's files; is it a git repository?");
    }
}
