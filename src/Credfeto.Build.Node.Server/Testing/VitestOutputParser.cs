using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Credfeto.Build.Core.Jobs;

namespace Credfeto.Build.Node.Server.Testing;

public sealed partial class VitestOutputParser : ITestOutputParser
{
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
            this._totals.Add(
                total: Number(summary.Groups["total"].Value),
                passed: SummaryParts.Count(text: summary.Groups["counts"].Value, name: "passed"),
                failed: SummaryParts.Count(text: summary.Groups["counts"].Value, name: "failed"),
                skipped: SummaryParts.Count(text: summary.Groups["counts"].Value, name: "skipped")
                    + SummaryParts.Count(text: summary.Groups["counts"].Value, name: "todo")
            );

            return;
        }

        Match failed = FailedTest().Match(line);

        if (failed.Success && this._seen.Add(failed.Groups["name"].Value))
        {
            this._failedTests.Add(failed.Groups["name"].Value);
        }
    }

    private static int Number(string digits)
    {
        return int.Parse(s: digits, provider: CultureInfo.InvariantCulture);
    }

    [GeneratedRegex(
        pattern: @"^\s*Tests\s+(?<counts>\d{1,9}\s+[a-z]+(?:\s*\|\s*\d{1,9}\s+[a-z]+)*)\s+\((?<total>\d{1,9})\)",
        options: RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex Summary();

    [GeneratedRegex(
        pattern: @"^\s*FAIL\s+(?<name>\S.*?\s>\s.*?)\s*$",
        options: RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex FailedTest();
}
