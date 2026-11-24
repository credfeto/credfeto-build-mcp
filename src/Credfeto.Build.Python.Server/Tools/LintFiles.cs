using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Repository;
using Credfeto.Build.Python.Server.Detection;

namespace Credfeto.Build.Python.Server.Tools;

// The Python files a lint run checks, repository-relative: every tracked *.py (scope all), or only those changed
// against HEAD plus new untracked ones (scope changed), as the pre-commit hook checks staged files and super-linter
// changed ones. Each file belongs to the project with the deepest directory that holds it, so nested projects are
// not linted twice
internal static class LintFiles
{
    public const string SCOPE_ALL = "all";
    public const string SCOPE_CHANGED = "changed";

    private const string PYTHON_FILES = ":(glob)**/*.py";

    public static async ValueTask<IReadOnlyList<string>> FindAsync(
        string repositoryRoot,
        string scope,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyList<string> files = StringComparer.Ordinal.Equals(x: scope, y: SCOPE_CHANGED)
            ?
            [
                .. await GitFilesAsync(
                    repositoryRoot: repositoryRoot,
                    arguments: ["diff", "--name-only", "-z", "--diff-filter=d", "HEAD", "--", PYTHON_FILES],
                    cancellationToken: cancellationToken
                ),
                .. await GitFilesAsync(
                    repositoryRoot: repositoryRoot,
                    arguments: ["ls-files", "-z", "--others", "--exclude-standard", "--", PYTHON_FILES],
                    cancellationToken: cancellationToken
                ),
            ]
            : await GitFilesAsync(
                repositoryRoot: repositoryRoot,
                arguments: ["ls-files", "-z", "--", PYTHON_FILES],
                cancellationToken: cancellationToken
            );

        return [.. files.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
    }

    // The files that belong to the project: under its directory and not under a deeper project's
    public static IReadOnlyList<string> For(
        PythonProject project,
        IReadOnlyList<PythonProject> allProjects,
        IReadOnlyList<string> files
    )
    {
        return
        [
            .. files.Where(file => IsSameProject(owner: Owner(file: file, projects: allProjects), project: project)),
        ];
    }

    private static bool IsSameProject(PythonProject? owner, PythonProject project)
    {
        return owner is not null
            && StringComparer.Ordinal.Equals(x: owner.RelativeDirectory, y: project.RelativeDirectory);
    }

    private static PythonProject? Owner(string file, IReadOnlyList<PythonProject> projects)
    {
        return projects
            .Where(project => IsUnder(file: file, directory: project.RelativeDirectory))
            .MaxBy(static project =>
                StringComparer.Ordinal.Equals(x: project.RelativeDirectory, y: PythonProject.ROOT_DIRECTORY)
                    ? 0
                    : project.RelativeDirectory.Length
            );
    }

    private static bool IsUnder(string file, string directory)
    {
        return StringComparer.Ordinal.Equals(x: directory, y: PythonProject.ROOT_DIRECTORY)
            || file.StartsWith(value: directory + "/", comparisonType: StringComparison.Ordinal);
    }

    private static async ValueTask<IReadOnlyList<string>> GitFilesAsync(
        string repositoryRoot,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken
    )
    {
        string? output = await GitCommand.TryRunAsync(
            repositoryRoot: repositoryRoot,
            arguments: arguments,
            cancellationToken: cancellationToken
        );

        return
        [
            .. GitCommand
                .SplitNul(output)
                .Where(file => file[0] != '-' && File.Exists(Path.Combine(path1: repositoryRoot, path2: file))),
        ];
    }
}
