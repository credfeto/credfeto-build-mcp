using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Overrides;

namespace Credfeto.Build.Python.Server.Detection;

// Strict detection: a project is a pyproject.toml with a [project] table or a requirements*.txt, at the repository
// root, in src/ or in a directory python.projects lists. Only when there is none are test files looked for, so the
// server can say where an undeclared test suite is
public static class PythonDetector
{
    public const string ALL_PROJECTS = "all";

    private const int DEFAULT_TIMEOUT_MINUTES = 30;
    private const int MAX_TIMEOUT_MINUTES = 240;
    private const string SOURCE_DIRECTORY = "src";
    private const string NO_PROJECT =
        "No pyproject.toml with a [project] table or requirements*.txt at the repository root or in src/";

    private static readonly string[] ProjectCandidates = [PythonProject.ROOT_DIRECTORY, SOURCE_DIRECTORY];

    // Watched whatever is found: where a manifest can appear, and the overrides file's directory
    public static IReadOnlyList<string> BaseWatchedDirectories { get; } = ["", SOURCE_DIRECTORY, ".ai"];

    public static async ValueTask<PythonDetection> DetectAsync(
        string repositoryRoot,
        CancellationToken cancellationToken
    )
    {
        OverridesLoadResult loaded = await OverridesLoader.LoadAsync(
            repositoryRoot: repositoryRoot,
            cancellationToken: cancellationToken
        );
        PythonOverrides? overrides = loaded.Overrides.Python;

        if (overrides?.Enabled == false)
        {
            return NotDetected(
                repositoryRoot: repositoryRoot,
                reason: "Disabled by " + OverridesLoader.RELATIVE_PATH,
                error: loaded.PythonErrors,
                watched: BaseWatchedDirectories
            );
        }

        if (!Directory.Exists(repositoryRoot))
        {
            return NotDetected(
                repositoryRoot: repositoryRoot,
                reason: "The repository root does not exist",
                error: loaded.PythonErrors,
                watched: BaseWatchedDirectories
            );
        }

        (IReadOnlyList<string> extra, string? extraError) = OverrideProjects(overrides);
        IReadOnlyList<string> watched = [.. BaseWatchedDirectories.Concat(extra).Distinct(StringComparer.Ordinal)];
        (IReadOnlyList<PythonProject> projects, string? missingError) = await ReadProjectsAsync(
            repositoryRoot: repositoryRoot,
            extra: extra,
            cancellationToken: cancellationToken
        );
        string? error = Combine(Combine(loaded.PythonErrors, extraError), missingError);

        if (projects.Count == 0)
        {
            return WithoutProjects(repositoryRoot: repositoryRoot, error: error, watched: watched);
        }

        PythonRepository repository = new(
            RepositoryRoot: repositoryRoot,
            Projects: UniqueNames(projects),
            Timeout: TimeSpan.FromMinutes(
                Math.Clamp(
                    value: overrides?.TimeoutMinutes ?? DEFAULT_TIMEOUT_MINUTES,
                    min: 1,
                    max: MAX_TIMEOUT_MINUTES
                )
            )
        );

        return new(
            RepositoryRoot: repositoryRoot,
            Repository: repository,
            Reason: "Python projects found",
            OverridesError: error,
            WatchedDirectories: watched
        );
    }

    private static async ValueTask<(IReadOnlyList<PythonProject> Projects, string? Error)> ReadProjectsAsync(
        string repositoryRoot,
        IReadOnlyList<string> extra,
        CancellationToken cancellationToken
    )
    {
        List<PythonProject> projects = [];
        List<string> missing = [];

        foreach (string candidate in ProjectCandidates.Concat(extra).Distinct(StringComparer.Ordinal))
        {
            PythonProject? project = await ProjectReader.ReadAsync(
                repositoryRoot: repositoryRoot,
                relativeDirectory: candidate,
                cancellationToken: cancellationToken
            );

            if (project is not null)
            {
                projects.Add(project);
            }
            else if (extra.Contains(value: candidate, comparer: StringComparer.Ordinal))
            {
                missing.Add(candidate);
            }
        }

        return (
            projects,
            missing.Count == 0
                ? null
                : "python.projects names directories with no project: " + string.Join(separator: ", ", values: missing)
        );
    }

    // No project: a repository with Python test files is still reported, with where they are, and the server offers
    // detect for it; one without any offers nothing
    private static PythonDetection WithoutProjects(string repositoryRoot, string? error, IReadOnlyList<string> watched)
    {
        IReadOnlyList<string> testDirectories = TestSuiteScan.Find(repositoryRoot);

        if (testDirectories.Count == 0)
        {
            return NotDetected(repositoryRoot: repositoryRoot, reason: NO_PROJECT, error: error, watched: watched);
        }

        return NotDetected(
            repositoryRoot: repositoryRoot,
            reason: NO_PROJECT + ", but there are Python test files that nothing declares",
            error: error,
            watched: watched
        ) with
        {
            TestSuite = new(testDirectories),
        };
    }

    // The overrides' projects add directories to look in; one that is not a relative path inside the repository is
    // refused without repeating it
    private static (IReadOnlyList<string> Directories, string? Error) OverrideProjects(PythonOverrides? overrides)
    {
        if (overrides?.Projects is not { Count: > 0 } listed)
        {
            return ([], null);
        }

        string[] valid = [.. listed.Where(IsInsideRepository).Distinct(StringComparer.Ordinal)];
        int refused = listed.Count(static directory => !IsInsideRepository(directory));

        return (
            valid,
            refused == 0
                ? null
                : "python.projects must list relative directories inside the repository, so "
                    + refused.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + " of them were ignored"
        );
    }

    // Relative, with no empty, . or .. segment and no backslash, so the path cannot leave the repository
    private static bool IsInsideRepository(string directory)
    {
        return !string.IsNullOrWhiteSpace(directory)
            && !Path.IsPathRooted(directory)
            && !directory.Contains(value: '\\', comparisonType: StringComparison.Ordinal)
            && directory.Split('/').All(static segment => segment.Length != 0 && segment is not "." and not "..");
    }

    // Project names are the package enum's values, so each must be unique and none may be "all"; a project whose
    // name is taken is named by its directory instead, with a number after it if that is taken too
    private static IReadOnlyList<PythonProject> UniqueNames(IReadOnlyList<PythonProject> projects)
    {
        HashSet<string> used = [with(StringComparer.Ordinal), ALL_PROJECTS];
        List<PythonProject> named = [];

        foreach (PythonProject project in projects)
        {
            string name = used.Contains(project.Name) ? project.RelativeDirectory : project.Name;
            int suffix = 1;

            while (used.Contains(name))
            {
                suffix++;
                name =
                    project.RelativeDirectory
                    + "-"
                    + suffix.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            _ = used.Add(name);
            named.Add(project with { Name = name });
        }

        return named;
    }

    private static PythonDetection NotDetected(
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
