using System;
using System.Collections.Generic;
using System.IO;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Dotnet.Server.Detection;

namespace Credfeto.Build.Dotnet.Server.Commands;

/// <summary>
///     The fixed argument vectors for every dotnet command the server runs. Nothing here comes from a tool
///     argument except the choice of a detected project.
/// </summary>
public static class DotnetCommands
{
    public const string DOTNET = "dotnet";
    public const string CONFIGURATION = "Release";
    public const string COVERAGE_DIRECTORY = "coverage";
    public const string COVERAGE_FILE_SUFFIX = ".coverage.cobertura.xml";

    private static readonly IReadOnlyDictionary<string, string> Environment = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["DOTNET_NOLOGO"] = "1",
        ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
        ["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1",

        // No build node or MSBuild server outlives the step and keeps its output pipes open
        ["MSBUILDDISABLENODEREUSE"] = "1",
        ["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0",
        ["NO_COLOR"] = "1",
    };

    public static ProcessStep Restore(DotnetRepository repository)
    {
        return Step(name: "restore", repository: repository, arguments: ["restore", SolutionFileName(repository)]);
    }

    public static ProcessStep Build(DotnetRepository repository)
    {
        return Step(name: "build", repository: repository, arguments: ["build", SolutionFileName(repository), "-c", CONFIGURATION]);
    }

    public static ProcessStep Test(DotnetRepository repository, DotnetProject project)
    {
        return Step(name: "test " + project.Name, repository: repository, arguments: TestArguments(repository: repository, project: project));
    }

    /// <summary>
    ///     The documented Microsoft.Testing.Platform coverage form: run from the solution directory with a relative
    ///     project path (an absolute one triggers the legacy VSTest bridge), coverage flags after <c>--</c>.
    /// </summary>
    public static ProcessStep Coverage(DotnetRepository repository, DotnetProject project)
    {
        List<string> arguments = TestArguments(repository: repository, project: project);
        arguments.AddRange(
            [
                "--",
                "--coverage",
                "--coverage-output-format",
                "cobertura",
                "--coverage-output",
                CoverageFile(repository: repository, project: project),
            ]
        );

        return Step(name: "coverage " + project.Name, repository: repository, arguments: arguments);
    }

    public static string CoverageDirectory(DotnetRepository repository)
    {
        return Path.Combine(path1: repository.RepositoryRoot, path2: COVERAGE_DIRECTORY);
    }

    public static string CoverageFile(DotnetRepository repository, DotnetProject project)
    {
        return Path.Combine(path1: CoverageDirectory(repository), path2: AssemblyUnderTest(project) + COVERAGE_FILE_SUFFIX);
    }

    public static string AssemblyUnderTest(DotnetProject project)
    {
        return project.Name.EndsWith(value: DotnetDetector.UNIT_TESTS_SUFFIX, comparisonType: StringComparison.Ordinal)
            ? project.Name[..^DotnetDetector.UNIT_TESTS_SUFFIX.Length]
            : project.Name;
    }

    private static List<string> TestArguments(DotnetRepository repository, DotnetProject project)
    {
        return ["test", project.RelativePath, "-c", CONFIGURATION, "-p:SolutionDir=" + SolutionDirectoryWithSlash(repository)];
    }

    private static string SolutionDirectoryWithSlash(DotnetRepository repository)
    {
        return Path.TrimEndingDirectorySeparator(repository.SolutionDirectory) + "/";
    }

    private static string SolutionFileName(DotnetRepository repository)
    {
        return Path.GetFileName(repository.SolutionPath);
    }

    private static ProcessStep Step(string name, DotnetRepository repository, IReadOnlyList<string> arguments)
    {
        return new(
            Name: name,
            FileName: DOTNET,
            Arguments: arguments,
            WorkingDirectory: repository.SolutionDirectory,
            Environment: Environment
        );
    }
}
