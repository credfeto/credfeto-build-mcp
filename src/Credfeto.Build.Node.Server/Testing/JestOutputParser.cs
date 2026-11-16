using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Credfeto.Build.Core.Jobs;

namespace Credfeto.Build.Node.Server.Testing;

public sealed partial class JestOutputParser : ITestOutputParser
{
    private const string SUITE_FAILED_TO_RUN = "Test suite failed to run";

    private readonly HashSet<string> _seen = [with(StringComparer.Ordinal)];
    private readonly List<string> _failedTests = [];
    private readonly TestCountTotals _totals = new();

    public TestCounts? Counts => this._totals.Counts;

    public IReadOnlyList<string> FailedTests => this._failedTests;

    public void Observe(string line)
    {
        Match summary = Summary().Match(line);

        if (summary.Success)
        {
            string counts = summary.Groups["counts"].Value;
            this._totals.Add(
                total: SummaryParts.Count(text: counts, name: "total"),
                passed: SummaryParts.Count(text: counts, name: "passed"),
                failed: SummaryParts.Count(text: counts, name: "failed"),
                skipped: SummaryParts.Count(text: counts, name: "skipped")
                    + SummaryParts.Count(text: counts, name: "todo")
            );

            return;
        }

        Match failed = FailedTest().Match(line);

        if (
            failed.Success
            && !StringComparer.Ordinal.Equals(x: failed.Groups["name"].Value, y: SUITE_FAILED_TO_RUN)
            && this._seen.Add(failed.Groups["name"].Value)
        )
        {
            this._failedTests.Add(failed.Groups["name"].Value);
        }
    }

    [GeneratedRegex(
        pattern: @"^\s*Tests:\s+(?<counts>\d{1,9}\s+[a-z]+(?:,\s*\d{1,9}\s+[a-z]+)*)\s*$",
        options: RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex Summary();

    [GeneratedRegex(
        pattern: @"^\s*●\s+(?<name>\S.*?)\s*$",
        options: RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex FailedTest();
}
