using System;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Dotnet.Server.Testing;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Build.Dotnet.Server.Tests.Testing;

public sealed class MtpTestOutputParserTests : TestBase
{
    private static readonly string[] FirstProject =
    [
        "Running tests from bin/Release/net10.0/Example.Core.Tests.dll (net10.0|x64)",
        "failed Example.Core.Tests.AlphaTests.RunsAsync (12ms)",
        "  Assert.Equal() Failure: Values differ",
        "failed Example.Core.Tests.BetaTests.Works(value: 3) (1s 004ms)",
        "Test run summary: Failed! - bin/Release/net10.0/Example.Core.Tests.dll (net10.0|x64)",
        "  total: 10",
        "  failed: 2",
        "  succeeded: 7",
        "  skipped: 1",
        "  duration: 2s 120ms",
    ];

    private static readonly string[] SecondProject =
    [
        "Test run summary: Passed! - bin/Release/net10.0/Example.Api.Tests.dll (net10.0|x64)",
        "  total: 5",
        "  failed: 0",
        "  succeeded: 5",
        "  skipped: 0",
    ];

    [Fact]
    public static void CountsAreSummedOverProjectsAndFailuresNamed()
    {
        MtpTestOutputParser parser = new();

        foreach (string line in FirstProject)
        {
            parser.Observe(line);
        }

        foreach (string line in SecondProject)
        {
            parser.Observe(line);
        }

        Assert.Equal(expected: new TestCounts(Total: 15, Passed: 12, Failed: 2, Skipped: 1), actual: parser.Counts);
        Assert.Equal(
            expected: ["Example.Core.Tests.AlphaTests.RunsAsync", "Example.Core.Tests.BetaTests.Works(value: 3)"],
            actual: parser.FailedTests,
            comparer: StringComparer.Ordinal
        );
    }

    [Fact]
    public static void NoSummaryMeansNoCounts()
    {
        MtpTestOutputParser parser = new();
        parser.Observe("error CS1002: ; expected");
        parser.Observe("Build FAILED.");

        Assert.Null(parser.Counts);
        Assert.Empty(parser.FailedTests);
    }
}
