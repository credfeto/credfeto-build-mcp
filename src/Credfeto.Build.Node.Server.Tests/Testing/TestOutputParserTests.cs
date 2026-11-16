using System;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Node.Server.Detection;
using Credfeto.Build.Node.Server.Testing;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Build.Node.Server.Tests.Testing;

public sealed class TestOutputParserTests : TestBase
{
    private static readonly string[] Vitest =
    [
        " ✓ src/a.test.ts (2 tests) 3ms",
        " ❯ src/b.test.ts (3 tests | 1 failed) 5ms",
        "   × suite > fails 2ms",
        "     → expected 1 to be 2",
        "⎯⎯⎯⎯⎯⎯⎯ Failed Tests 1 ⎯⎯⎯⎯⎯⎯⎯",
        " FAIL  src/b.test.ts > suite > fails",
        "AssertionError: expected 1 to be 2",
        " Test Files  1 failed | 1 passed (2)",
        "      Tests  1 failed | 3 passed | 1 skipped (5)",
        "   Duration  300ms",
    ];

    private static readonly string[] Jest =
    [
        "FAIL src/b.test.js",
        "  ● suite › fails",
        "    expect(received).toBe(expected)",
        "  ● Test suite failed to run",
        "PASS src/a.test.js",
        "Test Suites: 1 failed, 1 passed, 2 total",
        "Tests:       1 failed, 1 skipped, 3 passed, 5 total",
    ];

    private static readonly string[] NodeSpec =
    [
        "✔ adds (1.0235ms)",
        "✖ fails here (1.082479ms)",
        "﹣ skipped one (0.194204ms) # SKIP",
        "ℹ tests 3",
        "ℹ suites 0",
        "ℹ pass 1",
        "ℹ fail 1",
        "ℹ cancelled 0",
        "ℹ skipped 1",
        "ℹ todo 0",
        "ℹ duration_ms 108.669276",
        "✖ failing tests:",
        "test at a.test.js:3:1",
        "✖ fails here (1.082479ms)",
    ];

    private static readonly string[] NodeTap =
    [
        "TAP version 13",
        "ok 1 - adds",
        "not ok 2 - fails here",
        "  ---",
        "ok 3 - skipped one # SKIP",
        "# tests 3",
        "# suites 0",
        "# pass 1",
        "# fail 1",
        "# cancelled 1",
        "# skipped 1",
        "# todo 0",
    ];

    [Fact]
    public void VitestCountsAndFailuresAreRead()
    {
        ITestOutputParser parser = Feed(parser: new VitestOutputParser(), lines: Vitest);

        Assert.Equal(expected: new TestCounts(Total: 5, Passed: 3, Failed: 1, Skipped: 1), actual: parser.Counts);
        Assert.Equal(
            expected: ["src/b.test.ts > suite > fails"],
            actual: parser.FailedTests,
            comparer: StringComparer.Ordinal
        );
    }

    [Fact]
    public void VitestCountsAreSummedOverPackages()
    {
        ITestOutputParser parser = Feed(parser: new VitestOutputParser(), lines: [.. Vitest, .. Vitest]);

        Assert.Equal(expected: new TestCounts(Total: 10, Passed: 6, Failed: 2, Skipped: 2), actual: parser.Counts);
        Assert.Single(parser.FailedTests);
    }

    [Fact]
    public void JestCountsAndFailuresAreRead()
    {
        ITestOutputParser parser = Feed(parser: new JestOutputParser(), lines: Jest);

        Assert.Equal(expected: new TestCounts(Total: 5, Passed: 3, Failed: 1, Skipped: 1), actual: parser.Counts);
        Assert.Equal(expected: ["suite › fails"], actual: parser.FailedTests, comparer: StringComparer.Ordinal);
    }

    [Fact]
    public void NodeSpecCountsAndFailuresAreReadOnce()
    {
        ITestOutputParser parser = Feed(parser: new NodeTestOutputParser(), lines: NodeSpec);

        Assert.Equal(expected: new TestCounts(Total: 3, Passed: 1, Failed: 1, Skipped: 1), actual: parser.Counts);
        Assert.Equal(expected: ["fails here"], actual: parser.FailedTests, comparer: StringComparer.Ordinal);
    }

    [Fact]
    public void NodeTapCountsCancelledTestsAsFailed()
    {
        ITestOutputParser parser = Feed(parser: new NodeTestOutputParser(), lines: NodeTap);

        Assert.Equal(expected: new TestCounts(Total: 3, Passed: 1, Failed: 2, Skipped: 1), actual: parser.Counts);
        Assert.Equal(expected: ["fails here"], actual: parser.FailedTests, comparer: StringComparer.Ordinal);
    }

    [Fact]
    public void NoSummaryMeansNoCounts()
    {
        ITestOutputParser parser = Feed(parser: new NodeTestOutputParser(), lines: ["npm error Missing script: test"]);

        Assert.Null(parser.Counts);
        Assert.Empty(parser.FailedTests);
    }

    [Fact]
    public void TheCompositeReadsEveryRunnerInTheJob()
    {
        ITestOutputParser parser = Feed(
            parser: CompositeTestOutputParser.For([NodeTestRunner.Jest, NodeTestRunner.NodeTest, NodeTestRunner.Jest]),
            lines: [.. Jest, .. NodeSpec]
        );

        Assert.Equal(expected: new TestCounts(Total: 8, Passed: 4, Failed: 2, Skipped: 2), actual: parser.Counts);
        Assert.Equal(
            expected: ["suite › fails", "fails here"],
            actual: parser.FailedTests,
            comparer: StringComparer.Ordinal
        );
    }

    [Fact]
    public void AnUnknownRunnerReportsNoCounts()
    {
        ITestOutputParser parser = Feed(parser: CompositeTestOutputParser.For([NodeTestRunner.Unknown]), lines: Vitest);

        Assert.Null(parser.Counts);
        Assert.Empty(parser.FailedTests);
    }

    private static ITestOutputParser Feed(ITestOutputParser parser, string[] lines)
    {
        foreach (string line in lines)
        {
            parser.Observe(line);
        }

        return parser;
    }
}
