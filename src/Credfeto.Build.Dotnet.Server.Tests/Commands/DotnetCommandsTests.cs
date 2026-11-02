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
