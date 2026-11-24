using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.Build.Python.Server.Detection;

// Reads one directory into a project: a pyproject.toml with a [project] table, any requirements*.txt, or the lock
// file of a manager the server does not support (so that it can be reported). Null when the directory holds none
internal static class ProjectReader
{
    private static readonly string[] TestSetNames = ["dev", "test", "tests"];

    public static async ValueTask<PythonProject?> ReadAsync(
        string repositoryRoot,
        string relativeDirectory,
        CancellationToken cancellationToken
    )
    {
        string directory = FullPath(repositoryRoot: repositoryRoot, relativeDirectory: relativeDirectory);

        if (!Directory.Exists(directory))
        {
            return null;
        }

        ProjectFiles files = ProjectFiles.Find(directory);
        (PyprojectManifest? manifest, string? problem) = files.HasPyproject
            ? await PyprojectReader.ReadAsync(
                path: Path.Combine(path1: directory, path2: PyprojectReader.FILE_NAME),
                displayPath: Join(directory: relativeDirectory, file: PyprojectReader.FILE_NAME),
                cancellationToken: cancellationToken
            )
            : (null, null);

        if (!files.IsProject(manifest: manifest, problem: problem))
        {
            return null;
        }

        string name = manifest?.Name ?? relativeDirectory;
        string? unsupported = problem
            ?? Unsupported(files: files, manifest: manifest, relativeDirectory: relativeDirectory);

        if (unsupported is not null)
        {
            return new(Name: name, RelativeDirectory: relativeDirectory, Installer: PythonInstaller.Unsupported)
            {
                Problem = unsupported,
                Manifests = files.Manifests,
                RequirementFiles = files.RequirementFiles,
                HasProjectTable = manifest?.HasProjectTable ?? false,
            };
        }

        return await DescribeAsync(
            directory: directory,
            relativeDirectory: relativeDirectory,
            files: files,
            manifest: manifest,
            cancellationToken: cancellationToken
        );
    }

    private static async ValueTask<PythonProject> DescribeAsync(
        string directory,
        string relativeDirectory,
        ProjectFiles files,
        PyprojectManifest? manifest,
        CancellationToken cancellationToken
    )
    {
        (IReadOnlyList<string> listed, string? problem) = await ReadRequirementsAsync(
            directory: directory,
            relativeDirectory: relativeDirectory,
            files: files.RequirementFiles,
            cancellationToken: cancellationToken
        );
        string name = manifest?.Name ?? relativeDirectory;

        if (problem is not null)
        {
            return new(Name: name, RelativeDirectory: relativeDirectory, Installer: PythonInstaller.Unsupported)
            {
                Problem = problem,
                Manifests = files.Manifests,
                RequirementFiles = files.RequirementFiles,
            };
        }

        string[] extras = TestSets(manifest?.OptionalDependencies);
        string[] groups = TestSets(manifest?.DependencyGroups);
        IReadOnlyList<string> testRequirements = manifest is null
            ? []
            : TestRequirements(manifest: manifest, extras: extras, groups: groups);
        IReadOnlyList<string> dependencies = manifest?.Dependencies ?? [];
        IReadOnlyList<string> names = RequirementName.Names(listed.Concat(dependencies).Concat(testRequirements));

        return new(
            Name: name,
            RelativeDirectory: relativeDirectory,
            Installer: files.HasUvLock ? PythonInstaller.Uv : PythonInstaller.Pip
        )
        {
            Manifests = files.Manifests,
            RequirementFiles = files.RequirementFiles,
            HasProjectTable = manifest?.HasProjectTable ?? false,
            RequiresPython = manifest?.RequiresPython,
            Dependencies = dependencies,
            TestExtras = extras,
            TestGroups = groups,
            TestRequirements = testRequirements,
            DeclaresPytest = RequirementName.DeclaresPytest(names),
            DeclaresCoverage = RequirementName.DeclaresCoverage(names),
        };
    }

    private static async ValueTask<(IReadOnlyList<string> Requirements, string? Problem)> ReadRequirementsAsync(
        string directory,
        string relativeDirectory,
        IReadOnlyList<string> files,
        CancellationToken cancellationToken
    )
    {
        List<string> requirements = [];

        foreach (string file in files)
        {
            (IReadOnlyList<string>? read, string? problem) = await RequirementsReader.ReadAsync(
                path: Path.Combine(path1: directory, path2: file),
                displayPath: Join(directory: relativeDirectory, file: file),
                cancellationToken: cancellationToken
            );

            if (read is null)
            {
                return ([], problem);
            }

            requirements.AddRange(read);
        }

        return (requirements, null);
    }

    private static string? Unsupported(ProjectFiles files, PyprojectManifest? manifest, string relativeDirectory)
    {
        if (files.UnsupportedManifest is { } unsupported)
        {
            return Join(directory: relativeDirectory, file: unsupported.FileName)
                + " means "
                + unsupported.Manager
                + ", which is not supported: only pip and uv are";
        }

        if (manifest is { IsPoetry: true, HasProjectTable: false })
        {
            return Join(directory: relativeDirectory, file: PyprojectReader.FILE_NAME)
                + " describes a poetry project, which is not supported: only pip and uv are";
        }

        return files.HasUvLock && manifest?.HasProjectTable != true
            ? Join(directory: relativeDirectory, file: ManifestFiles.UV_LOCK)
                + " needs a pyproject.toml with a [project] table"
            : null;
    }

    private static IReadOnlyList<string> TestRequirements(PyprojectManifest manifest, string[] extras, string[] groups)
    {
        return
        [
            .. extras
                .SelectMany(extra => manifest.OptionalDependencies[extra])
                .Concat(groups.SelectMany(group => manifest.DependencyGroups[group]))
                .Distinct(StringComparer.Ordinal),
        ];
    }

    // The extras or groups a test run installs: those named dev, test or tests, in that order
    private static string[] TestSets(IReadOnlyDictionary<string, IReadOnlyList<string>>? sets)
    {
        return sets is null ? [] : [.. TestSetNames.Where(sets.ContainsKey)];
    }

    public static string FullPath(string repositoryRoot, string relativeDirectory)
    {
        return StringComparer.Ordinal.Equals(x: relativeDirectory, y: PythonProject.ROOT_DIRECTORY)
            ? repositoryRoot
            : Path.Combine(path1: repositoryRoot, path2: relativeDirectory);
    }

    // Joins a repository-relative directory and a file name with a forward slash; "." is the repository root
    public static string Join(string directory, string file)
    {
        return StringComparer.Ordinal.Equals(x: directory, y: PythonProject.ROOT_DIRECTORY)
            ? file
            : directory + "/" + file;
    }
}
