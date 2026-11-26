using System;
using Credfeto.Build.Node.Server.Commands;
using Credfeto.Build.Node.Server.Detection;
using Credfeto.Build.Node.Server.Tools;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Build.Node.Server.Tests.Tools;

public sealed class TestFilterTests : TestBase
{
    [Fact]
    public static void WithNeitherPathsNorCoverageTheRunIsUnchanged()
    {
        ScriptRun run = Run(NodeTestRunner.Jest) with { PathFilter = "packages/web" };

        Assert.Same(expected: run, actual: TestFilter.Apply(run: run, paths: [], coverage: false));
    }

    [Theory]
    [InlineData(NodeTestRunner.Jest, "--coverage")]
    [InlineData(NodeTestRunner.Vitest, "--coverage")]
    [InlineData(NodeTestRunner.NodeTest, "--experimental-test-coverage")]
    public static void CoverageComesBeforeThePaths(NodeTestRunner runner, string coverageSwitch)
    {
        ScriptRun run = TestFilter.Apply(run: Run(runner), paths: ["src/a.test.ts", "test/*.ts"], coverage: true);

        Assert.Equal(
            expected: [coverageSwitch, "src/a.test.ts", "test/*.ts"],
            actual: run.RunnerArguments,
            comparer: StringComparer.Ordinal
        );
    }

    [Fact]
    public static void AnUnknownRunnerGetsThePathsButNoCoverageSwitch()
    {
        ScriptRun run = TestFilter.Apply(run: Run(NodeTestRunner.Unknown), paths: ["a.test.js"], coverage: true);

        Assert.Equal(expected: ["a.test.js"], actual: run.RunnerArguments, comparer: StringComparer.Ordinal);
    }

    [Fact]
    public static void PathsReplaceThePackageDirectoryFilter()
    {
        ScriptRun run = TestFilter.Apply(
            run: Run(NodeTestRunner.Vitest) with
            {
                PathFilter = "packages/web",
            },
            paths: ["packages/web/a.test.ts"],
            coverage: false
        );

        Assert.Null(run.PathFilter);
        Assert.Equal(
            expected: ["packages/web/a.test.ts"],
            actual: run.RunnerArguments,
            comparer: StringComparer.Ordinal
        );
    }

    [Fact]
    public static void CoverageAloneKeepsThePackageDirectoryFilter()
    {
        ScriptRun run = TestFilter.Apply(
            run: Run(NodeTestRunner.Vitest) with
            {
                PathFilter = "packages/web",
            },
            paths: [],
            coverage: true
        );

        Assert.Equal(expected: "packages/web", actual: run.PathFilter);
        Assert.Equal(expected: ["--coverage"], actual: run.RunnerArguments, comparer: StringComparer.Ordinal);
    }

    [Theory]
    [InlineData("a.test.ts")]
    [InlineData("src/a.test.ts")]
    [InlineData("src/**/*.test.ts")]
    [InlineData("@scope/pkg+x/a_b-c.js")]
    [InlineData("a..b.js")]
    [InlineData("./a.test.ts")]
    public static void SafeRelativePathsAreAccepted(string path)
    {
        Assert.True(condition: IsAccepted(path), userMessage: path + " should be accepted");
    }

    [Theory]
    [InlineData("")]
    [InlineData("--watch")]
    [InlineData("-t")]
    [InlineData("/etc/passwd")]
    [InlineData("..")]
    [InlineData("../a.test.ts")]
    [InlineData("src/../../a.test.ts")]
    [InlineData("src/..")]
    [InlineData("a.test.ts; rm -rf x")]
    [InlineData("a.test.ts && b")]
    [InlineData("$(id)")]
    [InlineData("`id`")]
    [InlineData("a|b")]
    [InlineData("a>b")]
    [InlineData("~/a.test.ts")]
    [InlineData("a b")]
    [InlineData("a\\b")]
    [InlineData("a.test.ts\n")]
    public static void UnsafePathsAreRefused(string path)
    {
        Assert.False(condition: IsAccepted(path), userMessage: path + " should be refused");
    }

    [Fact]
    public static void KnownRunnersNeedNoNote()
    {
        Assert.Null(
            TestFilter.Note(
                runs: [Run(NodeTestRunner.Jest), Run(NodeTestRunner.NodeTest)],
                paths: ["a.test.ts"],
                coverage: true
            )
        );
    }

    [Fact]
    public static void AnUnknownRunnerNeedsNoNoteWithoutAFilter()
    {
        Assert.Null(TestFilter.Note(runs: [Run(NodeTestRunner.Unknown)], paths: [], coverage: false));
    }

    [Theory]
    [InlineData(
        true,
        false,
        "The test runner of app is not known, so the paths were passed to its test script after -- and may have been ignored"
    )]
    [InlineData(false, true, "The test runner of app is not known, so no coverage switch was passed")]
    [InlineData(
        true,
        true,
        "The test runner of app is not known, so the paths were passed to its test script after -- and may have been ignored, and no coverage switch was passed"
    )]
    public static void AnUnknownRunnerIsNamedInTheNote(bool withPaths, bool coverage, string expected)
    {
        string? note = TestFilter.Note(
            runs: [Run(NodeTestRunner.Unknown), Run(NodeTestRunner.Unknown), Run(NodeTestRunner.Jest)],
            paths: withPaths ? ["a.test.ts"] : [],
            coverage: coverage
        );

        Assert.Equal(expected: expected, actual: note);
    }

    private static bool IsAccepted(string path)
    {
        return TestFilter.Path().Match(path) is { Success: true } match && match.Length == path.Length;
    }

    private static ScriptRun Run(NodeTestRunner runner)
    {
        string name = runner == NodeTestRunner.Unknown ? "app" : runner.ToString();

        return new(
            Package: new NodePackage(Name: name, RelativeDirectory: ".", Scripts: [NodeScripts.TEST])
            {
                TestRunner = runner,
            },
            Script: NodeScripts.TEST
        );
    }
}
