using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Credfeto.Build.Core.Jobs;

namespace Credfeto.Build.Node.Server.Testing;

public sealed partial class NodeTestOutputParser : ITestOutputParser
{
    private readonly HashSet<string> _seen = [with(StringComparer.Ordinal)];
    private readonly List<string> _failedTests = [];
    private readonly TestCountTotals _totals = new();
    private int _failed;
    private int _passed;
    private int _skipped;
    private int _total;

    public TestCounts? Counts => this._totals.Counts;

    public IReadOnlyList<string> FailedTests => this._failedTests;

    public void Observe(string line)
    {
        Match summary = Summary().Match(line);

        if (summary.Success)
        {
            this.Count(
                name: summary.Groups["name"].Value,
                value: int.Parse(s: summary.Groups["value"].Value, provider: CultureInfo.InvariantCulture)
            );

            return;
        }

        Match failed = FailedTest().Match(line);

        if (failed.Success && this._seen.Add(failed.Groups["name"].Value))
        {
            this._failedTests.Add(failed.Groups["name"].Value);
        }
    }

    // The last summary line of a run (the count of tests left to write) closes the run and adds it to the totals
    private void Count(string name, int value)
    {
        switch (name)
        {
            case "tests":
                this._total = value;

                break;
            case "pass":
                this._passed = value;

                break;
            case "fail":
                this._failed += value;

                break;
            case "cancelled":
                this._failed += value;

                break;
            case "skipped":
                this._skipped += value;

                break;
            default:
                this._totals.Add(
                    total: this._total,
                    passed: this._passed,
                    failed: this._failed,
                    skipped: this._skipped + value
                );
                this._total = 0;
                this._passed = 0;
                this._failed = 0;
                this._skipped = 0;

                break;
        }
    }

    [GeneratedRegex(
        pattern: @"^\s*[#ℹ]\s+(?<name>tests|pass|fail|cancelled|skipped|todo)\s+(?<value>\d{1,9})\s*$",
        options: RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex Summary();

    [GeneratedRegex(
        pattern: @"^\s*(?:✖\s+(?<name>\S.*?)\s+\(\d[^)]*\)|not ok\s+\d{1,9}\s+-\s+(?<name>\S.*?)(?:\s+#.*)?)\s*$",
        options: RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex FailedTest();
}
