using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.Build.Core.Repository;

public static partial class GitHead
{
    private const string GIT_DIRECTORY = ".git";
    private const string REF_PREFIX = "ref: ";
    private const string GITDIR_PREFIX = "gitdir: ";
    private const int MAX_FILE_LENGTH = 1024 * 1024;

    public static async ValueTask<string?> TryReadAsync(string repositoryRoot, CancellationToken cancellationToken)
    {
        string? gitDirectory = await FindGitDirectoryAsync(repositoryRoot: repositoryRoot, cancellationToken: cancellationToken);

        if (gitDirectory is null)
        {
            return null;
        }

        string? head = await ReadFirstLineAsync(path: Path.Combine(path1: gitDirectory, path2: "HEAD"), cancellationToken: cancellationToken);

        if (head is null)
        {
            return null;
        }

        if (!head.StartsWith(value: REF_PREFIX, comparisonType: StringComparison.Ordinal))
        {
            return AsSha(head);
        }

        string reference = head[REF_PREFIX.Length..].Trim();

        return await ResolveReferenceAsync(gitDirectory: gitDirectory, reference: reference, cancellationToken: cancellationToken);
    }

    private static async ValueTask<string?> FindGitDirectoryAsync(string repositoryRoot, CancellationToken cancellationToken)
    {
        string dotGit = Path.Combine(path1: repositoryRoot, path2: GIT_DIRECTORY);

        if (Directory.Exists(dotGit))
        {
            return dotGit;
        }

        // A worktree or submodule has a .git file pointing at the real git directory
        string? pointer = await ReadFirstLineAsync(path: dotGit, cancellationToken: cancellationToken);

        if (pointer is null || !pointer.StartsWith(value: GITDIR_PREFIX, comparisonType: StringComparison.Ordinal))
        {
            return null;
        }

        string target = pointer[GITDIR_PREFIX.Length..].Trim();

        return Path.GetFullPath(path: target, basePath: repositoryRoot);
    }

    private static async ValueTask<string?> ResolveReferenceAsync(string gitDirectory, string reference, CancellationToken cancellationToken)
    {
        if (!ReferenceName().IsMatch(reference) || reference.Contains(value: "..", comparisonType: StringComparison.Ordinal))
        {
            return null;
        }

        string commonDirectory = await FindCommonDirectoryAsync(gitDirectory: gitDirectory, cancellationToken: cancellationToken);

        return AsSha(await ReadFirstLineAsync(path: Path.Combine(path1: gitDirectory, path2: reference), cancellationToken: cancellationToken))
            ?? AsSha(await ReadFirstLineAsync(path: Path.Combine(path1: commonDirectory, path2: reference), cancellationToken: cancellationToken))
            ?? await FindPackedReferenceAsync(commonDirectory: commonDirectory, reference: reference, cancellationToken: cancellationToken);
    }

    private static async ValueTask<string> FindCommonDirectoryAsync(string gitDirectory, CancellationToken cancellationToken)
    {
        string? common = await ReadFirstLineAsync(path: Path.Combine(path1: gitDirectory, path2: "commondir"), cancellationToken: cancellationToken);

        return common is null ? gitDirectory : Path.GetFullPath(path: common.Trim(), basePath: gitDirectory);
    }

    private static async ValueTask<string?> FindPackedReferenceAsync(string commonDirectory, string reference, CancellationToken cancellationToken)
    {
        string packed = Path.Combine(path1: commonDirectory, path2: "packed-refs");

        if (!IsReadable(packed))
        {
            return null;
        }

        await foreach (string line in File.ReadLinesAsync(path: packed, cancellationToken: cancellationToken))
        {
            string[] parts = line.Split(separator: ' ', count: 2);

            if (parts.Length == 2 && StringComparer.Ordinal.Equals(x: parts[1].Trim(), y: reference))
            {
                return AsSha(parts[0]);
            }
        }

        return null;
    }

    private static async ValueTask<string?> ReadFirstLineAsync(string path, CancellationToken cancellationToken)
    {
        if (!IsReadable(path))
        {
            return null;
        }

        using StreamReader reader = new(path);
        string? line = await reader.ReadLineAsync(cancellationToken);

        return line?.Trim();
    }

    private static bool IsReadable(string path)
    {
        FileInfo file = new(path);

        return file.Exists && file.Length <= MAX_FILE_LENGTH;
    }

    private static string? AsSha(string? value)
    {
        if (value is null)
        {
            return null;
        }

        string trimmed = value.Trim();

        return Sha().IsMatch(trimmed) ? trimmed : null;
    }

    [GeneratedRegex(pattern: "^(?:[0-9a-f]{40}|[0-9a-f]{64})$", options: RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Sha();

    [GeneratedRegex(pattern: "^refs/[A-Za-z0-9._/-]+$", options: RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ReferenceName();
}
