using System.Collections.Generic;
using System.Diagnostics;
using Credfeto.Build.Core.Coverage;

namespace Credfeto.Build.Core.Jobs;

[DebuggerDisplay("Id={Id}, Kind={Kind}, State={State}")]
public sealed record JobResult(
    string Id,
    string Kind,
    JobState State,
    double DurationSeconds,
    string LogPath,
    IReadOnlyList<StepResult> Steps,
    TestCounts? Tests,
    IReadOnlyList<string> FailedTests,
    IReadOnlyList<string> OutputTail,
    CoverageSummary? Coverage,
    string? CoverageMarkdown,
    IReadOnlyList<string> CoverageErrors,
    string? HeadSha,
    string? Message
);
