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
    private static readonly DotnetRepository Repository = TestRepositories.Create(root: "/workspace/repo", TestRepositories.CoreTests);

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
        Assert.Equal(
            expected: ["test", "Example.Core.Tests/Example.Core.Tests.csproj", "-c", "Release", "-p:SolutionDir=/workspace/repo/src/"],
            actual: step.Arguments,
            comparer: StringComparer.Ordinal
        );
    }

    [Fact]
    public static void BuildAndRestoreTargetTheSolution()
    {
        Assert.Equal(expected: ["build", "Example.slnx", "-c", "Release"], actual: DotnetCommands.Build(Repository).Arguments, comparer: StringComparer.Ordinal);
        Assert.Equal(expected: ["restore", "Example.slnx"], actual: DotnetCommands.Restore(Repository).Arguments, comparer: StringComparer.Ordinal);
    }

    [Fact]
    public static void TestOfTheWholeSolutionIsOneMicrosoftTestingPlatformRun()
    {
        ProcessStep step = DotnetCommands.TestSolution(Repository);

        Assert.Equal(expected: "/workspace/repo/src", actual: step.WorkingDirectory);
        Assert.Equal(expected: ["test", "--solution", "Example.slnx", "-c", "Release"], actual: step.Arguments, comparer: StringComparer.Ordinal);
    }

    [Fact]
    public static void QualityToolsRunFromTheRepositoryRootWithFixedArguments()
    {
        Assert.Equal(expected: ["tool", "restore"], actual: DotnetCommands.ToolRestore(Repository).Arguments, comparer: StringComparer.Ordinal);
        Assert.Equal(
            expected: ["buildcheck", "-Solution", "/workspace/repo/src/Example.slnx", "-WarningAsErrors", "true", "-PreReleaseBuild", "true"],
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
            actual: DotnetCommands.Cleanup(repository: Repository, name: "cscleanup", files: ["/workspace/repo/src/A.cs", "/workspace/repo/src/B.cs"]).Arguments,
            comparer: StringComparer.Ordinal
        );

        ProcessStep buildtest = DotnetCommands.Buildtest(repository: Repository, script: "/usr/local/bin/buildtest");
        Assert.Equal(expected: "/usr/local/bin/buildtest", actual: buildtest.FileName);
        Assert.Empty(buildtest.Arguments);

        foreach (ProcessStep step in new[] { DotnetCommands.ToolRestore(Repository), DotnetCommands.BuildCheck(repository: Repository, preRelease: false), DotnetCommands.ChangelogLint(Repository), buildtest })
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
        Assert.Equal(expected: expected, actual: DotnetCommands.AssemblyUnderTest(new(Name: project, RelativePath: project + ".csproj", Kind: DotnetProjectKind.UnitTests)));
    }
}
