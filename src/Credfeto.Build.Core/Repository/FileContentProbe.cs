using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Jobs;

namespace Credfeto.Build.Core.Repository;

/// <summary>
///     Hashes a set of files before a job runs and again afterwards, and reports the ones whose content changed
///     (or that disappeared), relative to the repository root.
/// </summary>
public sealed class FileContentProbe : IChangedFilesProbe
{
    private readonly IReadOnlyDictionary<string, string?> _before;
    private readonly string _repositoryRoot;

    private FileContentProbe(string repositoryRoot, IReadOnlyDictionary<string, string?> before)
    {
        this._repositoryRoot = repositoryRoot;
        this._before = before;
    }

    public static async ValueTask<FileContentProbe> SnapshotAsync(string repositoryRoot, IReadOnlyList<string> files, CancellationToken cancellationToken)
    {
        return new(repositoryRoot: repositoryRoot, before: await HashAllAsync(files: files, cancellationToken: cancellationToken));
    }

    public async ValueTask<IReadOnlyList<string>> CollectAsync(CancellationToken cancellationToken)
    {
        List<string> changed = [];

        foreach ((string file, string? before) in this._before)
        {
            string? after = await HashAsync(path: file, cancellationToken: cancellationToken);

            if (!StringComparer.Ordinal.Equals(x: before, y: after))
            {
                changed.Add(RepositoryPaths.RelativeTo(root: this._repositoryRoot, path: file));
            }
        }

        changed.Sort(StringComparer.Ordinal);

        return changed;
    }

    private static async ValueTask<IReadOnlyDictionary<string, string?>> HashAllAsync(IReadOnlyList<string> files, CancellationToken cancellationToken)
    {
        Dictionary<string, string?> hashes = [with(StringComparer.Ordinal)];

        foreach (string file in files)
        {
            hashes[file] = await HashAsync(path: file, cancellationToken: cancellationToken);
        }

        return hashes;
    }

    private static async ValueTask<string?> HashAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            await using FileStream stream = new(path: path, mode: FileMode.Open, access: FileAccess.Read, share: FileShare.ReadWrite, bufferSize: 4096, useAsync: true);
            byte[] hash = await SHA256.HashDataAsync(source: stream, cancellationToken: cancellationToken);

            return Convert.ToHexString(hash);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
