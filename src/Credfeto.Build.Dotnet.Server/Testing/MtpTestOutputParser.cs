using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Credfeto.Build.Core.Jobs;

namespace Credfeto.Build.Dotnet.Server.Testing;

/// <summary>
///     Reads Microsoft.Testing.Platform console output: the per-run summary counts (summed over every test
///     project the job runs) and the names of failed tests.
/// </summary>
public sealed partial class MtpTestOutputParser : ITestOutputParser
{
    private readonly List<string> _failedTests = [];
    private int _failed;
    private int _passed;
    private bool _sawSummary;
    private int _skipped;
    private int _total;

    public TestCounts? Counts =>
        this._sawSummary ? new(Total: this._total, Passed: this._passed, Failed: this._failed, Skipped: this._skipped) : null;

    public IReadOnlyList<string> FailedTests => this._failedTests;

    public void Observe(string line)
    {
        Match summary = SummaryCount().Match(line);

        if (summary.Success)
        {
            this.Count(name: summary.Groups["name"].Value, value: int.Parse(s: summary.Groups["value"].Value, provider: CultureInfo.InvariantCulture));

            return;
        }

        Match failed = FailedTest().Match(line);

        if (failed.Success)
        {
            this._failedTests.Add(failed.Groups["name"].Value);
        }
    }

    private void Count(string name, int value)
    {
        this._sawSummary = true;

        switch (name)
        {
            case "total":
                this._total += value;

                break;
            case "failed":
                this._failed += value;

                break;
            case "succeeded":
                this._passed += value;

                break;
            default:
                this._skipped += value;

                break;
        }
    }

    [GeneratedRegex(
        pattern: @"^\s+(?<name>total|failed|succeeded|skipped):\s+(?<value>\d{1,9})\s*$",
        options: RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex SummaryCount();

    [GeneratedRegex(
        pattern: @"^\s*failed\s+(?<name>\S.*?)\s+\(\d[^)]*\)\s*$",
        options: RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex FailedTest();
}
