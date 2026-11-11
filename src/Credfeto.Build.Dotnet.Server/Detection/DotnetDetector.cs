using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Credfeto.Build.Core.Overrides;
using Credfeto.Build.Core.Repository;

namespace Credfeto.Build.Dotnet.Server.Detection;

public static partial class DotnetDetector
{
    public const string UNIT_TESTS_SUFFIX = ".Tests";
    public const string INTEGRATION_TESTS_SUFFIX = ".Integration.Tests";
    public const string BENCHMARK_TESTS_SUFFIX = ".Benchmark.Tests";

    private const int DEFAULT_TIMEOUT_MINUTES = 30;
    private const int MAX_TIMEOUT_MINUTES = 240;
    private const int MAX_PROJECT_FILE_LENGTH = 1024 * 1024;

    private static readonly string[] SolutionPatterns = ["*.slnx", "*.sln"];

    public static async ValueTask<DotnetDetection> DetectAsync(
        string repositoryRoot,
        CancellationToken cancellationToken
    )
    {
        OverridesLoadResult loaded = await OverridesLoader.LoadAsync(
            repositoryRoot: repositoryRoot,
            cancellationToken: cancellationToken
        );
        LanguageOverrides? overrides = loaded.Overrides.Dotnet;

        if (overrides?.Enabled == false)
        {
            return NotDetected(
                repositoryRoot: repositoryRoot,
                reason: "Disabled by " + OverridesLoader.RELATIVE_PATH,
                loaded: loaded
            );
        }

        if (!Directory.Exists(repositoryRoot))
        {
            return NotDetected(
                repositoryRoot: repositoryRoot,
                reason: "The repository root does not exist",
                loaded: loaded
            );
        }

        (string? solution, string? overrideError) = FindSolution(repositoryRoot: repositoryRoot, overrides: overrides);
        string? error = CombineErrors(first: loaded.Error, second: overrideError);

        if (solution is null)
        {
            return new(
                RepositoryRoot: repositoryRoot,
                Repository: null,
                Reason: "No .slnx or .sln under src/ or the repository root",
                OverridesError: error
            );
        }

        return await TryDescribeAsync(
            repositoryRoot: repositoryRoot,
            solution: solution,
            overrides: overrides,
            overridesError: error,
            cancellationToken: cancellationToken
        );
    }

    private static async ValueTask<DotnetDetection> TryDescribeAsync(
        string repositoryRoot,
        string solution,
        LanguageOverrides? overrides,
        string? overridesError,
        CancellationToken cancellationToken
    )
    {
        try
        {
            DotnetRepository repository = await DescribeAsync(
                repositoryRoot: repositoryRoot,
                solutionPath: solution,
                overrides: overrides,
                cancellationToken: cancellationToken
            );

            return new(
                RepositoryRoot: repositoryRoot,
                Repository: repository,
                Reason: "Found " + RepositoryPaths.RelativeTo(root: repositoryRoot, path: solution),
                OverridesError: overridesError
            );
        }
        catch (Exception exception) when (exception is XmlException or IOException or UnauthorizedAccessException)
        {
            return new(
                RepositoryRoot: repositoryRoot,
                Repository: null,
                Reason: "The solution could not be read",
                OverridesError: overridesError
            );
        }
    }

    private static DotnetDetection NotDetected(string repositoryRoot, string reason, OverridesLoadResult loaded)
    {
        return new(RepositoryRoot: repositoryRoot, Repository: null, Reason: reason, OverridesError: loaded.Error);
    }

    private static (string? Solution, string? Error) FindSolution(string repositoryRoot, LanguageOverrides? overrides)
    {
        if (!string.IsNullOrWhiteSpace(overrides?.Solution))
        {
            string chosen = Path.GetFullPath(path: overrides.Solution, basePath: repositoryRoot);

            if (
                IsSolutionFile(chosen)
                && RepositoryPaths.IsWithin(root: repositoryRoot, path: chosen)
                && File.Exists(chosen)
            )
            {
                return (chosen, null);
            }

            return (
                Search(repositoryRoot),
                "The solution in "
                    + OverridesLoader.RELATIVE_PATH
                    + " is not a .slnx or .sln file inside the repository and was ignored"
            );
        }

        return (Search(repositoryRoot), null);
    }

    private static string? Search(string repositoryRoot)
    {
        string[] directories = [Path.Combine(path1: repositoryRoot, path2: "src"), repositoryRoot];

        foreach (string directory in directories.Where(Directory.Exists))
        {
            foreach (string pattern in SolutionPatterns)
            {
                string? found = Directory
                    .EnumerateFiles(
                        path: directory,
                        searchPattern: pattern,
                        searchOption: SearchOption.TopDirectoryOnly
                    )
                    .Where(IsSolutionFile)
                    .Order(StringComparer.Ordinal)
                    .FirstOrDefault();

                if (found is not null)
                {
                    return found;
                }
            }
        }

        return null;
    }

    private static bool IsSolutionFile(string path)
    {
        string extension = Path.GetExtension(path);

        return StringComparer.OrdinalIgnoreCase.Equals(x: extension, y: ".slnx")
            || StringComparer.OrdinalIgnoreCase.Equals(x: extension, y: ".sln");
    }

    private static async ValueTask<DotnetRepository> DescribeAsync(
        string repositoryRoot,
        string solutionPath,
        LanguageOverrides? overrides,
        CancellationToken cancellationToken
    )
    {
        string solutionDirectory = Path.GetDirectoryName(solutionPath) ?? repositoryRoot;
        HashSet<string> excluded = new(
            collection: overrides?.ExcludeTestProjects ?? [],
            comparer: StringComparer.Ordinal
        );
        List<DotnetProject> found = [];

        foreach (
            string relative in await SolutionReader.ReadProjectPathsAsync(
                solutionPath: solutionPath,
                cancellationToken: cancellationToken
            )
        )
        {
            DotnetProject? project = await ClassifyAsync(
                solutionDirectory: solutionDirectory,
                relativePath: relative,
                cancellationToken: cancellationToken
            );

            if (project is not null)
            {
                found.Add(project);
            }
        }

        DotnetProject[] projects =
        [
            .. found.OrderBy(keySelector: static project => project.Name, comparer: StringComparer.Ordinal),
        ];

        DotnetProject[] unitTests =
        [
            .. projects.Where(project =>
                project.Kind == DotnetProjectKind.UnitTests && !excluded.Contains(project.Name)
            ),
        ];

        return new(
            RepositoryRoot: repositoryRoot,
            SolutionPath: solutionPath,
            SolutionDirectory: solutionDirectory,
            Projects: projects,
            UnitTestProjects: unitTests,
            Timeout: TimeSpan.FromMinutes(
                Math.Clamp(
                    value: overrides?.TimeoutMinutes ?? DEFAULT_TIMEOUT_MINUTES,
                    min: 1,
                    max: MAX_TIMEOUT_MINUTES
                )
            )
        );
    }

    private static async ValueTask<DotnetProject?> ClassifyAsync(
        string solutionDirectory,
        string relativePath,
        CancellationToken cancellationToken
    )
    {
        string fullPath = Path.GetFullPath(path: relativePath, basePath: solutionDirectory);

        if (!File.Exists(fullPath))
        {
            return null;
        }

        string name = Path.GetFileNameWithoutExtension(fullPath);
        string relative = RepositoryPaths.RelativeTo(root: solutionDirectory, path: fullPath);

        DotnetProjectKind kind = KindOf(name);

        if (
            kind == DotnetProjectKind.UnitTests
            && await OptsOutOfTestingAsync(fullPath: fullPath, cancellationToken: cancellationToken)
        )
        {
            kind = DotnetProjectKind.Project;
        }

        return new(Name: name, RelativePath: relative, Kind: kind);
    }

    public static DotnetProjectKind KindOf(string name)
    {
        if (name.EndsWith(value: INTEGRATION_TESTS_SUFFIX, comparisonType: StringComparison.Ordinal))
        {
            return DotnetProjectKind.IntegrationTests;
        }

        if (name.EndsWith(value: BENCHMARK_TESTS_SUFFIX, comparisonType: StringComparison.Ordinal))
        {
            return DotnetProjectKind.BenchmarkTests;
        }

        return name.EndsWith(value: UNIT_TESTS_SUFFIX, comparisonType: StringComparison.Ordinal)
            ? DotnetProjectKind.UnitTests
            : DotnetProjectKind.Project;
    }

    private static async ValueTask<bool> OptsOutOfTestingAsync(string fullPath, CancellationToken cancellationToken)
    {
        FileInfo file = new(fullPath);

        if (file.Length > MAX_PROJECT_FILE_LENGTH)
        {
            return true;
        }

        // A test support library declares itself not runnable; dotnet test must never target it
        string content = await File.ReadAllTextAsync(path: fullPath, cancellationToken: cancellationToken);

        return TestingOptOut().IsMatch(content);
    }

    private static string? CombineErrors(string? first, string? second)
    {
        return (first, second) switch
        {
            (null, _) => second,
            (_, null) => first,
            _ => first + "; " + second,
        };
    }

    [GeneratedRegex(
        pattern: @"<(?<tag>IsTestProject|IsTestingPlatformApplication)>\s*false\s*</\k<tag>>",
        options: RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex TestingOptOut();
}
