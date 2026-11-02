using System.Collections.Generic;

namespace Credfeto.Build.Core.Jobs;

/// <summary>
///     Reads a language's test runner output line by line to collect pass/fail counts and failing test names.
/// </summary>
public interface ITestOutputParser
{
    TestCounts? Counts { get; }

    IReadOnlyList<string> FailedTests { get; }

    void Observe(string line);
}
