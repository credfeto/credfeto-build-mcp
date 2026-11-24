using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Credfeto.Build.Python.Server.Detection;

// Finds Python test files (test_*.py and *_test.py, the names pytest collects) in a repository that has no manifest,
// so the server can say where a test suite is that nothing declares. Dependency, cache and build directories are
// skipped, symbolic links are never followed, and the walk stops after a fixed number of entries
public static class TestSuiteScan
{
    private const int MAX_ENTRIES = 50_000;
    private const int MAX_DEPTH = 16;
    private const string EXTENSION = ".py";

    public static IReadOnlyList<string> IgnoredDirectories { get; } =
    [
        ".git",
        ".venv",
        "venv",
        "node_modules",
        ".tox",
        ".nox",
        "__pycache__",
        ".pytest_cache",
        ".mypy_cache",
        ".ruff_cache",
        "site-packages",
        "bin",
        "obj",
    ];

    private static readonly EnumerationOptions Options = new()
    {
        RecurseSubdirectories = false,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.ReparsePoint,
        ReturnSpecialDirectories = false,
    };

    public static bool IsTestFile(string fileName)
    {
        return fileName.EndsWith(value: EXTENSION, comparisonType: StringComparison.Ordinal)
            && (
                fileName.StartsWith(value: "test_", comparisonType: StringComparison.Ordinal)
                || fileName.EndsWith(value: "_test" + EXTENSION, comparisonType: StringComparison.Ordinal)
            );
    }

    // The directories holding test files, relative to the repository root ("." for the root), in order
    public static IReadOnlyList<string> Find(string repositoryRoot)
    {
        SortedSet<string> found = [with(StringComparer.Ordinal)];
        Stack<PendingDirectory> pending = new();
        pending.Push(new(Path: repositoryRoot, Relative: PythonProject.ROOT_DIRECTORY, Depth: 0));
        int remaining = MAX_ENTRIES;

        while (pending.Count != 0 && remaining > 0)
        {
            PendingDirectory current = pending.Pop();
            FileSystemInfo[] entries = Entries(path: current.Path, limit: remaining);
            remaining -= entries.Length;

            if (entries.Any(static entry => entry is FileInfo && IsTestFile(entry.Name)))
            {
                _ = found.Add(current.Relative);
            }

            if (current.Depth >= MAX_DEPTH)
            {
                continue;
            }

            foreach (DirectoryInfo directory in entries.OfType<DirectoryInfo>().Where(static d => !IsIgnored(d.Name)))
            {
                pending.Push(
                    new(
                        Path: directory.FullName,
                        Relative: ProjectReader.Join(directory: current.Relative, file: directory.Name),
                        Depth: current.Depth + 1
                    )
                );
            }
        }

        return [.. found];
    }

    private static FileSystemInfo[] Entries(string path, int limit)
    {
        try
        {
            return
            [
                .. new DirectoryInfo(path)
                    .EnumerateFileSystemInfos(searchPattern: "*", enumerationOptions: Options)
                    .Take(limit),
            ];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A directory removed while it was being walked
            return [];
        }
    }

    private static bool IsIgnored(string name)
    {
        return IgnoredDirectories.Contains(value: name, comparer: StringComparer.Ordinal);
    }

    [System.Diagnostics.DebuggerDisplay("{Relative}")]
    private sealed record PendingDirectory(string Path, string Relative, int Depth);
}
