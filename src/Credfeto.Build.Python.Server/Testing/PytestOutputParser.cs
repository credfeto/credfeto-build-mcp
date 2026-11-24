using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Credfeto.Build.Core.Jobs;

namespace Credfeto.Build.Python.Server.Testing;

// Reads pytest's output: the closing summary line ("=== 2 failed, 10 passed, 1 skipped in 0.12s ===") for the
// counts, summed over every project run, and the -rfE short summary ("FAILED tests/test_x.py::test_y - ...") for the
// failing test ids. Errors count as failures, xfailed as skipped and xpassed as passed
public sealed partial class PytestOutputParser : ITestOutputParser
{
    private const int MAX_FAILED_TESTS = 500;

    private readonly List<string> _failedTests = [];
    private readonly HashSet<string> _seen = [with(StringComparer.Ordinal)];
    private int _failed;
    private int _passed;
    private bool _sawSummary;
    private int _skipped;

    public TestCounts? Counts =>
        this._sawSummary
            ? new(
                Total: this._passed + this._failed + this._skipped,
                Passed: this._passed,
                Failed: this._failed,
                Skipped: this._skipped
            )
            : null;

    public IReadOnlyList<string> FailedTests => this._failedTests;

    public void Observe(string line)
    {
        Match summary = Summary().Match(line);

        if (summary.Success)
        {
            this.AddSummary(summary.Groups["counts"].Value);

            return;
        }

        Match failed = FailedTest().Match(line);

        if (failed.Success && this._failedTests.Count < MAX_FAILED_TESTS && this._seen.Add(failed.Groups["name"].Value))
        {
            this._failedTests.Add(failed.Groups["name"].Value);
        }
    }

    private void AddSummary(string counts)
    {
        Dictionary<string, int> parts = Part()
            .Matches(counts)
            .GroupBy(static part => part.Groups["name"].Value, StringComparer.Ordinal)
            .ToDictionary(
                keySelector: static group => group.Key,
                elementSelector: static group =>
                    group.Sum(static part =>
                        int.Parse(s: part.Groups["value"].Value, provider: CultureInfo.InvariantCulture)
                    ),
                comparer: StringComparer.Ordinal
            );

        this._sawSummary = true;
        this._passed += Count(parts: parts, name: "passed") + Count(parts: parts, name: "xpassed");
        this._failed +=
            Count(parts: parts, name: "failed")
            + Count(parts: parts, name: "error")
            + Count(parts: parts, name: "errors");
        this._skipped += Count(parts: parts, name: "skipped") + Count(parts: parts, name: "xfailed");
    }

    private static int Count(Dictionary<string, int> parts, string name)
    {
        return parts.TryGetValue(key: name, value: out int value) ? value : 0;
    }

    [GeneratedRegex(
        pattern: @"^=+ (?<counts>.*?)\s*in \d+(?:\.\d+)?s(?: \([^)]*\))? =+\s*$",
        options: RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex Summary();

    [GeneratedRegex(
        pattern: @"(?<value>\d{1,9}) (?<name>[a-z]+)",
        options: RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex Part();

    [GeneratedRegex(
        pattern: @"^(?:FAILED|ERROR) (?<name>\S+)",
        options: RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex FailedTest();
}
