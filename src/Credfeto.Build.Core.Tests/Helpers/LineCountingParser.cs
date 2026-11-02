using System;
using System.Collections.Generic;
using Credfeto.Build.Core.Jobs;

namespace Credfeto.Build.Core.Tests.Helpers;

/// <summary>Counts lines starting "pass" or "fail" as tests; a stand-in for a language's real parser.</summary>
internal sealed class LineCountingParser : ITestOutputParser
{
    private readonly List<string> _failed = [];
    private int _passed;

    public TestCounts? Counts => new(Total: this._passed + this._failed.Count, Passed: this._passed, Failed: this._failed.Count, Skipped: 0);

    public IReadOnlyList<string> FailedTests => this._failed;

    public void Observe(string line)
    {
        if (line.StartsWith(value: "pass ", comparisonType: StringComparison.Ordinal))
        {
            this._passed++;
        }
        else if (line.StartsWith(value: "fail ", comparisonType: StringComparison.Ordinal))
        {
            this._failed.Add(line[5..]);
        }
    }
}
