using System;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Dotnet.Server.Commands;
using Credfeto.Build.Dotnet.Server.Detection;
using Credfeto.Build.Dotnet.Server.Tests.Helpers;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Build.Dotnet.Server.Tests.Commands;

public sealed class DotnetCommandsTests : TestBase
{
    // What pre-commit buildtest passes today (credfeto-global-pre-commit src/scripts/buildtest); change both together
    private static readonly string[] BuildtestProperties =
    [
        "-p:ApiCompatGenerateSuppressionFile=true",
        "-p:ContinuousIntegrationBuild=true",
        "-p:IsProduction=false",
        "-p:Optimize=true",
        "-p:SuppressNETCoreSdkPreviewMessage=true",
        "-p:Version=0.0.0.1-test",
    ];

    private static readonly string[] BuildtestTestRunOptions =
    [
        "--long-running",
        "10",
        "--parallel-algorithm",
        "aggressive",
        "--ignore-exit-code",
        "8",
    ];

    private static readonly DotnetRepository Repository = TestRepositories.Create(
        root: "/workspace/repo",
        TestRepositories.CoreTests
    );

    [Fact]
    public static void CoverageUsesTheDocumentedMicrosoftTestingPlatformForm()
    {
        ProcessStep step = DotnetCommands.Coverage(repository: Repository, project: TestRepositories.CoreTests);

        Assert.Equal(expected: "dotnet", actual: step.FileName);
        Assert.Equal(expected: "/workspace/repo/src", actual: step.WorkingDirectory);
        Assert.Equal(
            expected:
            [
                "test",
                "Example.Core.Tests/Example.Core.Tests.csproj",
                "-c",
                "Release",
                "-p:SolutionDir=/workspace/repo/src/",
                "--",
                "--coverage",
                "--coverage-output-format",
                "cobertura",
                "--coverage-output",
                "/workspace/repo/coverage/Example.Core.coverage.cobertura.xml",
            ],
            actual: step.Arguments,
            comparer: StringComparer.Ordinal
        );
    }

    [Fact]
    public static void TestRunsOneProjectByRelativePathFromTheSolutionDirectory()
    {
        ProcessStep step = DotnetCommands.Test(repository: Repository, project: TestRepositories.CoreTests);

        Assert.Equal(expected: "/workspace/repo/src", actual: step.WorkingDirectory);
        Assert.False(condition: step.StopJobOnFailure, userMessage: "a test step is not a prerequisite");
        Assert.Equal(
            expected:
            [
                "test",
                "Example.Core.Tests/Example.Core.Tests.csproj",
                "-c",
                "Release",
                "--no-restore",
                "--no-build",
                .. BuildtestTestRunOptions,
                .. BuildtestProperties,
                "-p:SolutionDir=/workspace/repo/src/",
            ],
            actual: step.Arguments,
            comparer: StringComparer.Ordinal
        );
    }

    [Fact]
    public static void BuildOfOneProjectUsesTheSamePropertiesAsItsTestRun()
    {
        ProcessStep step = DotnetCommands.BuildProject(repository: Repository, project: TestRepositories.CoreTests);

        Assert.Equal(expected: "/workspace/repo/src", actual: step.WorkingDirectory);
        Assert.True(condition: step.StopJobOnFailure, userMessage: "a failed build must stop the job");
        Assert.Equal(
            expected:
            [
                "build",
                "Example.Core.Tests/Example.Core.Tests.csproj",
                "-c",
                "Release",
                "--no-restore",
                .. BuildtestProperties,
                "-p:SolutionDir=/workspace/repo/src/",
                "-nodeReuse:False",
            ],
            actual: step.Arguments,
            comparer: StringComparer.Ordinal
        );
    }

    [Fact]
    public static void BuildAndRestoreTargetTheSolution()
    {
        ProcessStep build = DotnetCommands.Build(Repository);
        ProcessStep restore = DotnetCommands.Restore(Repository);

        Assert.Equal(
            expected:
            [
                "build",
                "Example.slnx",
                "-c",
                "Release",
                "--no-restore",
                .. BuildtestProperties,
                "-nodeReuse:False",
            ],
            actual: build.Arguments,
            comparer: StringComparer.Ordinal
        );
        Assert.Equal(
            expected: ["restore", "Example.slnx"],
            actual: restore.Arguments,
            comparer: StringComparer.Ordinal
        );
        Assert.True(condition: build.StopJobOnFailure, userMessage: "a failed build must stop the job");
        Assert.True(condition: restore.StopJobOnFailure, userMessage: "a failed restore must stop the job");
    }

    [Fact]
    public static void TestOfTheWholeSolutionRunsAsBuildtestRunsIt()
    {
        ProcessStep step = DotnetCommands.TestSolution(Repository);

        Assert.Equal(expected: "/workspace/repo/src", actual: step.WorkingDirectory);
        Assert.Equal(
            expected:
            [
                "test",
                "--solution",
                "Example.slnx",
                "-c",
                "Release",
                "--no-restore",
                "--no-build",
                .. BuildtestTestRunOptions,
                "--filter-not-namespace",
                "*.Integration.Tests",
                "--filter-not-namespace",
                "*.Integration.Tests.*",
                "--filter-not-namespace",
                "*.Benchmark.Tests",
                "--filter-not-namespace",
                "*.Benchmark.Tests.*",
                .. BuildtestProperties,
            ],
            actual: step.Arguments,
            comparer: StringComparer.Ordinal
        );
    }

    [Fact]
    public static void SharedArgumentsMatchPreCommitBuildtest()
    {
        Assert.Equal(
            expected: BuildtestProperties,
            actual: DotnetCommands.BuildProperties,
            comparer: StringComparer.Ordinal
        );
        Assert.Equal(
            expected: BuildtestTestRunOptions,
            actual: DotnetCommands.TestRunOptions,
            comparer: StringComparer.Ordinal
        );

        // Exactly the namespaces buildtest filters, and no others
        Assert.Equal(
            expected: ["*.Integration.Tests", "*.Integration.Tests.*", "*.Benchmark.Tests", "*.Benchmark.Tests.*"],
            actual: DotnetCommands.ExcludedTestNamespaces,
            comparer: StringComparer.Ordinal
        );
    }

    [Fact]
    public static void QualityToolsRunFromTheRepositoryRootWithFixedArguments()
    {
        Assert.Equal(
            expected: ["tool", "restore"],
            actual: DotnetCommands.ToolRestore(Repository).Arguments,
            comparer: StringComparer.Ordinal
        );
        Assert.Equal(
            expected:
            [
                "buildcheck",
                "-Solution",
                "/workspace/repo/src/Example.slnx",
                "-WarningAsErrors",
                "true",
                "-PreReleaseBuild",
                "true",
            ],
            actual: DotnetCommands.BuildCheck(repository: Repository, preRelease: true).Arguments,
            comparer: StringComparer.Ordinal
        );
        Assert.Equal(
            expected: ["changelog", "--changelog", "/workspace/repo/CHANGELOG.md", "--lint"],
            actual: DotnetCommands.ChangelogLint(Repository).Arguments,
            comparer: StringComparer.Ordinal
        );
        Assert.Equal(
            expected: ["cscleanup", "/workspace/repo/src/A.cs", "/workspace/repo/src/B.cs"],
            actual: DotnetCommands
                .Cleanup(
                    repository: Repository,
                    name: "cscleanup",
                    files: ["/workspace/repo/src/A.cs", "/workspace/repo/src/B.cs"]
                )
                .Arguments,
            comparer: StringComparer.Ordinal
        );

        ProcessStep buildtest = DotnetCommands.Buildtest(repository: Repository, script: "/usr/local/bin/buildtest");
        Assert.Equal(expected: "/usr/local/bin/buildtest", actual: buildtest.FileName);
        Assert.Empty(buildtest.Arguments);

        foreach (
            ProcessStep step in new[]
            {
                DotnetCommands.ToolRestore(Repository),
                DotnetCommands.BuildCheck(repository: Repository, preRelease: false),
                DotnetCommands.ChangelogLint(Repository),
                buildtest,
            }
        )
        {
            Assert.Equal(expected: "/workspace/repo", actual: step.WorkingDirectory);
        }
    }

    [Fact]
    public static void BuildNodesDoNotOutliveTheStep()
    {
        ProcessStep step = DotnetCommands.Build(Repository);

        Assert.Equal(expected: "1", actual: step.Environment["MSBUILDDISABLENODEREUSE"]);
        Assert.Equal(expected: "0", actual: step.Environment["DOTNET_CLI_USE_MSBUILD_SERVER"]);
    }

    [Theory]
    [InlineData("Example.Core.Tests", "Example.Core")]
    [InlineData("Example.Tests", "Example")]
    public static void AssemblyUnderTestDropsTheTestsSuffix(string project, string expected)
    {
        Assert.Equal(
            expected: expected,
            actual: DotnetCommands.AssemblyUnderTest(
                new(Name: project, RelativePath: project + ".csproj", Kind: DotnetProjectKind.UnitTests)
            )
        );
    }
}
