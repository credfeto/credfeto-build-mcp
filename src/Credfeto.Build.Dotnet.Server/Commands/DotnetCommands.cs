using System;
using System.Collections.Generic;
using System.IO;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Dotnet.Server.Detection;

namespace Credfeto.Build.Dotnet.Server.Commands;

public static class DotnetCommands
{
    public const string DOTNET = "dotnet";
    public const string CONFIGURATION = "Release";
    public const string COVERAGE_DIRECTORY = "coverage";
    public const string COVERAGE_FILE_SUFFIX = ".coverage.cobertura.xml";
    public const string CHANGELOG_FILE = "CHANGELOG.md";

    // The MSBuild properties pre-commit buildtest passes to both its build and its test step; keep them in step
    public static readonly IReadOnlyList<string> BuildProperties =
    [
        "-p:ApiCompatGenerateSuppressionFile=true",
        "-p:ContinuousIntegrationBuild=true",
        "-p:IsProduction=false",
        "-p:Optimize=true",
        "-p:SuppressNETCoreSdkPreviewMessage=true",
        "-p:Version=0.0.0.1-test",
    ];

    // The Microsoft.Testing.Platform options buildtest passes to dotnet test; exit code 8 is "no tests ran"
    public static readonly IReadOnlyList<string> TestRunOptions =
    [
        "--long-running",
        "10",
        "--parallel-algorithm",
        "aggressive",
        "--ignore-exit-code",
        "8",
    ];

    // The namespaces buildtest leaves out of its solution-wide run; such projects only run when asked for by name
    public static readonly IReadOnlyList<string> ExcludedTestNamespaces =
    [
        "*.Integration.Tests",
        "*.Integration.Tests.*",
        "*.Benchmark.Tests",
        "*.Benchmark.Tests.*",
    ];

    private static readonly IReadOnlyDictionary<string, string> Environment = new Dictionary<string, string>(
        StringComparer.Ordinal
    )
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
        return Prerequisite(
            Step(name: "restore", repository: repository, arguments: ["restore", SolutionFileName(repository)])
        );
    }

    // The whole solution, from the solution directory, as buildtest builds it; restore runs first
    public static ProcessStep Build(DotnetRepository repository)
    {
        return Prerequisite(
            Step(
                name: "build",
                repository: repository,
                arguments:
                [
                    "build",
                    SolutionFileName(repository),
                    "-c",
                    CONFIGURATION,
                    "--no-restore",
                    .. BuildProperties,
                    "-nodeReuse:False",
                ]
            )
        );
    }

    // One project, built with the properties Test passes, so Test can run it with --no-build
    public static ProcessStep BuildProject(DotnetRepository repository, DotnetProject project)
    {
        return Prerequisite(
            Step(
                name: "build " + project.Name,
                repository: repository,
                arguments:
                [
                    "build",
                    project.RelativePath,
                    "-c",
                    CONFIGURATION,
                    "--no-restore",
                    .. BuildProperties,
                    SolutionDirProperty(repository),
                    "-nodeReuse:False",
                ]
            )
        );
    }

    // One Microsoft.Testing.Platform run over the solution's tests as pre-commit buildtest runs it: integration
    // and benchmark namespaces are excluded, and the binaries come from the Build step just before it
    public static ProcessStep TestSolution(DotnetRepository repository)
    {
        List<string> arguments =
        [
            "test",
            "--solution",
            SolutionFileName(repository),
            "-c",
            CONFIGURATION,
            "--no-restore",
            "--no-build",
            .. TestRunOptions,
        ];

        foreach (string excluded in ExcludedTestNamespaces)
        {
            arguments.AddRange(["--filter-not-namespace", excluded]);
        }

        arguments.AddRange(BuildProperties);

        return Step(name: "test", repository: repository, arguments: arguments);
    }

    // One project, with no namespace filter so a project asked for by name always runs; BuildProject runs first
    public static ProcessStep Test(DotnetRepository repository, DotnetProject project)
    {
        return Step(
            name: "test " + project.Name,
            repository: repository,
            arguments:
            [
                "test",
                project.RelativePath,
                "-c",
                CONFIGURATION,
                "--no-restore",
                "--no-build",
                .. TestRunOptions,
                .. BuildProperties,
                SolutionDirProperty(repository),
            ]
        );
    }

    public static ProcessStep ToolRestore(DotnetRepository repository)
    {
        return RootStep(name: "tool restore", repository: repository, fileName: DOTNET, arguments: ["tool", "restore"]);
    }

    public static ProcessStep BuildCheck(DotnetRepository repository, bool preRelease)
    {
        return RootStep(
            name: "buildcheck",
            repository: repository,
            fileName: DOTNET,
            arguments:
            [
                "buildcheck",
                "-Solution",
                repository.SolutionPath,
                "-WarningAsErrors",
                "true",
                "-PreReleaseBuild",
                preRelease ? "true" : "false",
            ]
        );
    }

    public static ProcessStep Cleanup(DotnetRepository repository, string name, IReadOnlyList<string> files)
    {
        return RootStep(name: name, repository: repository, fileName: DOTNET, arguments: ["cscleanup", .. files]);
    }

    public static ProcessStep ChangelogLint(DotnetRepository repository)
    {
        return RootStep(
            name: "changelog lint",
            repository: repository,
            fileName: DOTNET,
            arguments: ["changelog", "--changelog", ChangelogPath(repository), "--lint"]
        );
    }

    public static ProcessStep Buildtest(DotnetRepository repository, string script)
    {
        return RootStep(name: "buildtest", repository: repository, fileName: script, arguments: []);
    }

    public static string ChangelogPath(DotnetRepository repository)
    {
        return Path.Combine(path1: repository.RepositoryRoot, path2: CHANGELOG_FILE);
    }

    public static ProcessStep Coverage(DotnetRepository repository, DotnetProject project)
    {
        List<string> arguments = TestArguments(repository: repository, project: project);
        arguments.AddRange([
            "--",
            "--coverage",
            "--coverage-output-format",
            "cobertura",
            "--coverage-output",
            CoverageFile(repository: repository, project: project),
        ]);

        return Step(name: "coverage " + project.Name, repository: repository, arguments: arguments);
    }

    public static string CoverageDirectory(DotnetRepository repository)
    {
        return Path.Combine(path1: repository.RepositoryRoot, path2: COVERAGE_DIRECTORY);
    }

    public static string CoverageFile(DotnetRepository repository, DotnetProject project)
    {
        return Path.Combine(
            path1: CoverageDirectory(repository),
            path2: AssemblyUnderTest(project) + COVERAGE_FILE_SUFFIX
        );
    }

    public static string AssemblyUnderTest(DotnetProject project)
    {
        return project.Name.EndsWith(value: DotnetDetector.UNIT_TESTS_SUFFIX, comparisonType: StringComparison.Ordinal)
            ? project.Name[..^DotnetDetector.UNIT_TESTS_SUFFIX.Length]
            : project.Name;
    }

    // Coverage builds its own binaries: ContinuousIntegrationBuild would rewrite the PDB source paths it maps
    private static List<string> TestArguments(DotnetRepository repository, DotnetProject project)
    {
        return ["test", project.RelativePath, "-c", CONFIGURATION, SolutionDirProperty(repository)];
    }

    private static string SolutionDirProperty(DotnetRepository repository)
    {
        return "-p:SolutionDir=" + SolutionDirectoryWithSlash(repository);
    }

    private static ProcessStep Prerequisite(ProcessStep step)
    {
        return step with { StopJobOnFailure = true };
    }

    private static string SolutionDirectoryWithSlash(DotnetRepository repository)
    {
        return Path.TrimEndingDirectorySeparator(repository.SolutionDirectory) + "/";
    }

    private static string SolutionFileName(DotnetRepository repository)
    {
        return Path.GetFileName(repository.SolutionPath);
    }

    private static ProcessStep RootStep(
        string name,
        DotnetRepository repository,
        string fileName,
        IReadOnlyList<string> arguments
    )
    {
        return new(
            Name: name,
            FileName: fileName,
            Arguments: arguments,
            WorkingDirectory: repository.RepositoryRoot,
            Environment: Environment
        );
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
