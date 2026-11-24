using System;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Python.Server.Testing;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Build.Python.Server.Tests.Testing;

public sealed class PytestOutputParserTests : TestBase
{
    [Fact]
    public void ASummaryWithFailuresGivesCountsAndFailingIds()
    {
        PytestOutputParser parser = Observe(
            "tests/test_a.py ..F.s",
            "=========================== short test summary info ============================",
            "FAILED tests/test_a.py::test_parse[case-1] - AssertionError: assert 1 == 2",
            "ERROR tests/test_b.py - ModuleNotFoundError: No module named 'httpx'",
            "FAILED tests/test_a.py::test_parse[case-1] - again",
            "==== 1 failed, 3 passed, 1 skipped, 1 xfailed, 1 error, 2 warnings in 0.52s ====="
        );

        Assert.Equal(expected: new TestCounts(Total: 7, Passed: 3, Failed: 2, Skipped: 2), actual: parser.Counts);
        Assert.Equal(
            expected: ["tests/test_a.py::test_parse[case-1]", "tests/test_b.py"],
            actual: parser.FailedTests,
            comparer: StringComparer.Ordinal
        );
    }

    [Fact]
    public void EveryProjectsSummaryIsAdded()
    {
        PytestOutputParser parser = Observe(
            "============================== 3 passed in 0.02s ===============================",
            "===================== 2 passed, 1 xpassed in 1.10s (0:00:01) ====================="
        );

        Assert.Equal(expected: new TestCounts(Total: 6, Passed: 6, Failed: 0, Skipped: 0), actual: parser.Counts);
    }

    [Fact]
    public void NoTestsRanIsZeroTests()
    {
        PytestOutputParser parser = Observe(
            "============================ no tests ran in 0.01s ============================="
        );

        Assert.Equal(expected: new TestCounts(Total: 0, Passed: 0, Failed: 0, Skipped: 0), actual: parser.Counts);
    }

    [Fact]
    public void WithoutASummaryThereAreNoCounts()
    {
        PytestOutputParser parser = Observe("ImportError while loading conftest", "E   ModuleNotFoundError");

        Assert.Null(parser.Counts);
        Assert.Empty(parser.FailedTests);
    }

    private static PytestOutputParser Observe(params string[] lines)
    {
        PytestOutputParser parser = new();

        foreach (string line in lines)
        {
            parser.Observe(line);
        }

        return parser;
    }
}
